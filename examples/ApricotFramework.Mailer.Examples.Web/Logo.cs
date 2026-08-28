namespace ApricotFramework.Mailer.Examples.Web;

/// <summary>
/// A tiny image, so the inline-attachment case is real without shipping a binary asset.
/// </summary>
public static class Logo
{
    private const string Base64Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAIAQMAAAD+wSzIAAAABlBMVEX///+/v7+jQ3Y5AAAADklEQVQI12P4AIX8EAgALgAD/aNpbtEAAAAASUVORK5CYII=";

    /// <summary>
    /// Gets the image bytes.
    /// </summary>
    public static ReadOnlyMemory<byte> Bytes { get; } = Convert.FromBase64String(Base64Png);
}
