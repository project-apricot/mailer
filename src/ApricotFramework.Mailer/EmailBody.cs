namespace ApricotFramework.Mailer;

/// <summary>
/// What a message says, as HTML, as plain text, or as both.
/// </summary>
/// <remarks>
/// A null part is absent rather than empty: transport emits only the parts that exist, so an
/// HTML-only body is a single HTML part and never an alternative with an empty half.
/// </remarks>
public sealed record EmailBody
{
    private EmailBody(string? html, string? text)
    {
        this.Html = html;
        this.Text = text;
    }

    /// <summary>
    /// Gets the HTML body or null when the message has none.
    /// </summary>
    public string? Html { get; }

    /// <summary>
    /// Gets the plain-text body or null when the message has none.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Creates an HTML-only body.
    /// </summary>
    /// <param name="html">The HTML markup.</param>
    /// <returns>The body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is null.</exception>
    public static EmailBody FromHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        return new EmailBody(html, null);
    }

    /// <summary>
    /// Creates a plain-text-only body.
    /// </summary>
    /// <param name="text">The plain text.</param>
    /// <returns>The body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static EmailBody FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new EmailBody(null, text);
    }

    /// <summary>
    /// Creates a body carrying both representations for a client to choose between.
    /// </summary>
    /// <param name="html">The HTML markup.</param>
    /// <param name="text">The plain-text alternative.</param>
    /// <returns>The body.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="html"/> or <paramref name="text"/> is null.
    /// </exception>
    public static EmailBody FromBoth(string html, string text)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(text);

        return new EmailBody(html, text);
    }

    /// <summary>
    /// Decides whether this body would produce no content at all.
    /// </summary>
    /// <returns>True when neither representation is present.</returns>
    public bool IsEmpty()
    {
        return this.Html is null && this.Text is null;
    }
}
