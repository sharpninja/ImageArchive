using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace ImageArchive.Codecs.Skia;

internal static class PngChunkIo
{
    public static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static void WriteChunk(Stream s, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        s.Write(len);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = Crc32.Compute(typeBytes, data);
        Span<byte> crcBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuf, crc);
        s.Write(crcBuf);
    }

    public static List<(string Type, byte[] Data)> ReadChunks(Stream s)
    {
        var sig = new byte[8];
        if (s.Read(sig) != 8 || !sig.AsSpan().SequenceEqual(Signature))
            throw new ImageArchiveException("Not a PNG file.");
        var list = new List<(string, byte[])>();
        Span<byte> lenBuf = stackalloc byte[4];
        while (s.Position < s.Length)
        {
            if (s.Read(lenBuf) != 4) break;
            var len = BinaryPrimitives.ReadUInt32BigEndian(lenBuf);
            var typeBuf = new byte[4];
            if (s.Read(typeBuf) != 4) break;
            var type = Encoding.ASCII.GetString(typeBuf);
            var data = new byte[len];
            if (len > 0 && s.Read(data) != len) throw new ImageArchiveException("Truncated PNG chunk.");
            s.Position += 4; // crc
            list.Add((type, data));
            if (type == "IEND") break;
        }
        return list;
    }

    public static byte[] ExtractIdatFromPng(byte[] pngBytes)
    {
        using var ms = new MemoryStream(pngBytes);
        var chunks = ReadChunks(ms);
        using var idat = new MemoryStream();
        foreach (var (type, data) in chunks)
            if (type == "IDAT") idat.Write(data);
        return idat.ToArray();
    }

    public static byte[] BuildTextChunkPayload(string key, string value)
    {
        // tEXt: keyword\0text (Latin-1-ish; we use UTF-8 bytes which is fine for our ASCII keys)
        var k = Encoding.ASCII.GetBytes(key);
        var v = Encoding.UTF8.GetBytes(value);
        var buf = new byte[k.Length + 1 + v.Length];
        k.CopyTo(buf, 0);
        buf[k.Length] = 0;
        v.CopyTo(buf, k.Length + 1);
        return buf;
    }

    public static (string Key, string Value)? ParseTextChunk(byte[] data)
    {
        var z = Array.IndexOf(data, (byte)0);
        if (z <= 0) return null;
        var key = Encoding.ASCII.GetString(data, 0, z);
        var val = Encoding.UTF8.GetString(data, z + 1, data.Length - z - 1);
        return (key, val);
    }

    /// <summary>Adler32 + zlib header for raw deflate of scanlines (filter 0).</summary>
    public static byte[] CompressRgb24AsIdat(byte[] rgb, int width, int height)
    {
        // PNG raw image data: each row = filter byte 0 + RGB pixels
        var raw = new byte[height * (1 + width * 3)];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + width * 3);
            raw[row] = 0; // None filter
            Buffer.BlockCopy(rgb, y * width * 3, raw, row + 1, width * 3);
        }
        using var ms = new MemoryStream();
        // zlib header 78 9C
        ms.WriteByte(0x78);
        ms.WriteByte(0x9C);
        using (var def = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            def.Write(raw);
        var adler = Adler32(raw);
        Span<byte> a = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(a, adler);
        ms.Write(a);
        return ms.ToArray();
    }

    public static byte[] DecompressIdatToRgb24(byte[] zlibIdat, int width, int height)
    {
        using var input = new MemoryStream(zlibIdat);
        // skip zlib header 2 bytes
        input.ReadByte();
        input.ReadByte();
        using var def = new DeflateStream(input, CompressionMode.Decompress);
        using var rawMs = new MemoryStream();
        def.CopyTo(rawMs);
        var raw = rawMs.ToArray();
        var rgb = new byte[width * height * 3];
        var expectedRow = 1 + width * 3;
        for (var y = 0; y < height; y++)
        {
            var row = y * expectedRow;
            if (row >= raw.Length) break;
            // ignore filter byte raw[row]
            var copy = Math.Min(width * 3, raw.Length - row - 1);
            Buffer.BlockCopy(raw, row + 1, rgb, y * width * 3, copy);
        }
        return rgb;
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var d in data)
        {
            a = (a + d) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }
}

internal static class Crc32
{
    private static readonly uint[] Table = Create();
    private static uint[] Create()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Compute(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (var b in type) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}

