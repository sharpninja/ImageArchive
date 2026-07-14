using ImageArchive.Manifest;

namespace ImageArchive.Abstractions;

public interface IArchiveSourceLoader
{
    Stream OpenRead(ArchiveManifestSection archive, string? workingDirectory = null);
}
