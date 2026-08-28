using System.ComponentModel;
using System.Globalization;

namespace ApricotFramework.Mailer.Options;

/// <summary>
/// Lets a mailbox be declared as a single string as well as an object.
/// </summary>
/// <remarks>
/// Without this, <c>"DefaultFrom": "no-reply@example.com"</c> binds to nothing at all, and the
/// configuration binder reports no error — the address an operator set is simply not there. Both
/// <c>someone@example.com</c> and <c>Example &lt;someone@example.com&gt;</c> are accepted.
/// </remarks>
public sealed class MailAddressEntryConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text)
        {
            return base.ConvertFrom(context, culture, value);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var open = text.LastIndexOf('<');
        var close = text.LastIndexOf('>');

        if (open >= 0 && close > open)
        {
            return new MailAddressEntry
            {
                Address = text[(open + 1)..close].Trim(),
                Name = text[..open].Trim().Trim('"'),
            };
        }

        return new MailAddressEntry { Address = text.Trim() };
    }
}
