namespace ImageArchive.Abstractions;

public enum PixelFormat
{
    Rgb24 = 0,
    Rgba32 = 1
}

public enum ContainerFormat
{
    Png = 0,
    Webp = 1
}

/// <summary>Archive payload types. <see cref="Raw"/> is the default (0).</summary>
public enum ArchiveType
{
    Raw = 0,
    Git = 1,
    Zip = 2,
    Tar = 3
}

public enum HeaderContentType
{
    Text = 0,
    Image = 1,
    Folder = 2
}
