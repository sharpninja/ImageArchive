using ImageArchive.Abstractions;
using ImageArchive.Codecs.Skia;
using ImageArchive.Integrity;
using ImageArchive.Internal;
using ImageArchive.IO;
using ImageArchive.Manifest;
using ImageArchive.Qr;

namespace ImageArchive;

public sealed class ImageArchiveDecodeOptions
{
    public IImageDecoder? Decoder { get; init; }
    public IQrCodeService? QrCodeService { get; init; }
    public IStreamProcessor? StreamProcessor { get; init; }
    public bool VerifyFrameIntegrity { get; init; } = true;
    public bool VerifyStreamSha256 { get; init; } = true;
    public string? Postprocessor { get; init; }
}

public sealed class DecodeResult
{
    public required Stream ArchiveStream { get; init; }
    public required ArchiveMetadata Metadata { get; init; }
    public ImageArchiveManifest? Manifest { get; init; }
    public int FrameCount { get; init; }
    public required string StreamSha256 { get; init; }
}

public interface IImageArchiveDecoder
{
    DecodeResult Decode(Stream imageInput, ImageArchiveDecodeOptions? options = null);
}

public sealed class ImageArchiveDecoder : IImageArchiveDecoder
{
    public DecodeResult Decode(Stream imageInput, ImageArchiveDecodeOptions? options = null)
    {
        options ??= new ImageArchiveDecodeOptions();
        var decoder = options.Decoder ?? ImageArchiveCodecs.CreateDefaultDecoder();
        var container = decoder.Decode(imageInput);
        var frames = container.Frames;
        var qr = options.QrCodeService ?? new DefaultQrCodeService();

        using var concat = new MemoryStream();
        for (var i = 0; i < frames.Count; i++)
        {
            var data = PixelPacker.ExtractDataRegion(frames[i]);
            var actual = Sha256Hex.Compute(data);
            if (options.VerifyFrameIntegrity)
            {
                var hasStored = container.Metadata.AdditionalTextChunks.TryGetValue($"frameSha256.{i}", out var stored);
                var left = ExtractQrCell(frames[i], left: true);
                var fromQr = qr.DecodeFromPixels(left, Geometry.FrameGeometry.QrCellSize, Geometry.FrameGeometry.QrCellSize, frames[i].Format);
                var hasQr = !string.IsNullOrEmpty(fromQr);

                // Fail-closed: require at least one source; every present source must match actual
                if (!hasStored && !hasQr)
                    throw new IntegrityException($"Frame {i} missing integrity hash (QR undecodable and no frameSha256 metadata).", i, null, actual);
                if (hasStored && !string.Equals(stored, actual, StringComparison.OrdinalIgnoreCase))
                    throw new IntegrityException($"Frame {i} metadata frameSha256 mismatch.", i, stored, actual);
                if (hasQr && !string.Equals(fromQr, actual, StringComparison.OrdinalIgnoreCase))
                    throw new IntegrityException($"Frame {i} QR hash mismatch.", i, fromQr, actual);
            }
            concat.Write(data);
        }

        var padded = concat.ToArray();
        var streamSha = Sha256Hex.Compute(padded);

        ImageArchiveManifest? manifest = null;
        if (!string.IsNullOrWhiteSpace(container.Metadata.JsonManifest))
        {
            try { manifest = ManifestJson.Deserialize(container.Metadata.JsonManifest); }
            catch { /* leave null */ }
        }

        if (options.VerifyStreamSha256 && manifest?.StreamSha256 != null)
        {
            if (!string.Equals(manifest.StreamSha256, streamSha, StringComparison.OrdinalIgnoreCase))
                throw new IntegrityException("streamSha256 mismatch.", null, manifest.StreamSha256, streamSha);
        }

        byte[] archiveBytes;
        if (container.Metadata.AdditionalTextChunks.TryGetValue("originalLength", out var lenStr)
            && long.TryParse(lenStr, out var origLen)
            && origLen >= 0 && origLen <= padded.Length)
        {
            archiveBytes = new byte[origLen];
            Buffer.BlockCopy(padded, 0, archiveBytes, 0, (int)origLen);
        }
        else
        {
            archiveBytes = TrimLastFramePadding(padded, frames.Count);
        }

        Stream archiveStream = new MemoryStream(archiveBytes);
        var processor = options.StreamProcessor ?? new ExternalCommandStreamProcessor();
        var post = options.Postprocessor ?? manifest?.Postprocessor;
        archiveStream = processor.Apply(archiveStream, post);

        return new DecodeResult
        {
            ArchiveStream = archiveStream,
            Metadata = container.Metadata,
            Manifest = manifest,
            FrameCount = frames.Count,
            StreamSha256 = streamSha
        };
    }

    private static byte[] ExtractQrCell(FrameBitmap frame, bool left)
    {
        var bpp = frame.BytesPerPixel;
        var cell = new byte[Geometry.FrameGeometry.QrCellSize * Geometry.FrameGeometry.QrCellSize * bpp];
        var leftX = left ? 0 : Geometry.FrameGeometry.Width - Geometry.FrameGeometry.QrCellSize;
        var topY = Geometry.FrameGeometry.FooterFirstRow;
        var di = 0;
        for (var y = 0; y < Geometry.FrameGeometry.QrCellSize; y++)
        for (var x = 0; x < Geometry.FrameGeometry.QrCellSize; x++)
        {
            var si = ((topY + y) * Geometry.FrameGeometry.Width + leftX + x) * bpp;
            for (var b = 0; b < bpp; b++)
                cell[di++] = frame.Pixels[si + b];
        }
        return cell;
    }

    private static byte[] TrimLastFramePadding(byte[] padded, int frameCount)
    {
        // Last frame padding is zeros at end of last FrameCapacityBytes block.
        // Original archive length is not explicitly stored — we store it in metadata additional during encode.
        // Fallback: strip trailing zeros from entire stream carefully only within last frame.
        if (frameCount == 0) return padded;
        var cap = Geometry.FrameGeometry.FrameCapacityBytes;
        var lastStart = (frameCount - 1) * cap;
        var end = padded.Length;
        while (end > lastStart && padded[end - 1] == 0)
            end--;
        // If entire last frame empty and not first frame, still ok
        var result = new byte[end];
        Buffer.BlockCopy(padded, 0, result, 0, end);
        return result;
    }

    private static byte[] TrimTrailingPadding(byte[] padded, int frameCount) => TrimLastFramePadding(padded, frameCount);
}
