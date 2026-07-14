namespace ImageArchive.Abstractions;

public interface IQrCodeService
{
    bool[,] EncodeModules(string payload);
    string DecodeFromPixels(ReadOnlySpan<byte> pixels, int width, int height, PixelFormat format);
    int MaxPayloadLength { get; }
}
