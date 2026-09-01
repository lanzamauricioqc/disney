using System.Text.Json;
using Disney.Domain;

namespace Disney.Application;

public sealed class CompanyService(
    ICompanyRepository repository,
    ICompanyPasswordService passwordService,
    ICompanyTokenService tokenService,
    ICompanySecretService secretService,
    ICompanyNotificationOutbox notificationOutbox,
    ICompanyBillingGateway billingGateway,
    TimeProvider timeProvider)
{
    public async Task<CompanyAuthenticationResult> BootstrapAsync(
        string? organizationName,
        string? email,
        string? password,
        CancellationToken cancellationToken)
    {
        var errors = ValidateBootstrap(organizationName, email, password);
        if (errors.Count > 0)
        {
            return InvalidAuthentication(errors);
        }

        if (!tokenService.IsConfigured)
        {
            return new(
                CompanyAuthenticationOutcome.Unavailable,
                Message: "Company authentication is not configured.");
        }

        EmailAddress.TryCreate(email, out var normalizedEmail);
        var createdAt = timeProvider.GetUtcNow();
        var user = await repository.BootstrapAsync(
            organizationName!.Trim(),
            normalizedEmail!.Value,
            passwordService.Hash(password!),
            createdAt,
            cancellationToken);
        if (user is null)
        {
            return new(CompanyAuthenticationOutcome.AlreadyBootstrapped);
        }

        return AuthenticationSuccess(user);
    }

    public async Task<CompanyAuthenticationResult> LoginAsync(
        string? email,
        string? password,
        CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) ||
            string.IsNullOrEmpty(password))
        {
            return new(CompanyAuthenticationOutcome.InvalidCredentials);
        }

        if (!tokenService.IsConfigured)
        {
            return new(
                CompanyAuthenticationOutcome.Unavailable,
                Message: "Company authentication is not configured.");
        }

        var authenticationUser = await repository.FindAuthenticationUserAsync(
            normalizedEmail!.Value,
            cancellationToken);
        var passwordMatches = passwordService.Verify(
            authenticationUser?.PasswordHash,
            password);
        if (authenticationUser is null ||
            !authenticationUser.IsActive ||
            !passwordMatches)
        {
            return new(CompanyAuthenticationOutcome.InvalidCredentials);
        }

        var user = new CompanyUser(
            authenticationUser.Id,
            authenticationUser.OrganizationId,
            authenticationUser.Email,
            authenticationUser.Role,
            authenticationUser.IsActive,
            authenticationUser.CreatedAt);
        return AuthenticationSuccess(user);
    }

    public async Task<(CreatedCompanyInvitation? Invitation, string? Error)> InviteAsync(
        CompanyActor actor,
        string? email,
        CompanyRole role,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        if (!actor.Role.CanAssign(role))
        {
            return (null, "You cannot assign the selected role.");
        }

        if (!EmailAddress.TryCreate(email, out var normalizedEmail))
        {
            return (null, "Enter a valid email address.");
        }

        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(30))
        {
            return (null, "Invitation expiry must be between one minute and 30 days.");
        }

        var token = secretService.Generate();
        var now = timeProvider.GetUtcNow();
        var invitation = await repository.CreateInvitationAsync(
            actor,
            Guid.NewGuid(),
            normalizedEmail!.Value,
            role,
            secretService.Hash(token),
            now.Add(lifetime),
            now,
            cancellationToken);
        if (invitation is null)
        {
            return (null, "An active user or invitation already exists for this email.");
        }

        var notificationId = await notificationOutbox.QueueAsync(
            new NotificationRequest(
                actor.OrganizationId,
                "email",
                normalizedEmail.Value,
                "company-invitation-created",
                JsonSerializer.Serialize(new
                {
                    invitation.Id,
                    invitation.ExpiresAt
                })),
            now,
            cancellationToken);
        return (new CreatedCompanyInvitation(invitation, token, notificationId), null);
    }

    public async Task<CompanyAuthenticationResult> AcceptInvitationAsync(
        string? token,
        string? password,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(token))
        {
            errors["token"] = ["An invitation token is required."];
        }

        if (!CompanyPasswordRules.IsValid(password))
        {
            errors["password"] = [PasswordRequirements];
        }

        if (errors.Count > 0)
        {
            return InvalidAuthentication(errors);
        }

        if (!tokenService.IsConfigured)
        {
            return new(
                CompanyAuthenticationOutcome.Unavailable,
                Message: "Company authentication is not configured.");
        }

        var user = await repository.AcceptInvitationAsync(
            secretService.Hash(token!),
            passwordService.Hash(password!),
            timeProvider.GetUtcNow(),
            cancellationToken);
        return user is null
            ? new(CompanyAuthenticationOutcome.InvalidOrExpiredInvitation)
            : AuthenticationSuccess(user);
    }

    public Task<OrganizationDetails?> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        repository.GetOrganizationAsync(organizationId, cancellationToken);

    public Task<OrganizationDetails?> UpdateOrganizationAsync(
        CompanyActor actor,
        OrganizationUpdate update,
        CancellationToken cancellationToken) =>
        repository.UpdateOrganizationAsync(
            actor,
            update with
            {
                Name = update.Name.Trim(),
                SupportEmail = NormalizeOptionalEmail(update.SupportEmail),
                SupportPhone = CleanOptional(update.SupportPhone)
            },
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<OrganizationDetails?> UpdateBrandingAsync(
        CompanyActor actor,
        BrandingUpdate update,
        CancellationToken cancellationToken) =>
        repository.UpdateBrandingAsync(
            actor,
            update with
            {
                DisplayName = update.DisplayName.Trim(),
                LogoUrl = CleanOptional(update.LogoUrl),
                PrimaryColor = CleanOptional(update.PrimaryColor),
                SecondaryColor = CleanOptional(update.SecondaryColor),
                WelcomeMessage = CleanOptional(update.WelcomeMessage),
                SupportEmail = NormalizeOptionalEmail(update.SupportEmail),
                SupportPhone = CleanOptional(update.SupportPhone)
            },
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<IReadOnlyList<CompanyUser>> ListTeamAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        repository.ListTeamAsync(organizationId, cancellationToken);

    public Task<IReadOnlyList<CompanyInvitation>> ListInvitationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        repository.ListInvitationsAsync(organizationId, cancellationToken);

    public Task<bool> ChangeRoleAsync(
        CompanyActor actor,
        Guid userId,
        CompanyRole role,
        CancellationToken cancellationToken)
    {
        if (!actor.Role.CanAssign(role) || userId == actor.UserId)
        {
            return Task.FromResult(false);
        }

        return repository.ChangeRoleAsync(
            actor,
            userId,
            role,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<bool> DeactivateUserAsync(
        CompanyActor actor,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!actor.Role.CanManageTeam() || userId == actor.UserId)
        {
            return Task.FromResult(false);
        }

        return repository.DeactivateUserAsync(
            actor,
            userId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<IReadOnlyList<CompanyCustomer>> SearchCustomersAsync(
        Guid organizationId,
        string? search,
        int limit,
        CancellationToken cancellationToken) =>
        repository.SearchCustomersAsync(
            organizationId,
            CleanOptional(search),
            Math.Clamp(limit, 1, 200),
            cancellationToken);

    public Task<CompanyCustomer?> GetCustomerAsync(
        Guid organizationId,
        Guid customerId,
        CancellationToken cancellationToken) =>
        repository.GetCustomerAsync(organizationId, customerId, cancellationToken);

    public Task<CompanyCustomer> CreateCustomerAsync(
        CompanyActor actor,
        CompanyCustomerInput input,
        CancellationToken cancellationToken) =>
        repository.CreateCustomerAsync(
            actor,
            Guid.NewGuid(),
            NormalizeCustomer(input),
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<CompanyCustomer?> UpdateCustomerAsync(
        CompanyActor actor,
        Guid customerId,
        CompanyCustomerInput input,
        CancellationToken cancellationToken) =>
        repository.UpdateCustomerAsync(
            actor,
            customerId,
            NormalizeCustomer(input),
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<bool> DeleteCustomerAsync(
        CompanyActor actor,
        Guid customerId,
        CancellationToken cancellationToken) =>
        repository.DeleteCustomerAsync(
            actor,
            customerId,
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<IReadOnlyList<CompanyVisit>> ListVisitsAsync(
        Guid organizationId,
        DateOnly? from,
        DateOnly? to,
        CompanyVisitStatus? status,
        Guid? customerId,
        int limit,
        CancellationToken cancellationToken) =>
        repository.ListVisitsAsync(
            organizationId,
            from,
            to,
            status,
            customerId,
            Math.Clamp(limit, 1, 200),
            cancellationToken);

    public Task<CompanyVisit?> GetVisitAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken) =>
        repository.GetVisitAsync(organizationId, visitId, cancellationToken);

    public Task<CompanyVisit?> CreateVisitAsync(
        CompanyActor actor,
        CompanyVisitInput input,
        CancellationToken cancellationToken) =>
        repository.CreateVisitAsync(
            actor,
            Guid.NewGuid(),
            NormalizeVisit(input),
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<CompanyVisit?> UpdateVisitAsync(
        CompanyActor actor,
        Guid visitId,
        CompanyVisitInput input,
        CancellationToken cancellationToken) =>
        repository.UpdateVisitAsync(
            actor,
            visitId,
            NormalizeVisit(input),
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<bool> DeleteVisitAsync(
        CompanyActor actor,
        Guid visitId,
        CancellationToken cancellationToken) =>
        repository.DeleteVisitAsync(
            actor,
            visitId,
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<CompanyVisit?> UpdateVisitProgressAsync(
        CompanyActor actor,
        Guid visitId,
        VisitProgressUpdate update,
        CancellationToken cancellationToken) =>
        repository.UpdateVisitProgressAsync(
            actor,
            visitId,
            update,
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<VisitNote?> AddVisitNoteAsync(
        CompanyActor actor,
        Guid visitId,
        string note,
        CancellationToken cancellationToken) =>
        repository.AddVisitNoteAsync(
            actor,
            visitId,
            Guid.NewGuid(),
            note.Trim(),
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<IReadOnlyList<VisitNote>> ListVisitNotesAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken) =>
        repository.ListVisitNotesAsync(organizationId, visitId, cancellationToken);

    public Task<VisitOverride?> AddVisitOverrideAsync(
        CompanyActor actor,
        Guid visitId,
        string summary,
        string detailsJson,
        CancellationToken cancellationToken) =>
        repository.AddVisitOverrideAsync(
            actor,
            visitId,
            Guid.NewGuid(),
            summary.Trim(),
            detailsJson,
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<IReadOnlyList<VisitOverride>> ListVisitOverridesAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken) =>
        repository.ListVisitOverridesAsync(organizationId, visitId, cancellationToken);

    public Task<VisitEntitlement?> GetVisitEntitlementAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken) =>
        repository.GetVisitEntitlementAsync(organizationId, visitId, cancellationToken);

    public Task<VisitEntitlement?> AssignVisitEntitlementAsync(
        CompanyActor actor,
        Guid visitId,
        CancellationToken cancellationToken) =>
        repository.AssignVisitEntitlementAsync(
            actor,
            visitId,
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            cancellationToken);

    public async Task<CreatedVisitorAccessLink?> CreateVisitorAccessLinkAsync(
        CompanyActor actor,
        Guid visitId,
        DateTimeOffset expiresAt,
        string? channel,
        string? recipient,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (expiresAt <= now || expiresAt > now.AddDays(90))
        {
            return null;
        }

        var entitlement = await repository.GetVisitEntitlementAsync(
            actor.OrganizationId,
            visitId,
            cancellationToken);
        if (entitlement is null)
        {
            return null;
        }

        await repository.RevokeVisitorAccessLinksAsync(
            actor,
            visitId,
            now,
            cancellationToken);
        var token = secretService.Generate();
        var link = await repository.CreateVisitorAccessLinkAsync(
            actor,
            visitId,
            Guid.NewGuid(),
            secretService.Hash(token),
            expiresAt,
            now,
            cancellationToken);
        if (link is null)
        {
            return null;
        }

        Guid? notificationId = null;
        if (!string.IsNullOrWhiteSpace(channel) && !string.IsNullOrWhiteSpace(recipient))
        {
            notificationId = await notificationOutbox.QueueAsync(
                new NotificationRequest(
                    actor.OrganizationId,
                    channel.Trim().ToLowerInvariant(),
                    recipient.Trim(),
                    "visitor-access-link-created",
                    JsonSerializer.Serialize(new { link.Id, link.VisitId, link.ExpiresAt })),
                now,
                cancellationToken);
        }

        return new CreatedVisitorAccessLink(link, token, notificationId);
    }

    public Task<bool> RevokeVisitorAccessLinksAsync(
        CompanyActor actor,
        Guid visitId,
        CancellationToken cancellationToken) =>
        repository.RevokeVisitorAccessLinksAsync(
            actor,
            visitId,
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<VisitorVisit?> ResolveVisitorAccessAsync(
        string token,
        CancellationToken cancellationToken) =>
        repository.ResolveVisitorAccessAsync(
            secretService.Hash(token),
            timeProvider.GetUtcNow(),
            cancellationToken);

    public Task<CreditBalance> GetCreditBalanceAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        repository.GetCreditBalanceAsync(organizationId, cancellationToken);

    public Task<IReadOnlyList<CreditLedgerEntry>> ListCreditLedgerAsync(
        Guid organizationId,
        int limit,
        CancellationToken cancellationToken) =>
        repository.ListCreditLedgerAsync(
            organizationId,
            Math.Clamp(limit, 1, 200),
            cancellationToken);

    public Task<CreditLedgerEntry> AddCreditAdjustmentAsync(
        CompanyActor actor,
        int amount,
        string reason,
        CancellationToken cancellationToken) =>
        repository.AddCreditAdjustmentAsync(
            actor,
            amount,
            reason.Trim(),
            timeProvider.GetUtcNow(),
            cancellationToken);

    public async Task<CompanyCheckoutSession> CreateCheckoutSessionAsync(
        CompanyActor actor,
        string email,
        string bundleCode,
        CancellationToken cancellationToken)
    {
        var session = await billingGateway.CreateCheckoutSessionAsync(
            actor.OrganizationId,
            email,
            bundleCode,
            cancellationToken);
        await repository.SaveCompanyCheckoutSessionAsync(
            actor.OrganizationId,
            session,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return session;
    }

    public IReadOnlyList<CompanyCreditBundle> GetCreditBundles() =>
        billingGateway.GetCreditBundles();

    public Task<CompanyDashboard> GetDashboardAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        repository.GetDashboardAsync(
            organizationId,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);

    public Task<IReadOnlyList<MonthlyUsage>> GetMonthlyUsageAsync(
        Guid organizationId,
        DateOnly fromMonth,
        DateOnly toMonth,
        CancellationToken cancellationToken) =>
        repository.GetMonthlyUsageAsync(
            organizationId,
            new DateOnly(fromMonth.Year, fromMonth.Month, 1),
            new DateOnly(toMonth.Year, toMonth.Month, 1),
            cancellationToken);

    public async Task<CreatedCompanyApiKey> CreateApiKeyAsync(
        CompanyActor actor,
        string name,
        CancellationToken cancellationToken)
    {
        var secret = $"dsk_{secretService.Generate()}";
        var apiKey = await repository.CreateApiKeyAsync(
            actor,
            Guid.NewGuid(),
            name.Trim(),
            secretService.GetPrefix(secret),
            secretService.Hash(secret),
            timeProvider.GetUtcNow(),
            cancellationToken);
        return new CreatedCompanyApiKey(apiKey, secret);
    }

    public Task<IReadOnlyList<CompanyApiKey>> ListApiKeysAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        repository.ListApiKeysAsync(organizationId, cancellationToken);

    public Task<bool> RevokeApiKeyAsync(
        CompanyActor actor,
        Guid apiKeyId,
        CancellationToken cancellationToken) =>
        repository.RevokeApiKeyAsync(
            actor,
            apiKeyId,
            timeProvider.GetUtcNow(),
            cancellationToken);

    public async Task<ReservationImportResult?> ImportReservationAsync(
        string apiKey,
        ReservationImport reservation,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var organizationId = await repository.AuthenticateApiKeyAsync(
            secretService.Hash(apiKey),
            now,
            cancellationToken);
        if (!organizationId.HasValue)
        {
            return null;
        }

        return await repository.UpsertReservationAsync(
            organizationId.Value,
            reservation,
            now,
            cancellationToken);
    }

    public Task<IReadOnlyList<NotificationOutboxItem>> ListNotificationsAsync(
        Guid organizationId,
        int limit,
        CancellationToken cancellationToken) =>
        repository.ListNotificationsAsync(
            organizationId,
            Math.Clamp(limit, 1, 200),
            cancellationToken);

    private CompanyAuthenticationResult AuthenticationSuccess(CompanyUser user)
    {
        var token = tokenService.Issue(user);
        return new(
            CompanyAuthenticationOutcome.Success,
            new CompanyAuthenticationResponse(token.AccessToken, token.ExpiresAt, user));
    }

    private static CompanyAuthenticationResult InvalidAuthentication(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(CompanyAuthenticationOutcome.InvalidRequest, Errors: errors);

    private static Dictionary<string, string[]> ValidateBootstrap(
        string? organizationName,
        string? email,
        string? password)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(organizationName) || organizationName.Trim().Length > 200)
        {
            errors["organizationName"] = ["Organization name is required and must be 200 characters or fewer."];
        }

        if (!EmailAddress.TryCreate(email, out _))
        {
            errors["email"] = ["Enter a valid email address."];
        }

        if (!CompanyPasswordRules.IsValid(password))
        {
            errors["password"] = [PasswordRequirements];
        }

        return errors;
    }

    private static CompanyCustomerInput NormalizeCustomer(CompanyCustomerInput input) =>
        input with
        {
            Name = input.Name.Trim(),
            Email = NormalizeOptionalEmail(input.Email),
            Phone = CleanOptional(input.Phone),
            ExternalReference = CleanOptional(input.ExternalReference),
            Notes = CleanOptional(input.Notes)
        };

    private static CompanyVisitInput NormalizeVisit(CompanyVisitInput input) =>
        input with
        {
            ParkName = input.ParkName.Trim(),
            TimeZone = input.TimeZone.Trim(),
            Instructions = CleanOptional(input.Instructions),
            MeetingPoint = CleanOptional(input.MeetingPoint),
            TransportationDetails = CleanOptional(input.TransportationDetails),
            ExternalReference = CleanOptional(input.ExternalReference)
        };

    private static string? NormalizeOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return EmailAddress.TryCreate(email, out var normalized) ? normalized!.Value : email.Trim();
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public const string PasswordRequirements =
        "Password must be at least 12 characters and include uppercase, lowercase, number, and symbol.";
}
