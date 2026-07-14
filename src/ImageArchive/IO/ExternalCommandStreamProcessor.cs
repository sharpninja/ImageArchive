using System.Diagnostics;
using ImageArchive.Abstractions;

namespace ImageArchive.IO;

public sealed class ExternalCommandStreamProcessor : IStreamProcessor
{
    public Stream Apply(Stream input, string? externalCommand)
    {
        if (string.IsNullOrWhiteSpace(externalCommand))
        {
            if (input.CanSeek) input.Position = 0;
            var copy = new MemoryStream();
            input.CopyTo(copy);
            copy.Position = 0;
            return copy;
        }

        // Simple: command is executable path; stdin/stdout piping
        var psi = new ProcessStartInfo
        {
            FileName = externalCommand,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi) ?? throw new StreamProcessorException("Failed to start processor.");
        if (input.CanSeek) input.Position = 0;
        input.CopyTo(proc.StandardInput.BaseStream);
        proc.StandardInput.Close();
        var output = new MemoryStream();
        proc.StandardOutput.BaseStream.CopyTo(output);
        proc.WaitForExit(60_000);
        if (proc.ExitCode != 0)
            throw new StreamProcessorException($"Processor exited {proc.ExitCode}: {proc.StandardError.ReadToEnd()}");
        output.Position = 0;
        return output;
    }
}
