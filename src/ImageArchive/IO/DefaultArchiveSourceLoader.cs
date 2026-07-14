using System.Formats.Tar;
using System.IO.Compression;
using ImageArchive.Abstractions;
using ImageArchive.Manifest;

namespace ImageArchive.IO;

public sealed class DefaultArchiveSourceLoader : IArchiveSourceLoader
{
    private static readonly HashSet<string> ExcludeDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", "node_modules"
    };

    public Stream OpenRead(ArchiveManifestSection archive, string? workingDirectory = null)
    {
        var root = workingDirectory ?? Directory.GetCurrentDirectory();
        var source = Path.IsPathRooted(archive.Source)
            ? archive.Source
            : Path.GetFullPath(Path.Combine(root, archive.Source));

        return archive.Type switch
        {
            ArchiveType.Raw or ArchiveType.Zip or ArchiveType.Tar => OpenFile(source),
            ArchiveType.Git => BuildGitWorktreeTarGz(source),
            _ => throw new ArchiveSourceException($"Unknown archive type {archive.Type}")
        };
    }

    private static Stream OpenFile(string path)
    {
        if (!File.Exists(path))
            throw new ArchiveSourceException($"Source file not found: {path}");
        return File.OpenRead(path);
    }

    /// <summary>Compressed tar of .git + working tree, excluding bin/obj.</summary>
    public static MemoryStream BuildGitWorktreeTarGz(string directory)
    {
        if (!Directory.Exists(directory))
            throw new ArchiveSourceException($"Source directory not found: {directory}");

        var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        using (var tar = new TarWriter(gz, leaveOpen: true))
        {
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(f => f)
                .Where(f => !IsExcluded(directory, f))
                .OrderBy(f => f.Replace('\\', '/'), StringComparer.Ordinal)
                .ToList();

            foreach (var file in files)
            {
                var rel = Path.GetRelativePath(directory, file).Replace('\\', '/');
                var entry = new PaxTarEntry(TarEntryType.RegularFile, rel)
                {
                    DataStream = File.OpenRead(file)
                };
                try
                {
                    tar.WriteEntry(entry);
                }
                finally
                {
                    entry.DataStream?.Dispose();
                }
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static bool IsExcluded(string root, string fullPath)
    {
        var rel = Path.GetRelativePath(root, fullPath);
        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => ExcludeDirNames.Contains(p));
    }
}
