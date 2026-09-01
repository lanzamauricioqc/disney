using System.Reflection;
using Disney.Application;
using Disney.Domain;
using Disney.Infrastructure;

namespace Disney.Tests;

public sealed class CompanyPlatformTests
{
    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase123!")]
    [InlineData("ALLUPPERCASE123!")]
    [InlineData("NoNumbersHere!")]
    [InlineData("NoSymbolsHere123")]
    public void PasswordRulesRejectWeakPasswords(string password)
    {
        Assert.False(CompanyPasswordRules.IsValid(password));
    }

    [Fact]
    public void PasswordServiceUsesOneWayIdentityHash()
    {
        var service = new CompanyPasswordService();

        var passwordHash = service.Hash("StrongPassword123!");

        Assert.DoesNotContain("StrongPassword123!", passwordHash);
        Assert.True(service.Verify(passwordHash, "StrongPassword123!"));
        Assert.False(service.Verify(passwordHash, "WrongPassword123!"));
    }

    [Fact]
    public void SecretServiceGeneratesHighEntropyHashedSecrets()
    {
        var service = new CompanySecretService();

        var first = service.Generate();
        var second = service.Generate();

        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 43);
        Assert.Equal(64, service.Hash(first).Length);
        Assert.DoesNotContain(first, service.Hash(first));
    }

    [Fact]
    public async Task BootstrapDoesNotPersistWhenJwtIsNotConfigured()
    {
        var repository = DispatchProxy.Create<ICompanyRepository, ThrowingRepositoryProxy>();
        var service = CreateService(repository, new StubTokenService(false));

        var result = await service.BootstrapAsync(
            "Agency",
            "owner@example.com",
            "StrongPassword123!",
            CancellationToken.None);

        Assert.Equal(CompanyAuthenticationOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task InvitationPersistsOnlyTokenHashAndQueuesNoSecret()
    {
        var repositoryProxy = DispatchProxy.Create<ICompanyRepository, InvitationRepositoryProxy>();
        var repository = (InvitationRepositoryProxy)(object)repositoryProxy;
        var outbox = new RecordingOutbox();
        var service = CreateService(repositoryProxy, new StubTokenService(true), outbox);
        var actor = new CompanyActor(Guid.NewGuid(), Guid.NewGuid(), CompanyRole.Owner);

        var result = await service.InviteAsync(
            actor,
            " Planner@Example.com ",
            CompanyRole.Planner,
            TimeSpan.FromDays(3),
            CancellationToken.None);

        Assert.NotNull(result.Invitation);
        Assert.NotEqual(result.Invitation!.Token, repository.TokenHash);
        Assert.Equal(64, repository.TokenHash!.Length);
        Assert.DoesNotContain(result.Invitation.Token, outbox.Request!.PayloadJson);
        Assert.Equal("planner@example.com", outbox.Request.Recipient);
    }

    [Fact]
    public void CompanyMigrationDefinesTenantAndSecurityBoundaries()
    {
        var migrationSql = ReadRepositoryFile(
            "Disney.Infrastructure",
            "Migrations",
            "008_add_company_platform.sql");

        Assert.Contains("CREATE TABLE public.company_organizations", migrationSql);
        Assert.Contains("CREATE TABLE public.company_users", migrationSql);
        Assert.Contains("CREATE TABLE public.company_notification_outbox", migrationSql);
        Assert.Contains("CREATE TABLE public.company_audit_logs", migrationSql);
        Assert.Contains("UNIQUE (organization_id, id)", migrationSql);
        Assert.Contains("token_hash text NOT NULL", migrationSql);
        Assert.Contains("secret_hash text NOT NULL", migrationSql);
        Assert.Contains("company_credit_ledger_append_only", migrationSql);
        Assert.DoesNotContain("password_hash text NULL", migrationSql);
    }

    [Fact]
    public void VisitEntitlementMigrationLinksOneCreditToOneTenantVisit()
    {
        var migrationSql = ReadRepositoryFile(
            "Disney.Infrastructure",
            "Migrations",
            "009_add_company_visit_entitlements.sql");

        Assert.Contains("CREATE TABLE public.company_visit_entitlements", migrationSql);
        Assert.Contains("UNIQUE (organization_id, visit_id)", migrationSql);
        Assert.Contains("UNIQUE (credit_ledger_entry_id)", migrationSql);
        Assert.Contains("REFERENCES public.company_credit_ledger (id)", migrationSql);
        Assert.Contains("REFERENCES public.company_visits (organization_id, id)", migrationSql);
    }

    [Fact]
    public void TenantOwnedRepositoryQueriesIncludeOrganizationScope()
    {
        var operationsSource = ReadRepositoryFile(
            "Disney.Infrastructure",
            "PostgreSqlCompanyOperationsRepository.cs");
        var commerceSource = ReadRepositoryFile(
            "Disney.Infrastructure",
            "PostgreSqlCompanyCommerceRepository.cs");

        Assert.Contains("visit.organization_id = @OrganizationId", operationsSource);
        Assert.Contains("customer.organization_id = @OrganizationId", operationsSource);
        Assert.Contains("WHERE organization_id = @OrganizationId", commerceSource);
        Assert.Contains("AND organization_id = @OrganizationId", commerceSource);
    }

    private static CompanyService CreateService(
        ICompanyRepository repository,
        ICompanyTokenService tokenService,
        ICompanyNotificationOutbox? outbox = null) =>
        new(
            repository,
            new CompanyPasswordService(),
            tokenService,
            new CompanySecretService(),
            outbox ?? new RecordingOutbox(),
            new StubBillingGateway(),
            TimeProvider.System);

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var path = Path.Combine(
            [AppContext.BaseDirectory, "..", "..", "..", "..", .. pathParts]);
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private sealed class StubTokenService(bool isConfigured) : ICompanyTokenService
    {
        public bool IsConfigured { get; } = isConfigured;

        public CompanyAccessToken Issue(CompanyUser user) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));
    }

    private sealed class RecordingOutbox : ICompanyNotificationOutbox
    {
        public NotificationRequest? Request { get; private set; }

        public Task<Guid> QueueAsync(
            NotificationRequest request,
            DateTimeOffset queuedAt,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class StubBillingGateway : ICompanyBillingGateway
    {
        public IReadOnlyList<CompanyCreditBundle> GetCreditBundles() => [];

        public Task<CompanyCheckoutSession> CreateCheckoutSessionAsync(
            Guid organizationId,
            string email,
            string bundleCode,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private class ThrowingRepositoryProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? arguments) =>
            throw new InvalidOperationException(
                $"Repository method {targetMethod?.Name} should not have been called.");
    }

    private class InvitationRepositoryProxy : DispatchProxy
    {
        public string? TokenHash { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments)
        {
            if (targetMethod?.Name != nameof(ICompanyRepository.CreateInvitationAsync))
            {
                throw new InvalidOperationException(
                    $"Unexpected repository method {targetMethod?.Name}.");
            }

            TokenHash = (string)arguments![4]!;
            var invitation = new CompanyInvitation(
                (Guid)arguments[1]!,
                (string)arguments[2]!,
                (CompanyRole)arguments[3]!,
                (DateTimeOffset)arguments[5]!,
                (DateTimeOffset)arguments[6]!,
                null,
                null);
            return Task.FromResult<CompanyInvitation?>(invitation);
        }
    }
}
