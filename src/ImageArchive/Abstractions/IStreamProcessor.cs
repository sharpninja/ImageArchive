namespace ImageArchive.Abstractions;

public interface IStreamProcessor
{
    Stream Apply(Stream input, string? externalCommand);
}
