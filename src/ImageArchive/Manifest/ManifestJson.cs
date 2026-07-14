using System.Text.Json;
using System.Text.Json.Serialization;
using ImageArchive.Abstractions;

namespace ImageArchive.Manifest;

public static class ManifestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new ContainerFormatConverter()
        }
    };

    public static ImageArchiveManifest Deserialize(string json) =>
        JsonSerializer.Deserialize<ImageArchiveManifest>(json, Options)
        ?? throw new ImageArchiveException("Manifest JSON deserialized to null.");

    public static string Serialize(ImageArchiveManifest manifest) =>
        JsonSerializer.Serialize(manifest, Options);
}

/// <summary>Maps schema png/webp strings to <see cref="ContainerFormat"/>.</summary>
internal sealed class ContainerFormatConverter : JsonConverter<ContainerFormat>
{
    public override ContainerFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString()?.ToLowerInvariant();
        return s switch
        {
            "png" => ContainerFormat.Png,
            "webp" => ContainerFormat.Webp,
            _ => throw new JsonException($"Unknown container format '{s}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ContainerFormat value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value == ContainerFormat.Webp ? "webp" : "png");
    }
}
