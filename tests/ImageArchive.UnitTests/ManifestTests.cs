using ImageArchive.Abstractions;
using ImageArchive.Manifest;

namespace ImageArchive.UnitTests;

public class ManifestTests
{
    [Fact]
    [Trait("AC", "AC-FR-MANF-001-3")]
    [Trait("AC", "AC-FR-MANF-001-5")]
    public void Valid_manifest_passes()
    {
        var json = """
        {
          "version": "1.0.0",
          "encoder": { "name": "t", "version": "1", "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" },
          "archive": { "type": "raw", "mimeType": "application/octet-stream", "source": "a.bin" },
          "output": { "path": "out.iar", "format": "png" },
          "frames": [ {} ]
        }
        """;
        var v = new JsonSchemaManifestValidator();
        var r = v.ValidateJson(json);
        Assert.True(r.IsValid, string.Join("; ", r.Errors.Select(e => e.Message)));
        var m = v.Parse(json);
        Assert.Equal(ArchiveType.Raw, m.Archive.Type);
        Assert.False(m.Dark);
    }

    [Fact]
    public void Dark_boolean_round_trips_in_schema_and_model()
    {
        var json = """
        {
          "version": "1.0.0",
          "encoder": { "name": "t", "version": "1", "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" },
          "archive": { "type": "raw", "mimeType": "application/octet-stream", "source": "a.bin" },
          "output": { "path": "out.iar", "format": "png" },
          "dark": true,
          "frames": [ {} ]
        }
        """;
        var v = new JsonSchemaManifestValidator();
        var r = v.ValidateJson(json);
        Assert.True(r.IsValid, string.Join("; ", r.Errors.Select(e => e.Message)));
        var m = v.Parse(json);
        Assert.True(m.Dark);
        var again = ManifestJson.Deserialize(ManifestJson.Serialize(m));
        Assert.True(again.Dark);
    }

    [Fact]
    [Trait("AC", "AC-FR-MANF-001-2")]
    public void Missing_required_fails()
    {
        var json = """{ "version": "1.0.0" }""";
        var v = new JsonSchemaManifestValidator();
        var r = v.ValidateJson(json);
        Assert.False(r.IsValid);
    }

    [Fact]
    [Trait("AC", "AC-FR-MANF-001-3")]
    public void Wrong_version_fails()
    {
        var m = new ImageArchiveManifest
        {
            Version = "2.0.0",
            Encoder = new EncoderManifestSection { Name = "t", Version = "1", Sha256 = new string('a', 64) },
            Archive = new ArchiveManifestSection { Type = ArchiveType.Raw, MimeType = "x", Source = "a" },
            Output = new OutputManifestSection { Path = "o", Format = ContainerFormat.Png },
            Frames = new List<FrameManifestSection> { new() }
        };
        var r = new JsonSchemaManifestValidator().Validate(m);
        Assert.False(r.IsValid);
    }
}
