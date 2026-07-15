using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

/// <summary>
/// ImageArchive build: compile/test, pack CLI as a .NET tool, pack/publish library NuGet package.
/// Versioning via GitVersion (see GitVersion.yml; release tags v0.5.0+).
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

    [Parameter("Version override for packages (optional; otherwise GitVersion is used)")]
    readonly string? PackageVersion;

    [Solution]
    readonly Solution Solution = default!;

    [GitVersion(NoFetch = true, NoCache = false)]
    readonly GitVersion GitVersion = default!;

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

    /// <summary>NuGet package version: parameter override, else GitVersion SemVer / NuGet fields.</summary>
    string EffectivePackageVersion =>
        !string.IsNullOrWhiteSpace(PackageVersion)
            ? PackageVersion!
            : FirstNonEmpty(
                GitVersion?.SemVer,
                GitVersion?.NuGetVersionV2,
                GitVersion?.NuGetVersion,
                GitVersion?.MajorMinorPatch)
            ?? "0.0.0";

    static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

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
            // Solution is product-only (build/_build.csproj is intentionally not listed).
            DotNetRestore(s => s.SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            Serilog.Log.Information("GitVersion: SemVer={SemVer} NuGet={NuGet} Full={Full}",
                GitVersion?.SemVer, EffectivePackageVersion, GitVersion?.FullSemVer);
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetVersion(EffectivePackageVersion)
                .SetAssemblyVersion(GitVersion?.AssemblySemVer)
                .SetFileVersion(GitVersion?.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion?.InformationalVersion));
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
            // PackAsTool re-publishes into bin; allow pack to rebuild publish output so
            // DeletePublishedPdbs (strips huge SkiaSharp .pdb) always runs.
            DotNetPack(s => ConfigurePack(s, CliProjectFile).DisableNoBuild());
        });

    DotNetPackSettings ConfigurePack(DotNetPackSettings s, AbsolutePath projectFile)
    {
        // DisableGitVersionTask: GitVersion.MsBuild otherwise rewrites PackageVersion late and
        // ignores -p:PackageVersion (e.g. publish of 0.5.1 while tag is still v0.5.0).
        return s
            .SetProject(projectFile)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .EnableNoRestore()
            .SetOutputDirectory(PackagesDirectory)
            .SetProperty("PackageOutputPath", PackagesDirectory)
            .SetVersion(EffectivePackageVersion)
            .SetProperty("Version", EffectivePackageVersion)
            .SetProperty("PackageVersion", EffectivePackageVersion)
            .SetProperty("DisableGitVersionTask", "true")
            .SetAssemblyVersion(GitVersion?.AssemblySemVer ?? EffectivePackageVersion + ".0")
            .SetFileVersion(GitVersion?.AssemblySemFileVer ?? EffectivePackageVersion + ".0")
            .SetInformationalVersion(GitVersion?.InformationalVersion ?? EffectivePackageVersion);
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
                .Where(p => p.Name.StartsWith("ImageArchive.", StringComparison.Ordinal)
                            && !p.Name.StartsWith("ImageArchive.Cli", StringComparison.Ordinal)
                            && !p.Name.StartsWith("imagearchive-cli", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (packages.Count == 0)
                throw new InvalidOperationException($"No library packages found in {PackagesDirectory}");

            foreach (var package in packages)
            {
                Serilog.Log.Information("Pushing library package {Package}", package.Name);
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(EffectiveApiKey)
                    .EnableSkipDuplicate());
            }
        });

    /// <summary>Push the CLI .NET tool package to the configured source.</summary>
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
                Serilog.Log.Information("Pushing CLI tool package {Package}", package.Name);
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(EffectiveApiKey)
                    .EnableSkipDuplicate());
            }
        });

    /// <summary>Pack and push library + CLI tool packages.</summary>
    Target Publish => _ => _
        .DependsOn(PublishLibrary, PublishCliTool);

    /// <summary>Full CI path: clean, test, pack library + CLI tool.</summary>
    Target Ci => _ => _
        .DependsOn(Clean, Test, Pack);
}
