using ImageArchive;
using ImageArchive.Manifest;

namespace ImageArchive.Cli;

public static class Program
{
    public const int ExitSuccess = 0;
    public const int ExitValidation = 1;
    public const int ExitIntegrity = 2;
    public const int ExitIo = 3;
    public const int ExitInternal = 4;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return ExitValidation;
            }

            var cmd = args[0].ToLowerInvariant();
            return cmd switch
            {
                "encode" => Encode(args.Skip(1).ToArray()),
                "decode" => Decode(args.Skip(1).ToArray()),
                "init" => Init(args.Skip(1).ToArray()),
                "manifest" => Init(args.Skip(1).ToArray()), // alias
                _ => FailValidation($"Unknown command '{args[0]}'.")
            };
        }
        catch (ManifestValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitValidation;
        }
        catch (IntegrityException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitIntegrity;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitIo;
        }
        catch (ArchiveSourceException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitIo;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return ExitInternal;
        }
    }

    private static int Encode(string[] args)
    {
        var manifestPath = GetOpt(args, "--manifest") ?? throw new ManifestValidationException(
            new ManifestValidationResult { IsValid = false, Errors = new[] { new ManifestValidationError { Path = "args", Message = "--manifest required" } } });
        var json = File.ReadAllText(manifestPath);
        var validator = new JsonSchemaManifestValidator();
        var manifest = validator.Parse(json);
        var workDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        var outPath = Path.IsPathRooted(manifest.Output.Path)
            ? manifest.Output.Path
            : Path.GetFullPath(Path.Combine(workDir ?? ".", manifest.Output.Path));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        int? widthOpt = null;
        var widthStr = GetOpt(args, "--width");
        if (widthStr != null)
        {
            if (!int.TryParse(widthStr, out var w))
                throw new ManifestValidationException(new ManifestValidationResult
                {
                    IsValid = false,
                    Errors = new[] { new ManifestValidationError { Path = "args", Message = "--width must be an integer" } }
                });
            widthOpt = w;
        }

        using var output = File.Create(outPath);
        var encoder = new ImageArchiveEncoder();
        encoder.Encode(manifest, output, new ImageArchiveEncodeOptions
        {
            WorkingDirectory = workDir,
            ToolCommitUrl = Environment.GetEnvironmentVariable("IMAGEARCHIVE_TOOL_COMMIT_URL") ?? "https://github.com/sharpninja/ImageArchive",
            Dark = HasFlag(args, "--dark"),
            FrameWidth = widthOpt
        });
        return ExitSuccess;
    }

    private static int Decode(string[] args)
    {
        var input = GetOpt(args, "--input") ?? throw new ManifestValidationException(
            new ManifestValidationResult { IsValid = false, Errors = new[] { new ManifestValidationError { Path = "args", Message = "--input required" } } });
        var output = GetOpt(args, "--output") ?? throw new ManifestValidationException(
            new ManifestValidationResult { IsValid = false, Errors = new[] { new ManifestValidationError { Path = "args", Message = "--output required" } } });

        using var image = File.OpenRead(input);
        var decoder = new ImageArchiveDecoder();
        var result = decoder.Decode(image);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        using var fs = File.Create(output);
        result.ArchiveStream.CopyTo(fs);
        result.ArchiveStream.Dispose();
        return ExitSuccess;
    }

    /// <summary>
    /// Write a blank schema-valid manifest for hand-editing.
    /// Usage: imga init [--output path] [--force]
    /// </summary>
    private static int Init(string[] args)
    {
        var output = GetOpt(args, "--output")
            ?? GetOpt(args, "-o")
            ?? "manifest.json";
        var force = HasFlag(args, "--force") || HasFlag(args, "-f");

        var path = Path.GetFullPath(output);
        if (File.Exists(path) && !force)
        {
            Console.Error.WriteLine($"Refusing to overwrite existing file: {path} (use --force)");
            return ExitValidation;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = BlankManifest.ToJson();
        File.WriteAllText(path, json);

        // Ensure the blank file validates
        var validation = new JsonSchemaManifestValidator().ValidateJson(json);
        if (!validation.IsValid)
        {
            Console.Error.WriteLine("Internal error: blank manifest failed schema validation.");
            foreach (var e in validation.Errors)
                Console.Error.WriteLine($"  {e.Path}: {e.Message}");
            return ExitInternal;
        }

        Console.WriteLine(path);
        return ExitSuccess;
    }

    private static string? GetOpt(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  imga init [--output <path>] [--force]   Write a blank manifest (default: manifest.json)");
        Console.Error.WriteLine("  imga encode --manifest <path> [--dark] [--width <n>]");
        Console.Error.WriteLine("      --dark   invert header/footer chrome");
        Console.Error.WriteLine("      --width  square frame edge 512–1440 (overrides manifest frameWidth; default 1024)");
        Console.Error.WriteLine("  imga decode --input <image> --output <path>");
    }

    private static int FailValidation(string msg)
    {
        Console.Error.WriteLine(msg);
        PrintUsage();
        return ExitValidation;
    }
}
