using Disney.Domain;

namespace Disney.Application;

public interface ICompanyPasswordService
{
    string Hash(string password);
    bool Verify(string? passwordHash, string password);
}

public interface ICompanyTokenService
{
    bool IsConfigured { get; }
    CompanyAccessToken Issue(CompanyUser user);
}

public interface ICompanySecretService
{
    string Generate(int bytes = 32);
    string Hash(string secret);
    string GetPrefix(string secret);
}

public interface ICompanyNotificationOutbox
{
    Task<Guid> QueueAsync(
        NotificationRequest request,
        DateTimeOffset queuedAt,
        CancellationToken cancellationToken);
}

public interface ICompanyBillingGateway
{
    IReadOnlyList<CompanyCreditBundle> GetCreditBundles();

    Task<CompanyCheckoutSession> CreateCheckoutSessionAsync(
        Guid organizationId,
        string email,
        string bundleCode,
        CancellationToken cancellationToken);
}

public interface ICompanyAuthenticationRepository
{
    Task<CompanyUser?> BootstrapAsync(
        string organizationName,
        string normalizedEmail,
        string passwordHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<CompanyAuthenticationUser?> FindAuthenticationUserAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<CompanyUser?> AcceptInvitationAsync(
        string tokenHash,
        string passwordHash,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken);
}

public interface ICompanyOrganizationRepository
{
    Task<OrganizationDetails?> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<OrganizationDetails?> UpdateOrganizationAsync(
        CompanyActor actor,
        OrganizationUpdate update,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task<OrganizationDetails?> UpdateBrandingAsync(
        CompanyActor actor,
        BrandingUpdate update,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}

public interface ICompanyTeamRepository
{
    Task<CompanyInvitation?> CreateInvitationAsync(
        CompanyActor actor,
        Guid invitationId,
        string normalizedEmail,
        CompanyRole role,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CompanyUser>> ListTeamAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CompanyInvitation>> ListInvitationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<bool> ChangeRoleAsync(
        CompanyActor actor,
        Guid userId,
        CompanyRole role,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken);

    Task<bool> DeactivateUserAsync(
        CompanyActor actor,
        Guid userId,
        DateTimeOffset deactivatedAt,
        CancellationToken cancellationToken);
}

public interface ICompanyCustomerRepository
{
    Task<IReadOnlyList<CompanyCustomer>> SearchCustomersAsync(
        Guid organizationId,
        string? search,
        int limit,
        CancellationToken cancellationToken);

    Task<CompanyCustomer?> GetCustomerAsync(
        Guid organizationId,
        Guid customerId,
        CancellationToken cancellationToken);

    Task<CompanyCustomer> CreateCustomerAsync(
        CompanyActor actor,
        Guid customerId,
        CompanyCustomerInput input,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<CompanyCustomer?> UpdateCustomerAsync(
        CompanyActor actor,
        Guid customerId,
        CompanyCustomerInput input,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task<bool> DeleteCustomerAsync(
        CompanyActor actor,
        Guid customerId,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken);
}

public interface ICompanyVisitRepository
{
    Task<IReadOnlyList<CompanyVisit>> ListVisitsAsync(
        Guid organizationId,
        DateOnly? from,
        DateOnly? to,
        CompanyVisitStatus? status,
        Guid? customerId,
        int limit,
        CancellationToken cancellationToken);

    Task<CompanyVisit?> GetVisitAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken);

    Task<CompanyVisit?> CreateVisitAsync(
        CompanyActor actor,
        Guid visitId,
        CompanyVisitInput input,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<CompanyVisit?> UpdateVisitAsync(
        CompanyActor actor,
        Guid visitId,
        CompanyVisitInput input,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task<bool> DeleteVisitAsync(
        CompanyActor actor,
        Guid visitId,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken);

    Task<CompanyVisit?> UpdateVisitProgressAsync(
        CompanyActor actor,
        Guid visitId,
        VisitProgressUpdate update,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task<VisitNote?> AddVisitNoteAsync(
        CompanyActor actor,
        Guid visitId,
        Guid noteId,
        string note,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VisitNote>> ListVisitNotesAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken);

    Task<VisitOverride?> AddVisitOverrideAsync(
        CompanyActor actor,
        Guid visitId,
        Guid overrideId,
        string summary,
        string detailsJson,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VisitOverride>> ListVisitOverridesAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken);

    Task<VisitEntitlement?> GetVisitEntitlementAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken);

    Task<VisitEntitlement?> AssignVisitEntitlementAsync(
        CompanyActor actor,
        Guid visitId,
        Guid entitlementId,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken);

    Task<VisitorAccessLink?> CreateVisitorAccessLinkAsync(
        CompanyActor actor,
        Guid visitId,
        Guid accessLinkId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<bool> RevokeVisitorAccessLinksAsync(
        CompanyActor actor,
        Guid visitId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken);

    Task<VisitorVisit?> ResolveVisitorAccessAsync(
        string tokenHash,
        DateTimeOffset accessedAt,
        CancellationToken cancellationToken);
}

public interface ICompanyBillingRepository
{
    Task<CreditBalance> GetCreditBalanceAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CreditLedgerEntry>> ListCreditLedgerAsync(
        Guid organizationId,
        int limit,
        CancellationToken cancellationToken);

    Task<CreditLedgerEntry> AddCreditAdjustmentAsync(
        CompanyActor actor,
        int amount,
        string reason,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task SaveCompanyCheckoutSessionAsync(
        Guid organizationId,
        CompanyCheckoutSession session,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<bool> ApplyCompanyPaymentAsync(
        CompanyPaymentEvent paymentEvent,
        CancellationToken cancellationToken);
}

public interface ICompanyReportingRepository
{
    Task<CompanyDashboard> GetDashboardAsync(
        Guid organizationId,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlyUsage>> GetMonthlyUsageAsync(
        Guid organizationId,
        DateOnly fromMonth,
        DateOnly toMonth,
        CancellationToken cancellationToken);
}

public interface ICompanyIntegrationRepository
{
    Task<CompanyApiKey> CreateApiKeyAsync(
        CompanyActor actor,
        Guid apiKeyId,
        string name,
        string prefix,
        string secretHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CompanyApiKey>> ListApiKeysAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<bool> RevokeApiKeyAsync(
        CompanyActor actor,
        Guid apiKeyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken);

    Task<Guid?> AuthenticateApiKeyAsync(
        string secretHash,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken);

    Task<ReservationImportResult> UpsertReservationAsync(
        Guid organizationId,
        ReservationImport reservation,
        DateTimeOffset importedAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationOutboxItem>> ListNotificationsAsync(
        Guid organizationId,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>
/// Composite contract kept for the single PostgreSQL implementation. Consumers
/// should depend on the narrowest interface that covers their use.
/// </summary>
public interface ICompanyRepository :
    ICompanyAuthenticationRepository,
    ICompanyOrganizationRepository,
    ICompanyTeamRepository,
    ICompanyCustomerRepository,
    ICompanyVisitRepository,
    ICompanyBillingRepository,
    ICompanyReportingRepository,
    ICompanyIntegrationRepository;
