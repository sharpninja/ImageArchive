using ImageArchive.Integrity;

namespace ImageArchive.UnitTests;

public class IntegrityTests
{
    [Fact]
    [Trait("AC", "AC-FR-INTG-001-2")]
    public void Sha256_is_lowercase_64_hex()
    {
        var hash = Sha256Hex.Compute(new byte[] { 1, 2, 3 });
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[a-f0-9]{64}$", hash);
    }

    [Fact]
    [Trait("AC", "AC-FR-INTG-001-1")]
    public void Same_bytes_same_hash()
    {
        var a = Sha256Hex.Compute("hello"u8.ToArray());
        var b = Sha256Hex.Compute("hello"u8.ToArray());
        Assert.Equal(a, b);
    }
}
