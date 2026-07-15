using System.Reflection;
using ImageArchive.Abstractions;
using ImageArchive.Codecs.Skia;
using ImageArchive.Geometry;
using ImageArchive.Integrity;
using ImageArchive.Internal;
using ImageArchive.IO;
using ImageArchive.Manifest;
using ImageArchive.Qr;

namespace ImageArchive;

public sealed class ImageArchiveEncodeOptions
{
    public IImageEncoder? Encoder { get; init; }
    public IQrCodeService? QrCodeService { get; init; }
    public IArchiveSourceLoader? SourceLoader { get; init; }
    public IStreamProcessor? StreamProcessor { get; init; }
    public IManifestValidator? ManifestValidator { get; init; }
    public string? ToolCommitUrl { get; init; }
    public string? WorkingDirectory { get; init; }
    /// <summary>When true, invert header/footer chrome (black background, light foreground/QR).</summary>
    public bool Dark { get; init; }
    /// <summary>Optional square frame width override (512–1440). Overrides manifest.frameWidth when set.</summary>
    public int? FrameWidth { get; init; }
}

public sealed class EncodeResult
{
    public int FrameCount { get; init; }
    public required string StreamSha256 { get; init; }
    public required ImageArchiveManifest EmbeddedManifest { get; init; }
    public required ArchiveMetadata Metadata { get; init; }
}

public interface IImageArchiveEncoder
{
    EncodeResult Encode(ImageArchiveManifest manifest, Stream output, ImageArchiveEncodeOptions? options = null);
    EncodeResult Encode(ImageArchiveManifest manifest, Stream archiveInput, Stream output, ImageArchiveEncodeOptions? options = null);
}

public sealed class ImageArchiveEncoder : IImageArchiveEncoder
{
    public EncodeResult Encode(ImageArchiveManifest manifest, Stream output, ImageArchiveEncodeOptions? options = null)
    {
        options ??= new ImageArchiveEncodeOptions();
        var loader = options.SourceLoader ?? new DefaultArchiveSourceLoader();
        using var src = loader.OpenRead(manifest.Archive, options.WorkingDirectory);
        return Encode(manifest, src, output, options);
    }

    public EncodeResult Encode(ImageArchiveManifest manifest, Stream archiveInput, Stream output, ImageArchiveEncodeOptions? options = null)
    {
        options ??= new ImageArchiveEncodeOptions();
        var validator = options.ManifestValidator ?? new JsonSchemaManifestValidator();
        var vr = validator.Validate(manifest);
        if (!vr.IsValid)
            throw new ManifestValidationException(vr);

        var processor = options.StreamProcessor ?? new ExternalCommandStreamProcessor();
        using var processed = processor.Apply(archiveInput, manifest.Preprocessor);
        if (processed.CanSeek) processed.Position = 0;
        using var archiveMs = new MemoryStream();
        processed.CopyTo(archiveMs);
        var archiveBytes = archiveMs.ToArray();

        var qr = options.QrCodeService ?? new DefaultQrCodeService();
        var encoder = options.Encoder ?? ImageArchiveCodecs.CreateDefaultEncoder(manifest.Output.Format);

        // CLI/options width overrides manifest; persist effective width into embedded manifest.
        var frameWidth = options.FrameWidth
            ?? FrameGeometry.ClampOrDefault(manifest.FrameWidth);
        FrameGeometry.ValidateWidth(frameWidth);
        manifest.FrameWidth = frameWidth;

        using (FrameGeometry.Use(frameWidth))
        {
            var frames = new List<FrameBitmap>();
            var offset = 0;
            var capacity = FrameGeometry.FrameCapacityBytes;
            var frameCount = Math.Max(1, (int)Math.Ceiling(archiveBytes.Length / (double)capacity));
            while (manifest.Frames.Count < frameCount)
                manifest.Frames.Add(new FrameManifestSection());

            if (options.Dark)
                manifest.Dark = true;
            var dark = manifest.Dark;
            for (var i = 0; i < frameCount; i++)
            {
                var remaining = archiveBytes.Length - offset;
                var take = Math.Min(capacity, Math.Max(0, remaining));
                var slice = take > 0 ? archiveBytes.AsSpan(offset, take) : ReadOnlySpan<byte>.Empty;
                var dataRegion = PixelPacker.PackDataRegion(slice);
                var sha = Sha256Hex.Compute(dataRegion);
                var header = manifest.Frames[i].Header ?? manifest.Header;
                frames.Add(FrameRenderer.RenderFrame(
                    i, frameCount, dataRegion, sha, header, options.ToolCommitUrl, qr, options.WorkingDirectory,
                    dark: dark));
                offset += take;
            }

            // Hash concatenated data regions (with padding) for stream integrity
            string streamSha;
            using (var concat = new MemoryStream())
            {
                offset = 0;
                for (var i = 0; i < frameCount; i++)
                {
                    var remaining = archiveBytes.Length - offset;
                    var take = Math.Min(capacity, Math.Max(0, remaining));
                    var slice = take > 0 ? archiveBytes.AsSpan(offset, take) : ReadOnlySpan<byte>.Empty;
                    concat.Write(PixelPacker.PackDataRegion(slice));
                    offset += take;
                }
                streamSha = Sha256Hex.Compute(concat.ToArray());
            }

            manifest.StreamSha256 = streamSha;
            var schemaJson = LoadEmbeddedSchema();
            var manifestJson = ManifestJson.Serialize(manifest);

            var metadata = new ArchiveMetadata
            {
                EncoderName = manifest.Encoder.Name,
                EncoderVersion = manifest.Encoder.Version,
                EncoderSha256 = manifest.Encoder.Sha256,
                MimeType = manifest.Archive.MimeType,
                ArchiveType = manifest.Archive.Type,
                JsonSchema = schemaJson,
                JsonManifest = manifestJson,
                SourceUrl = manifest.Archive.SourceUrl,
                CommitHash = manifest.Archive.CommitHash,
                OriginalFileName = manifest.Archive.OriginalFileName
            };
            metadata.AdditionalTextChunks["originalLength"] = archiveBytes.Length.ToString();
            metadata.AdditionalTextChunks["frameWidth"] = frameWidth.ToString();
            offset = 0;
            for (var i = 0; i < frameCount; i++)
            {
                var remaining = archiveBytes.Length - offset;
                var take = Math.Min(capacity, Math.Max(0, remaining));
                var slice = take > 0 ? archiveBytes.AsSpan(offset, take) : ReadOnlySpan<byte>.Empty;
                metadata.AdditionalTextChunks[$"frameSha256.{i}"] = Sha256Hex.Compute(PixelPacker.PackDataRegion(slice));
                offset += take;
            }

            encoder.Encode(output, frames, metadata, FrameGeometry.AnimationDelayMilliseconds);

            return new EncodeResult
            {
                FrameCount = frameCount,
                StreamSha256 = streamSha,
                EmbeddedManifest = manifest,
                Metadata = metadata
            };
        }
    }

    private static string LoadEmbeddedSchema()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().First(n => n.EndsWith("imagearchive-schema.json", StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
