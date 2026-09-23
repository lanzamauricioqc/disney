namespace Disney.Domain;

public enum CompanyRole
{
    Owner,
    Administrator,
    Planner,
    Support
}

public enum CompanyVisitStatus
{
    Planned,
    Active,
    Completed,
    Cancelled
}

public enum NotificationDeliveryStatus
{
    Queued,
    Processing,
    Delivered,
    Failed
}

public static class CompanyRoleRules
{
    public static bool CanManageTeam(this CompanyRole role) =>
        role is CompanyRole.Owner or CompanyRole.Administrator;

    public static bool CanManageBilling(this CompanyRole role) =>
        role is CompanyRole.Owner or CompanyRole.Administrator;

    public static bool CanManageConfiguration(this CompanyRole role) =>
        role is CompanyRole.Owner or CompanyRole.Administrator;

    public static bool CanAssign(this CompanyRole actorRole, CompanyRole targetRole) =>
        actorRole == CompanyRole.Owner ||
        actorRole == CompanyRole.Administrator && targetRole != CompanyRole.Owner;
}

public static class CompanyPasswordRules
{
    public const int MinimumLength = 12;

    public static bool IsValid(string? password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length >= MinimumLength &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(char.IsDigit) &&
        password.Any(character => !char.IsLetterOrDigit(character));
}

public static class CompanyColorRules
{
    public static bool IsValid(string? color) =>
        string.IsNullOrWhiteSpace(color) ||
        color.Length == 7 &&
        color[0] == '#' &&
        color.Skip(1).All(Uri.IsHexDigit);
}

public static class CompanyApiKeyRules
{
    public const int PrefixLength = 12;

    public static string GetPrefix(string secret) =>
        secret[..Math.Min(secret.Length, PrefixLength)];
}
