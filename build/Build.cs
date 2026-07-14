using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

/// <summary>
/// ImageArchive build: compile/test, pack CLI as a .NET tool, pack/publish library NuGet package.
/// </summary>
[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build (Debug/Release)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("NuGet API key for pushing packages (or set NUGET_API_KEY env var)")]
    [Secret]
    readonly string? NuGetApiKey;

    [Parameter("NuGet source for push (default nuget.org)")]
    readonly string NuGetSource = "https://api.nuget.org/v3/index.json";

    [Parameter("Version override for packages (optional; otherwise project Version is used)")]
    readonly string? PackageVersion;

    [Solution]
    readonly Solution Solution = default!;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";

    AbsolutePath LibraryProjectFile => RootDirectory / "src" / "ImageArchive" / "ImageArchive.csproj";
    AbsolutePath CliProjectFile => RootDirectory / "src" / "ImageArchive.Cli" / "ImageArchive.Cli.csproj";

    string? EffectiveApiKey =>
        !string.IsNullOrWhiteSpace(NuGetApiKey)
            ? NuGetApiKey
            : Environment.GetEnvironmentVariable("NUGET_API_KEY");

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(d => d.DeleteDirectory());
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(d => d.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
        });

    /// <summary>Pack the library as a multi-target NuGet package into artifacts/packages.</summary>
    Target PackLibrary => _ => _
        .DependsOn(Compile)
        .Produces(PackagesDirectory / "*.nupkg")
        .Executes(() =>
        {
            PackagesDirectory.CreateDirectory();
            DotNetPack(s => ConfigurePack(s, LibraryProjectFile));
        });

    /// <summary>Pack the CLI as a .NET tool package (PackAsTool) into artifacts/packages.</summary>
    Target PackCliTool => _ => _
        .DependsOn(Compile)
        .Produces(PackagesDirectory / "*.nupkg")
        .Executes(() =>
        {
            PackagesDirectory.CreateDirectory();
            DotNetPack(s => ConfigurePack(s, CliProjectFile));
        });

    DotNetPackSettings ConfigurePack(DotNetPackSettings s, AbsolutePath projectFile)
    {
        s = s
            .SetProject(projectFile)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .EnableNoRestore()
            .SetOutputDirectory(PackagesDirectory)
            .SetProperty("PackageOutputPath", PackagesDirectory);
        if (!string.IsNullOrWhiteSpace(PackageVersion))
        {
            s = s
                .SetVersion(PackageVersion)
                .SetProperty("Version", PackageVersion)
                .SetProperty("PackageVersion", PackageVersion);
        }
        return s;
    }

    /// <summary>Pack library + CLI tool packages.</summary>
    Target Pack => _ => _
        .DependsOn(PackLibrary, PackCliTool);

    /// <summary>Push the library NuGet package to the configured source.</summary>
    Target PublishLibrary => _ => _
        .DependsOn(PackLibrary)
        .Requires(() => !string.IsNullOrWhiteSpace(EffectiveApiKey))
        .Executes(() =>
        {
            var packages = PackagesDirectory.GlobFiles("ImageArchive.*.nupkg")
                .Where(p => !p.Name.Contains(".Cli.", StringComparison.OrdinalIgnoreCase)
                            && !p.Name.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Prefer the main library package id ImageArchive (not tool)
            packages = packages
                .Where(p => p.Name.StartsWith("ImageArchive.", StringComparison.Ordinal)
                            && !p.Name.StartsWith("ImageArchive.Cli", StringComparison.Ordinal)
                            && !p.Name.StartsWith("imagearchive-cli", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (packages.Count == 0)
                throw new InvalidOperationException($"No library packages found in {PackagesDirectory}");

            foreach (var package in packages)
            {
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(EffectiveApiKey)
                    .EnableSkipDuplicate());
            }
        });

    /// <summary>Push the CLI .NET tool package to the configured source (optional companion target).</summary>
    Target PublishCliTool => _ => _
        .DependsOn(PackCliTool)
        .Requires(() => !string.IsNullOrWhiteSpace(EffectiveApiKey))
        .Executes(() =>
        {
            var packages = PackagesDirectory.GlobFiles("*.nupkg")
                .Where(p => p.Name.Contains("ImageArchive.Cli", StringComparison.OrdinalIgnoreCase)
                            || p.Name.StartsWith("imagearchive-cli", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.Name.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (packages.Count == 0)
                throw new InvalidOperationException($"No CLI tool packages found in {PackagesDirectory}");

            foreach (var package in packages)
            {
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(EffectiveApiKey)
                    .EnableSkipDuplicate());
            }
        });

    /// <summary>Full CI path: clean, test, pack library + CLI tool.</summary>
    Target Ci => _ => _
        .DependsOn(Clean, Test, Pack);
}
