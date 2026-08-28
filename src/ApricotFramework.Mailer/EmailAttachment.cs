namespace ApricotFramework.Mailer;

/// <summary>
/// A file traveling with a message, either attached or referenced from the HTML body.
/// </summary>
/// <remarks>
/// The payload arrives through <see cref="OpenRead"/> rather than as bytes, so a large file need not
/// be held in memory and one attachment can be sent more than once.
/// </remarks>
public sealed record EmailAttachment
{
    /// <summary>
    /// The content type used when a factory is not told one.
    /// </summary>
    public const string DefaultContentType = "application/octet-stream";

    private EmailAttachment(string fileName, string contentType, string? contentId, bool isInline, Func<Stream> openRead)
    {
        this.FileName = fileName;
        this.ContentType = contentType;
        this.ContentId = contentId;
        this.IsInline = isInline;
        this.OpenRead = openRead;
    }

    /// <summary>
    /// Gets the file name the recipient sees.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the MIME content type, such as <c>image/png</c>.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the content id an HTML body references with <c>cid:</c>, or null when there is none.
    /// </summary>
    public string? ContentId { get; }

    /// <summary>
    /// Gets whether this is part of the body rather than an attachment listed on its own.
    /// </summary>
    public bool IsInline { get; }

    /// <summary>
    /// Gets the function that opens the payload.
    /// </summary>
    /// <remarks>
    /// Must return a fresh, readable stream on every call, and may be called more than once for a
    /// single sending — measuring an attachment against a size limit opens it separately from writing
    /// it. Whoever calls it disposes of what it returns, so it must not hand out a stream it needs
    /// afterward.
    /// </remarks>
    public Func<Stream> OpenRead { get; }

    /// <summary>
    /// Creates an attachment over bytes already in memory.
    /// </summary>
    /// <param name="fileName">The file name the recipient sees.</param>
    /// <param name="content">The payload, copied so later changes to the caller's buffer do not show up.</param>
    /// <param name="contentType">The MIME content type, or null for <see cref="DefaultContentType"/>.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="fileName"/> is null, blank, or contains a control character.
    /// </exception>
    public static EmailAttachment FromBytes(string fileName, ReadOnlyMemory<byte> content, string? contentType = null)
    {
        var payload = content.ToArray();

        return Create(fileName, contentType, null, false, () => new MemoryStream(payload, 0, payload.Length, writable: false, publiclyVisible: false));
    }

    /// <summary>
    /// Creates an attachment read from a file on each sending.
    /// </summary>
    /// <param name="path">The path to read.</param>
    /// <param name="fileName">The file name the recipient sees, or null to use the one in the path.</param>
    /// <param name="contentType">The MIME content type, or null for <see cref="DefaultContentType"/>.</param>
    /// <returns>The attachment.</returns>
    /// <remarks>
    /// The file is opened when the message is sent, not here, so a path that disappears in between
    /// surfaces as a failed sending rather than a failure at construction.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is null or blank, or the resolved file name is unusable.
    /// </exception>
    public static EmailAttachment FromFile(string path, string? fileName = null, string? contentType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Create(fileName ?? Path.GetFileName(path), contentType, null, false, () => File.OpenRead(path));
    }

    /// <summary>
    /// Creates an inline resource an HTML body references as <c>cid:{contentId}</c>.
    /// </summary>
    /// <param name="contentId">The content id, without the <c>cid:</c> prefix and without angle brackets.</param>
    /// <param name="fileName">The file name, which a client may show if it cannot render the part.</param>
    /// <param name="content">The payload, copied.</param>
    /// <param name="contentType">The MIME content type, or null for <see cref="DefaultContentType"/>.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the content id or the file name is null, blank, or contains a control character.
    /// </exception>
    public static EmailAttachment Inline(string contentId, string fileName, ReadOnlyMemory<byte> content, string? contentType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);

        var payload = content.ToArray();

        return Create(fileName, contentType, contentId, true, () => new MemoryStream(payload, 0, payload.Length, writable: false, publiclyVisible: false));
    }

    /// <summary>
    /// Creates an inline resource read from a file on each sending.
    /// </summary>
    /// <param name="contentId">The content id, without the <c>cid:</c> prefix and without angle brackets.</param>
    /// <param name="path">The path to read.</param>
    /// <param name="contentType">The MIME content type, or null for <see cref="DefaultContentType"/>.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the content id or the path is null or blank, or the resolved file name is unusable.
    /// </exception>
    public static EmailAttachment InlineFromFile(string contentId, string path, string? contentType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Create(Path.GetFileName(path), contentType, contentId, true, () => File.OpenRead(path));
    }

    /// <summary>
    /// Validates the parts every factory shares and builds the attachment.
    /// </summary>
    /// <param name="fileName">The file name the recipient sees.</param>
    /// <param name="contentType">The MIME content type, or null for the default.</param>
    /// <param name="contentId">The content id, or null.</param>
    /// <param name="isInline">Whether the part belongs to the body.</param>
    /// <param name="openRead">Opens the payload.</param>
    /// <returns>The attachment.</returns>
    private static EmailAttachment Create(string fileName, string? contentType, string? contentId, bool isInline, Func<Stream> openRead)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // both reach a header parameter, so a control character in either is a header-injection attempt
        if (fileName.Any(char.IsControl))
        {
            throw new ArgumentException("An attachment file name may not contain a control character.", nameof(fileName));
        }

        if (contentId is not null && (contentId.Any(char.IsControl) || contentId.Any(char.IsWhiteSpace)))
        {
            throw new ArgumentException("A content id may not contain white space or a control character.", nameof(contentId));
        }

        var resolved = string.IsNullOrWhiteSpace(contentType) ? DefaultContentType : contentType.Trim();

        if (resolved.Any(char.IsControl))
        {
            throw new ArgumentException("A content type may not contain a control character.", nameof(contentType));
        }

        return new EmailAttachment(fileName.Trim(), resolved, contentId?.Trim(), isInline, openRead);
    }
}
