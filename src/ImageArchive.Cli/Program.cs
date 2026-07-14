using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using ImageArchive;
using ImageArchive.Encoders;
using ImageArchive.Models;

var root = new RootCommand("ImageArchive – embed any archive inside a standard animated image");

// ------------------------------------------------------------------
// encode command
// ------------------------------------------------------------------
var encodeCmd = new Command("encode", "Create an ImageArchive from a JSON manifest");
var manifestOption = new Option<FileInfo>("--manifest", "Path to the JSON manifest") { IsRequired = true };
var outputOption = new Option<FileInfo?>("--output", "Override output path from the manifest");
encodeCmd.AddOption(manifestOption);
encodeCmd.AddOption(outputOption);

encodeCmd.SetHandler(async (FileInfo manifestFile, FileInfo? outputOverride) =>
{
    string json = await File.ReadAllTextAsync(manifestFile.FullName);
    var manifest = JsonSerializer.Deserialize<Manifest>(json)
                   ?? throw new InvalidOperationException("Invalid manifest");

    if (outputOverride != null)
        manifest.Output.Path = outputOverride.FullName;

    // Compute encoder SHA if not supplied
    if (string.IsNullOrEmpty(manifest.Encoder.Sha256))
    {
        string exePath = Environment.ProcessPath ?? "ImageArchive";
        if (File.Exists(exePath))
        {
            byte[] hash = SHA256.HashData(await File.ReadAllBytesAsync(exePath));
            manifest.Encoder.Sha256 = Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    // Load the source data
    Stream dataStream;
    if (manifest.Archive.Type == "git")
    {
        // TODO: create a proper git archive (tar of .git + working tree)
        Console.WriteLine("Git archive creation not yet implemented in this skeleton.");
        return;
    }
    else
    {
        dataStream = File.OpenRead(manifest.Archive.Source);
    }

    // Optional preprocessor
    if (!string.IsNullOrWhiteSpace(manifest.Preprocessor))
    {
        // In a full implementation we would pipe through the external command
        Console.WriteLine($"Preprocessor '{manifest.Preprocessor}' would be applied here.");
    }

    IImageEncoder encoder = manifest.Output.Format.ToLowerInvariant() switch
    {
        "png"  => new PngEncoder(),
        "webp" => throw new NotImplementedException("WebP encoder coming soon"),
        _      => throw new ArgumentException($"Unsupported format: {manifest.Output.Format}")
    };

    await encoder.EncodeAsync(dataStream, manifest, manifest.Output.Path);
    Console.WriteLine($"Created {manifest.Output.Path}");
}, manifestOption, outputOption);

// ------------------------------------------------------------------
// decode command
// ------------------------------------------------------------------
var decodeCmd = new Command("decode", "Extract an ImageArchive");
var inputOption = new Option<FileInfo>("--input", "Path to the ImageArchive file") { IsRequired = true };
var outDirOption = new Option<DirectoryInfo>("--output", "Directory to extract into") { IsRequired = true };
decodeCmd.AddOption(inputOption);
decodeCmd.AddOption(outDirOption);

decodeCmd.SetHandler(async (FileInfo input, DirectoryInfo outDir) =>
{
    outDir.Create();

    IImageEncoder encoder = new PngEncoder(); // auto-detect later
    var result = await encoder.DecodeAsync(input.FullName);

    // Write the raw data
    string outFile = Path.Combine(outDir.FullName, result.Manifest.Archive.OriginalFileName ?? "archive.bin");
    await using var fs = File.Create(outFile);
    await result.DataStream.CopyToAsync(fs);

    Console.WriteLine($"Extracted to {outFile}");
    Console.WriteLine($"Verified {result.FrameSha256s.Count} frames");
}, inputOption, outDirOption);

root.AddCommand(encodeCmd);
root.AddCommand(decodeCmd);

return await root.InvokeAsync(args);
