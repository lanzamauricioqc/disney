using Disney.Domain;

namespace Disney.Application;

public static class CompanyClaimNames
{
    public const string OrganizationId = "organization_id";
}

public sealed class CompanyUser
{
    public CompanyUser()
    {
    }

    public CompanyUser(
        Guid id,
        Guid organizationId,
        string email,
        CompanyRole role,
        bool isActive,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        Email = email;
        Role = role;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public CompanyRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CompanyAuthenticationUser
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public CompanyRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record CompanyAccessToken(
    string AccessToken,
    DateTimeOffset ExpiresAt);

public sealed record CompanyAuthenticationResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    CompanyUser User);

public enum CompanyAuthenticationOutcome
{
    Success,
    InvalidRequest,
    InvalidCredentials,
    AlreadyBootstrapped,
    Unavailable,
    InvalidOrExpiredInvitation,
    EmailAlreadyExists
}

public sealed record CompanyAuthenticationResult(
    CompanyAuthenticationOutcome Outcome,
    CompanyAuthenticationResponse? Authentication = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    string? Message = null);

public sealed class OrganizationDetails
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? WelcomeMessage { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record OrganizationUpdate(
    string Name,
    string? SupportEmail,
    string? SupportPhone);

public sealed record BrandingUpdate(
    string DisplayName,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? WelcomeMessage,
    string? SupportEmail,
    string? SupportPhone);

public sealed class CompanyInvitation
{
    public CompanyInvitation()
    {
    }

    public CompanyInvitation(
        Guid id,
        string email,
        CompanyRole role,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        DateTimeOffset? acceptedAt,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        Email = email;
        Role = role;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        AcceptedAt = acceptedAt;
        RevokedAt = revokedAt;
    }

    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public CompanyRole Role { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed record CreatedCompanyInvitation(
    CompanyInvitation Invitation,
    string Token,
    Guid NotificationId);

public sealed class CompanyCustomer
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record CompanyCustomerInput(
    string Name,
    string? Email,
    string? Phone,
    string? ExternalReference,
    string? Notes);

public sealed class CompanyVisit
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ParkName { get; set; } = string.Empty;
    public DateOnly VisitDate { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public CompanyVisitStatus Status { get; set; }
    public int PartySize { get; set; }
    public string? Instructions { get; set; }
    public string? MeetingPoint { get; set; }
    public string? TransportationDetails { get; set; }
    public int CompletedItemCount { get; set; }
    public int TotalItemCount { get; set; }
    public string? ExternalReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record CompanyVisitInput(
    Guid CustomerId,
    string ParkName,
    DateOnly VisitDate,
    string TimeZone,
    CompanyVisitStatus Status,
    int PartySize,
    string? Instructions,
    string? MeetingPoint,
    string? TransportationDetails,
    int CompletedItemCount,
    int TotalItemCount,
    string? ExternalReference);

public sealed record VisitProgressUpdate(
    CompanyVisitStatus Status,
    int CompletedItemCount,
    int TotalItemCount);

public sealed class VisitNote
{
    public Guid Id { get; set; }
    public Guid VisitId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class VisitOverride
{
    public Guid Id { get; set; }
    public Guid VisitId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class VisitorAccessLink
{
    public Guid Id { get; set; }
    public Guid VisitId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed record CreatedVisitorAccessLink(
    VisitorAccessLink Link,
    string Token,
    Guid? NotificationId);

public sealed class VisitEntitlement
{
    public Guid Id { get; set; }
    public Guid VisitId { get; set; }
    public long CreditLedgerEntryId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
}

public sealed class VisitorVisit
{
    public Guid VisitId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ParkName { get; set; } = string.Empty;
    public DateOnly VisitDate { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public CompanyVisitStatus Status { get; set; }
    public int PartySize { get; set; }
    public string? Instructions { get; set; }
    public string? MeetingPoint { get; set; }
    public string? TransportationDetails { get; set; }
    public int CompletedItemCount { get; set; }
    public int TotalItemCount { get; set; }
    public DateTimeOffset AccessExpiresAt { get; set; }
}

public sealed class CreditLedgerEntry
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record CreditBalance(
    int Remaining,
    int Consumed);

public sealed record CompanyCheckoutSession(
    string ProviderSessionId,
    string CheckoutUrl,
    string BundleCode,
    int Credits);

public sealed record CompanyCreditBundle(
    string Code,
    string Name,
    int Credits,
    string? DisplayPrice);

public sealed class CompanyApiKey
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed record CreatedCompanyApiKey(
    CompanyApiKey ApiKey,
    string Secret);

public sealed record ReservationImport(
    string CustomerExternalReference,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CustomerNotes,
    string ReservationExternalReference,
    string ParkName,
    DateOnly VisitDate,
    string TimeZone,
    int PartySize,
    string? Instructions,
    string? MeetingPoint,
    string? TransportationDetails);

public sealed record ReservationImportResult(
    Guid CustomerId,
    Guid VisitId,
    bool CustomerCreated,
    bool VisitCreated);

public sealed class CompanyDashboard
{
    public int Customers { get; set; }
    public int UpcomingVisits { get; set; }
    public int ActiveVisits { get; set; }
    public int CompletedVisits { get; set; }
    public int CreditsConsumed { get; set; }
    public int CreditsRemaining { get; set; }
}

public sealed class MonthlyUsage
{
    public DateOnly Month { get; set; }
    public int CreditsConsumed { get; set; }
    public int VisitsCreated { get; set; }
}

public sealed class NotificationOutboxItem
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public NotificationDeliveryStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}

public sealed record NotificationRequest(
    Guid OrganizationId,
    string Channel,
    string Recipient,
    string Template,
    string PayloadJson);

public sealed record CompanyActor(
    Guid OrganizationId,
    Guid UserId,
    CompanyRole Role);

public sealed record CompanyPaymentEvent(
    string ProviderEventId,
    string ProviderSessionId,
    string EventType,
    long? AmountPaid,
    string? Currency,
    DateTimeOffset OccurredAt);
