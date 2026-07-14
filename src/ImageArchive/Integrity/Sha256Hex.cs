using System.Security.Cryptography;
using System.Text;

namespace ImageArchive.Integrity;

public static class Sha256Hex
{
    public static string Compute(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return ToLowerHex(hash);
    }

    public static string Compute(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;
        var hash = SHA256.HashData(stream);
        if (stream.CanSeek)
            stream.Position = 0;
        return ToLowerHex(hash);
    }

    public static string Compute(byte[] data) => Compute(data.AsSpan());

    private static string ToLowerHex(ReadOnlySpan<byte> hash)
    {
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
