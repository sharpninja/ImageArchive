using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ImageArchive.Abstractions;
using Json.Schema;

namespace ImageArchive.Manifest;

public interface IManifestValidator
{
    ManifestValidationResult Validate(ImageArchiveManifest manifest);
    ManifestValidationResult ValidateJson(string json);
    ImageArchiveManifest Parse(string json);
}

public sealed class JsonSchemaManifestValidator : IManifestValidator
{
    private static readonly Lazy<JsonSchema> Schema = new(LoadSchema);
    private static readonly Regex Hex64 = new("^[a-fA-F0-9]{64}$", RegexOptions.Compiled);

    private static JsonSchema LoadSchema()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("imagearchive-schema.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded schema not found.");
        using var s = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(s);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    public ImageArchiveManifest Parse(string json)
    {
        var result = ValidateJson(json);
        if (!result.IsValid)
            throw new ManifestValidationException(result);
        return ManifestJson.Deserialize(json);
    }

    public ManifestValidationResult ValidateJson(string json)
    {
        var errors = new List<ManifestValidationError>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var eval = Schema.Value.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!eval.IsValid)
            {
                foreach (var detail in eval.Details.Where(d => d.HasErrors))
                {
                    var msg = detail.Errors != null ? string.Join("; ", detail.Errors.Select(e => e.Value)) : "invalid";
                    errors.Add(new ManifestValidationError { Path = detail.InstanceLocation.ToString(), Message = msg });
                }
                if (errors.Count == 0)
                    errors.Add(new ManifestValidationError { Path = "$", Message = "Schema validation failed." });
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ManifestValidationError { Path = "$", Message = ex.Message });
        }

        // Extra checks in case schema eval is loose on enums after deserialization
        try
        {
            var m = ManifestJson.Deserialize(json);
            errors.AddRange(ValidateModel(m).Errors);
        }
        catch (Exception ex)
        {
            errors.Add(new ManifestValidationError { Path = "$", Message = "Deserialize: " + ex.Message });
        }

        // Dedupe by path+message
        errors = errors
            .GroupBy(e => e.Path + "|" + e.Message)
            .Select(g => g.First())
            .ToList();

        return new ManifestValidationResult { IsValid = errors.Count == 0, Errors = errors };
    }

    public ManifestValidationResult Validate(ImageArchiveManifest manifest) => ValidateModel(manifest);

    private static ManifestValidationResult ValidateModel(ImageArchiveManifest m)
    {
        var errors = new List<ManifestValidationError>();
        if (m.Version != "1.0.0")
            errors.Add(new ManifestValidationError { Path = "version", Message = "version must be 1.0.0" });
        if (string.IsNullOrWhiteSpace(m.Encoder?.Name))
            errors.Add(new ManifestValidationError { Path = "encoder.name", Message = "required" });
        if (string.IsNullOrWhiteSpace(m.Encoder?.Version))
            errors.Add(new ManifestValidationError { Path = "encoder.version", Message = "required" });
        if (m.Encoder == null || !Hex64.IsMatch(m.Encoder.Sha256 ?? ""))
            errors.Add(new ManifestValidationError { Path = "encoder.sha256", Message = "must be 64 hex chars" });
        if (string.IsNullOrWhiteSpace(m.Archive?.Source))
            errors.Add(new ManifestValidationError { Path = "archive.source", Message = "required" });
        if (string.IsNullOrWhiteSpace(m.Archive?.MimeType))
            errors.Add(new ManifestValidationError { Path = "archive.mimeType", Message = "required" });
        if (string.IsNullOrWhiteSpace(m.Output?.Path))
            errors.Add(new ManifestValidationError { Path = "output.path", Message = "required" });
        if (m.Frames == null || m.Frames.Count < 1)
            errors.Add(new ManifestValidationError { Path = "frames", Message = "minItems is 1" });
        if (!string.IsNullOrEmpty(m.StreamSha256) && !Hex64.IsMatch(m.StreamSha256))
            errors.Add(new ManifestValidationError { Path = "streamSha256", Message = "must be 64 hex chars" });
        return new ManifestValidationResult { IsValid = errors.Count == 0, Errors = errors };
    }
}
