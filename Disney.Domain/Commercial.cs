using System.Net.Mail;

namespace Disney.Domain;

public sealed record EmailAddress
{
    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? candidate, out EmailAddress? emailAddress)
    {
        emailAddress = null;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var normalized = candidate.Trim().ToLowerInvariant();
        if (normalized.Length > 320)
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(normalized);
            if (!string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        catch (FormatException)
        {
            return false;
        }

        emailAddress = new EmailAddress(normalized);
        return true;
    }

    public override string ToString() => Value;
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Expired
}
