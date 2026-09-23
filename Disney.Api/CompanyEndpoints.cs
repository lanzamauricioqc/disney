using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Disney.Application;
using Disney.Domain;

namespace Disney.Api;

internal static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var root = endpoints.MapGroup("/api/v1/company")
            .WithTags("Company");

        var authentication = root.MapGroup("/auth")
            .RequireRateLimiting("company-auth");
        authentication.MapPost("/bootstrap", BootstrapAsync);
        authentication.MapPost("/login", LoginAsync);
        authentication.MapPost("/invitations/accept", AcceptInvitationAsync);

        root.MapGet("/visitor-access/{token}", ResolveVisitorAccessAsync)
            .RequireRateLimiting("company-auth");
        root.MapPost("/integrations/reservations", ImportReservationAsync)
            .RequireRateLimiting("company-integration");

        var company = root.MapGroup(string.Empty)
            .RequireAuthorization("CompanyMember");

        company.MapGet("/dashboard", GetDashboardAsync);
        company.MapGet("/organization", GetOrganizationAsync);
        company.MapPut("/organization", UpdateOrganizationAsync)
            .RequireAuthorization("CompanyConfigurationManagement");
        company.MapPut("/branding", UpdateBrandingAsync)
            .RequireAuthorization("CompanyConfigurationManagement");

        company.MapGet("/team", ListTeamAsync)
            .RequireAuthorization("CompanyTeamManagement");
        company.MapGet("/team/invitations", ListInvitationsAsync)
            .RequireAuthorization("CompanyTeamManagement");
        company.MapPost("/team/invitations", InviteTeamMemberAsync)
            .RequireAuthorization("CompanyTeamManagement");
        company.MapPut("/team/{userId:guid}/role", ChangeRoleAsync)
            .RequireAuthorization("CompanyTeamManagement");
        company.MapPost("/team/{userId:guid}/deactivate", DeactivateTeamMemberAsync)
            .RequireAuthorization("CompanyTeamManagement");

        company.MapGet("/customers", SearchCustomersAsync);
        company.MapGet("/customers/{customerId:guid}", GetCustomerAsync);
        company.MapPost("/customers", CreateCustomerAsync);
        company.MapPut("/customers/{customerId:guid}", UpdateCustomerAsync);
        company.MapDelete("/customers/{customerId:guid}", DeleteCustomerAsync);

        company.MapGet("/visits", ListVisitsAsync);
        company.MapGet("/visits/{visitId:guid}", GetVisitAsync);
        company.MapPost("/visits", CreateVisitAsync);
        company.MapPut("/visits/{visitId:guid}", UpdateVisitAsync);
        company.MapDelete("/visits/{visitId:guid}", DeleteVisitAsync);
        company.MapPut("/visits/{visitId:guid}/progress", UpdateVisitProgressAsync);
        company.MapGet("/visits/{visitId:guid}/notes", ListVisitNotesAsync);
        company.MapPost("/visits/{visitId:guid}/notes", AddVisitNoteAsync);
        company.MapGet("/visits/{visitId:guid}/overrides", ListVisitOverridesAsync);
        company.MapPost("/visits/{visitId:guid}/overrides", AddVisitOverrideAsync);
        company.MapGet("/visits/{visitId:guid}/entitlement", GetVisitEntitlementAsync);
        company.MapPost("/visits/{visitId:guid}/entitlement", AssignVisitEntitlementAsync);
        company.MapPost("/visits/{visitId:guid}/access-link", CreateAccessLinkAsync);
        company.MapDelete("/visits/{visitId:guid}/access-link", RevokeAccessLinkAsync);

        company.MapGet("/credits", GetCreditsAsync);
        company.MapGet("/credits/usage", GetCreditUsageAsync);
        company.MapPost("/credits/adjustments", AddCreditAdjustmentAsync)
            .RequireAuthorization("CompanyBillingManagement");
        company.MapGet("/billing/products", GetCompanyBillingProducts);
        company.MapPost("/billing/checkout-sessions", CreateCompanyCheckoutAsync)
            .RequireAuthorization("CompanyBillingManagement");

        company.MapGet("/reports/usage", GetUsageAsync);
        company.MapGet("/reports/usage.csv", ExportUsageCsvAsync);

        company.MapGet("/integrations/api-keys", ListApiKeysAsync)
            .RequireAuthorization("CompanyConfigurationManagement");
        company.MapPost("/integrations/api-keys", CreateApiKeyAsync)
            .RequireAuthorization("CompanyConfigurationManagement");
        company.MapDelete("/integrations/api-keys/{apiKeyId:guid}", RevokeApiKeyAsync)
            .RequireAuthorization("CompanyConfigurationManagement");
        company.MapGet("/notifications", ListNotificationsAsync)
            .RequireAuthorization("CompanyConfigurationManagement");
        return endpoints;
    }

    private static async Task<IResult> BootstrapAsync(
        BootstrapRequest request,
        CompanyService service,
        CancellationToken cancellationToken) =>
        AuthenticationResult(await service.BootstrapAsync(
            request.OrganizationName,
            request.Email,
            request.Password,
            cancellationToken));

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        CompanyService service,
        CancellationToken cancellationToken) =>
        AuthenticationResult(await service.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken));

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptInvitationRequest request,
        CompanyService service,
        CancellationToken cancellationToken) =>
        AuthenticationResult(await service.AcceptInvitationAsync(
            request.Token,
            request.Password,
            cancellationToken));

    private static async Task<IResult> GetDashboardAsync(
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var actor = GetActor(principal);
        return Results.Ok(await service.GetDashboardAsync(
            actor.OrganizationId,
            cancellationToken));
    }

    private static async Task<IResult> GetOrganizationAsync(
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var organization = await service.GetOrganizationAsync(
            GetActor(principal).OrganizationId,
            cancellationToken);
        return organization is null ? Results.NotFound() : Results.Ok(organization);
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        OrganizationRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var errors = ValidateOrganization(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var organization = await service.UpdateOrganizationAsync(
            GetActor(principal),
            new OrganizationUpdate(request.Name!, request.SupportEmail, request.SupportPhone),
            cancellationToken);
        return organization is null ? Results.NotFound() : Results.Ok(organization);
    }

    private static async Task<IResult> UpdateBrandingAsync(
        BrandingRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var errors = ValidateBranding(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var organization = await service.UpdateBrandingAsync(
            GetActor(principal),
            new BrandingUpdate(
                request.DisplayName!,
                request.LogoUrl,
                request.PrimaryColor,
                request.SecondaryColor,
                request.WelcomeMessage,
                request.SupportEmail,
                request.SupportPhone),
            cancellationToken);
        return organization is null ? Results.NotFound() : Results.Ok(organization);
    }

    private static async Task<IResult> ListTeamAsync(
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListTeamAsync(
            GetActor(principal).OrganizationId,
            cancellationToken));

    private static async Task<IResult> ListInvitationsAsync(
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListInvitationsAsync(
            GetActor(principal).OrganizationId,
            cancellationToken));

    private static async Task<IResult> InviteTeamMemberAsync(
        TeamInvitationRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var expiresInHours = request.ExpiresInHours ?? 72;
        if (expiresInHours is < 1 or > 720)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["expiresInHours"] = ["Invitation expiry must be between 1 and 720 hours."]
            });
        }

        var lifetime = TimeSpan.FromHours(expiresInHours);
        var result = await service.InviteAsync(
            GetActor(principal),
            request.Email,
            request.Role,
            lifetime,
            cancellationToken);
        return result.Invitation is null
            ? Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "invitation_not_created",
                detail: result.Error)
            : Results.Created(
                $"/api/v1/company/team/invitations/{result.Invitation.Invitation.Id}",
                result.Invitation);
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid userId,
        ChangeRoleRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        await service.ChangeRoleAsync(
            GetActor(principal),
            userId,
            request.Role,
            cancellationToken)
            ? Results.NoContent()
            : Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "role_not_changed",
                detail: "The role change is not permitted or the user was not found.");

    private static async Task<IResult> DeactivateTeamMemberAsync(
        Guid userId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        await service.DeactivateUserAsync(
            GetActor(principal),
            userId,
            cancellationToken)
            ? Results.NoContent()
            : Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "user_not_deactivated",
                detail: "The user cannot be deactivated.");

    private static async Task<IResult> SearchCustomersAsync(
        string? search,
        int? limit,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.SearchCustomersAsync(
            GetActor(principal).OrganizationId,
            search,
            limit ?? 50,
            cancellationToken));

    private static async Task<IResult> GetCustomerAsync(
        Guid customerId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var customer = await service.GetCustomerAsync(
            GetActor(principal).OrganizationId,
            customerId,
            cancellationToken);
        return customer is null ? Results.NotFound() : Results.Ok(customer);
    }

    private static async Task<IResult> CreateCustomerAsync(
        CustomerRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCustomer(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var customer = await service.CreateCustomerAsync(
            GetActor(principal),
            ToCustomerInput(request),
            cancellationToken);
        return Results.Created($"/api/v1/company/customers/{customer.Id}", customer);
    }

    private static async Task<IResult> UpdateCustomerAsync(
        Guid customerId,
        CustomerRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCustomer(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var customer = await service.UpdateCustomerAsync(
            GetActor(principal),
            customerId,
            ToCustomerInput(request),
            cancellationToken);
        return customer is null ? Results.NotFound() : Results.Ok(customer);
    }

    private static async Task<IResult> DeleteCustomerAsync(
        Guid customerId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        await service.DeleteCustomerAsync(
            GetActor(principal),
            customerId,
            cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> ListVisitsAsync(
        DateOnly? from,
        DateOnly? to,
        CompanyVisitStatus? status,
        Guid? customerId,
        int? limit,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListVisitsAsync(
            GetActor(principal).OrganizationId,
            from,
            to,
            status,
            customerId,
            limit ?? 100,
            cancellationToken));

    private static async Task<IResult> GetVisitAsync(
        Guid visitId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var visit = await service.GetVisitAsync(
            GetActor(principal).OrganizationId,
            visitId,
            cancellationToken);
        return visit is null ? Results.NotFound() : Results.Ok(visit);
    }

    private static async Task<IResult> CreateVisitAsync(
        VisitRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var errors = ValidateVisit(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var visit = await service.CreateVisitAsync(
            GetActor(principal),
            ToVisitInput(request),
            cancellationToken);
        return visit is null
            ? Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["customerId"] = ["The customer was not found in this organization."]
            })
            : Results.Created($"/api/v1/company/visits/{visit.Id}", visit);
    }

    private static async Task<IResult> UpdateVisitAsync(
        Guid visitId,
        VisitRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var errors = ValidateVisit(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var visit = await service.UpdateVisitAsync(
            GetActor(principal),
            visitId,
            ToVisitInput(request),
            cancellationToken);
        return visit is null ? Results.NotFound() : Results.Ok(visit);
    }

    private static async Task<IResult> DeleteVisitAsync(
        Guid visitId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        await service.DeleteVisitAsync(
            GetActor(principal),
            visitId,
            cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> UpdateVisitProgressAsync(
        Guid visitId,
        VisitProgressRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        if (request.CompletedItemCount < 0 ||
            request.TotalItemCount < 0 ||
            request.CompletedItemCount > request.TotalItemCount)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["progress"] = ["Progress counts must be non-negative and completed cannot exceed total."]
            });
        }

        var visit = await service.UpdateVisitProgressAsync(
            GetActor(principal),
            visitId,
            new VisitProgressUpdate(
                request.Status,
                request.CompletedItemCount,
                request.TotalItemCount),
            cancellationToken);
        return visit is null ? Results.NotFound() : Results.Ok(visit);
    }

    private static async Task<IResult> ListVisitNotesAsync(
        Guid visitId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListVisitNotesAsync(
            GetActor(principal).OrganizationId,
            visitId,
            cancellationToken));

    private static async Task<IResult> AddVisitNoteAsync(
        Guid visitId,
        VisitNoteRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 10000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["note"] = ["A note between 1 and 10,000 characters is required."]
            });
        }

        var note = await service.AddVisitNoteAsync(
            GetActor(principal),
            visitId,
            request.Note,
            cancellationToken);
        return note is null ? Results.NotFound() : Results.Ok(note);
    }

    private static async Task<IResult> ListVisitOverridesAsync(
        Guid visitId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListVisitOverridesAsync(
            GetActor(principal).OrganizationId,
            visitId,
            cancellationToken));

    private static async Task<IResult> AddVisitOverrideAsync(
        Guid visitId,
        VisitOverrideRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Summary) ||
            request.Summary.Length > 500 ||
            string.IsNullOrWhiteSpace(request.DetailsJson) ||
            !IsJson(request.DetailsJson))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["override"] = ["A summary and valid JSON details are required."]
            });
        }

        var overrideRecord = await service.AddVisitOverrideAsync(
            GetActor(principal),
            visitId,
            request.Summary,
            request.DetailsJson,
            cancellationToken);
        return overrideRecord is null ? Results.NotFound() : Results.Ok(overrideRecord);
    }

    private static async Task<IResult> CreateAccessLinkAsync(
        Guid visitId,
        AccessLinkRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        if ((!string.IsNullOrWhiteSpace(request.Channel) &&
             request.Channel is not ("email" or "sms")) ||
            (string.IsNullOrWhiteSpace(request.Channel) !=
             string.IsNullOrWhiteSpace(request.Recipient)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["notification"] = ["Channel and recipient must be supplied together; channel must be email or sms."]
            });
        }

        var link = await service.CreateVisitorAccessLinkAsync(
            GetActor(principal),
            visitId,
            request.ExpiresAt,
            request.Channel,
            request.Recipient,
            cancellationToken);
        return link is null
            ? Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["expiresAt"] = ["Expiry must be in the future and no more than 90 days away."]
            })
            : Results.Ok(link);
    }

    private static async Task<IResult> GetVisitEntitlementAsync(
        Guid visitId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var entitlement = await service.GetVisitEntitlementAsync(
            GetActor(principal).OrganizationId,
            visitId,
            cancellationToken);
        return Results.Ok(entitlement);
    }

    private static async Task<IResult> AssignVisitEntitlementAsync(
        Guid visitId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var entitlement = await service.AssignVisitEntitlementAsync(
            GetActor(principal),
            visitId,
            cancellationToken);
        return entitlement is null
            ? Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "entitlement_not_assigned",
                detail: "The visit is missing, already entitled, or the organization has no available credits.")
            : Results.Ok(entitlement);
    }

    private static async Task<IResult> RevokeAccessLinkAsync(
        Guid visitId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        await service.RevokeVisitorAccessLinksAsync(
            GetActor(principal),
            visitId,
            cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> ResolveVisitorAccessAsync(
        string token,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
        {
            return Results.NotFound();
        }

        var visit = await service.ResolveVisitorAccessAsync(token, cancellationToken);
        return visit is null ? Results.NotFound() : Results.Ok(visit);
    }

    private static async Task<IResult> GetCreditsAsync(
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var organizationId = GetActor(principal).OrganizationId;
        var balance = await service.GetCreditBalanceAsync(organizationId, cancellationToken);
        var entries = await service.ListCreditLedgerAsync(organizationId, 50, cancellationToken);
        return Results.Ok(new { balance, entries });
    }

    private static async Task<IResult> GetCreditUsageAsync(
        int? limit,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListCreditLedgerAsync(
            GetActor(principal).OrganizationId,
            limit ?? 100,
            cancellationToken));

    private static async Task<IResult> AddCreditAdjustmentAsync(
        CreditAdjustmentRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        if (request.Amount == 0 ||
            request.Amount is < -1_000_000 or > 1_000_000 ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Length > 500)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["adjustment"] = ["A non-zero amount and reason of 500 characters or fewer are required."]
            });
        }

        return Results.Ok(await service.AddCreditAdjustmentAsync(
            GetActor(principal),
            request.Amount,
            request.Reason,
            cancellationToken));
    }

    private static async Task<IResult> CreateCompanyCheckoutAsync(
        CompanyCheckoutRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BundleCode))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["bundleCode"] = ["A configured bundle code is required."]
            });
        }

        try
        {
            var actor = GetActor(principal);
            var email = principal.FindFirstValue("email") ??
                principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            return Results.Ok(await service.CreateCheckoutSessionAsync(
                actor,
                email,
                request.BundleCode,
                cancellationToken));
        }
        catch (PaymentConfigurationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "company_billing_not_configured",
                detail: exception.Message);
        }
    }

    private static IResult GetCompanyBillingProducts(CompanyService service) =>
        Results.Ok(service.GetCreditBundles());

    private static async Task<IResult> GetUsageAsync(
        DateOnly? fromMonth,
        DateOnly? toMonth,
        ClaimsPrincipal principal,
        CompanyService service,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var (from, to, error) = UsageRange(fromMonth, toMonth, timeProvider);
        return error is not null
            ? Results.ValidationProblem(error)
            : Results.Ok(await service.GetMonthlyUsageAsync(
                GetActor(principal).OrganizationId,
                from,
                to,
                cancellationToken));
    }

    private static async Task<IResult> ExportUsageCsvAsync(
        DateOnly? fromMonth,
        DateOnly? toMonth,
        ClaimsPrincipal principal,
        CompanyService service,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var (from, to, error) = UsageRange(fromMonth, toMonth, timeProvider);
        if (error is not null)
        {
            return Results.ValidationProblem(error);
        }

        var usage = await service.GetMonthlyUsageAsync(
            GetActor(principal).OrganizationId,
            from,
            to,
            cancellationToken);
        var csv = new StringBuilder("month,credits_consumed,visits_created\r\n");
        foreach (var month in usage)
        {
            csv.Append(month.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(month.CreditsConsumed)
                .Append(',')
                .Append(month.VisitsCreated)
                .Append("\r\n");
        }

        return Results.Text(
            csv.ToString(),
            "text/csv",
            Encoding.UTF8,
            StatusCodes.Status200OK);
    }

    private static async Task<IResult> ListApiKeysAsync(
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListApiKeysAsync(
            GetActor(principal).OrganizationId,
            cancellationToken));

    private static async Task<IResult> CreateApiKeyAsync(
        CreateApiKeyRequest request,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["A name of 100 characters or fewer is required."]
            });
        }

        return Results.Ok(await service.CreateApiKeyAsync(
            GetActor(principal),
            request.Name,
            cancellationToken));
    }

    private static async Task<IResult> RevokeApiKeyAsync(
        Guid apiKeyId,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        await service.RevokeApiKeyAsync(
            GetActor(principal),
            apiKeyId,
            cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> ImportReservationAsync(
        ReservationImport request,
        HttpRequest httpRequest,
        CompanyService service,
        CancellationToken cancellationToken)
    {
        var apiKey = httpRequest.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Results.Unauthorized();
        }

        var errors = ValidateReservation(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await service.ImportReservationAsync(
            apiKey,
            request,
            cancellationToken);
        return result is null ? Results.Unauthorized() : Results.Ok(result);
    }

    private static async Task<IResult> ListNotificationsAsync(
        int? limit,
        ClaimsPrincipal principal,
        CompanyService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListNotificationsAsync(
            GetActor(principal).OrganizationId,
            limit ?? 50,
            cancellationToken));

    private static IResult AuthenticationResult(CompanyAuthenticationResult result) =>
        result.Outcome switch
        {
            CompanyAuthenticationOutcome.Success => Results.Ok(result.Authentication),
            CompanyAuthenticationOutcome.InvalidRequest =>
                Results.ValidationProblem(result.Errors!),
            CompanyAuthenticationOutcome.InvalidCredentials => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "invalid_credentials",
                detail: "The email or password is invalid."),
            CompanyAuthenticationOutcome.AlreadyBootstrapped => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "already_bootstrapped",
                detail: "An organization already exists."),
            CompanyAuthenticationOutcome.InvalidOrExpiredInvitation => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "invalid_invitation",
                detail: "The invitation is invalid, expired, or already used."),
            _ => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "company_authentication_unavailable",
                detail: result.Message)
        };

    private static CompanyActor GetActor(ClaimsPrincipal principal) =>
        new(
            Guid.Parse(principal.FindFirstValue(CompanyClaimNames.OrganizationId)!),
            Guid.Parse(principal.FindFirstValue("sub")!),
            Enum.Parse<CompanyRole>(principal.FindFirstValue("role")!, true));

    private static Dictionary<string, string[]> ValidateOrganization(
        OrganizationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            errors["name"] = ["Name is required and must be 200 characters or fewer."];
        }

        AddOptionalEmailError(errors, request.SupportEmail);
        return errors;
    }

    private static Dictionary<string, string[]> ValidateBranding(BrandingRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            request.DisplayName.Trim().Length > 200)
        {
            errors["displayName"] = ["Display name is required and must be 200 characters or fewer."];
        }

        if (!CompanyColorRules.IsValid(request.PrimaryColor) ||
            !CompanyColorRules.IsValid(request.SecondaryColor))
        {
            errors["colors"] = ["Colors must use #RRGGBB format."];
        }

        if (!string.IsNullOrWhiteSpace(request.LogoUrl) &&
            (!Uri.TryCreate(request.LogoUrl, UriKind.Absolute, out var logoUri) ||
             logoUri.Scheme is not ("https" or "http")))
        {
            errors["logoUrl"] = ["Logo URL must be an absolute HTTP or HTTPS URL."];
        }

        AddOptionalEmailError(errors, request.SupportEmail);
        return errors;
    }

    private static Dictionary<string, string[]> ValidateCustomer(CustomerRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 300)
        {
            errors["name"] = ["Name is required and must be 300 characters or fewer."];
        }

        AddOptionalEmailError(errors, request.Email);
        return errors;
    }

    private static Dictionary<string, string[]> ValidateVisit(VisitRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.CustomerId == Guid.Empty)
        {
            errors["customerId"] = ["Customer ID is required."];
        }

        if (string.IsNullOrWhiteSpace(request.ParkName) || request.ParkName.Length > 200)
        {
            errors["parkName"] = ["Park name is required and must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(request.TimeZone) || request.TimeZone.Length > 100)
        {
            errors["timeZone"] = ["Time zone is required and must be 100 characters or fewer."];
        }

        if (request.PartySize is < 1 or > 100)
        {
            errors["partySize"] = ["Party size must be between 1 and 100."];
        }

        if (request.CompletedItemCount < 0 ||
            request.TotalItemCount < 0 ||
            request.CompletedItemCount > request.TotalItemCount)
        {
            errors["progress"] = ["Progress counts must be non-negative and completed cannot exceed total."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateReservation(ReservationImport request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.CustomerExternalReference))
        {
            errors["customerExternalReference"] = ["Customer external reference is required."];
        }

        if (string.IsNullOrWhiteSpace(request.ReservationExternalReference))
        {
            errors["reservationExternalReference"] = ["Reservation external reference is required."];
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            errors["customerName"] = ["Customer name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.ParkName))
        {
            errors["parkName"] = ["Park name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.TimeZone))
        {
            errors["timeZone"] = ["Time zone is required."];
        }

        if (request.PartySize is < 1 or > 100)
        {
            errors["partySize"] = ["Party size must be between 1 and 100."];
        }

        AddOptionalEmailError(errors, request.CustomerEmail);
        return errors;
    }

    private static void AddOptionalEmailError(
        IDictionary<string, string[]> errors,
        string? email)
    {
        if (!string.IsNullOrWhiteSpace(email) &&
            !EmailAddress.TryCreate(email, out _))
        {
            errors["email"] = ["Enter a valid email address."];
        }
    }

    private static CompanyCustomerInput ToCustomerInput(CustomerRequest request) =>
        new(
            request.Name!,
            request.Email,
            request.Phone,
            request.ExternalReference,
            request.Notes);

    private static CompanyVisitInput ToVisitInput(VisitRequest request) =>
        new(
            request.CustomerId,
            request.ParkName!,
            request.VisitDate,
            request.TimeZone!,
            request.Status,
            request.PartySize,
            request.Instructions,
            request.MeetingPoint,
            request.TransportationDetails,
            request.CompletedItemCount,
            request.TotalItemCount,
            request.ExternalReference);

    private static bool IsJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static (
        DateOnly From,
        DateOnly To,
        Dictionary<string, string[]>? Error) UsageRange(
        DateOnly? fromMonth,
        DateOnly? toMonth,
        TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var to = toMonth ?? new DateOnly(today.Year, today.Month, 1);
        var from = fromMonth ?? to.AddMonths(-11);
        if (from > to || to.Year * 12 + to.Month - (from.Year * 12 + from.Month) > 35)
        {
            return (
                from,
                to,
                new Dictionary<string, string[]>
                {
                    ["range"] = ["Usage range must be ordered and no longer than 36 months."]
                });
        }

        return (from, to, null);
    }

    private sealed record BootstrapRequest(
        string? OrganizationName,
        string? Email,
        string? Password);

    private sealed record LoginRequest(string? Email, string? Password);
    private sealed record AcceptInvitationRequest(string? Token, string? Password);
    private sealed record OrganizationRequest(
        string? Name,
        string? SupportEmail,
        string? SupportPhone);
    private sealed record BrandingRequest(
        string? DisplayName,
        string? LogoUrl,
        string? PrimaryColor,
        string? SecondaryColor,
        string? WelcomeMessage,
        string? SupportEmail,
        string? SupportPhone);
    private sealed record TeamInvitationRequest(
        string? Email,
        CompanyRole Role,
        int? ExpiresInHours);
    private sealed record ChangeRoleRequest(CompanyRole Role);
    private sealed record CustomerRequest(
        string? Name,
        string? Email,
        string? Phone,
        string? ExternalReference,
        string? Notes);
    private sealed record VisitRequest(
        Guid CustomerId,
        string? ParkName,
        DateOnly VisitDate,
        string? TimeZone,
        CompanyVisitStatus Status,
        int PartySize,
        string? Instructions,
        string? MeetingPoint,
        string? TransportationDetails,
        int CompletedItemCount,
        int TotalItemCount,
        string? ExternalReference);
    private sealed record VisitProgressRequest(
        CompanyVisitStatus Status,
        int CompletedItemCount,
        int TotalItemCount);
    private sealed record VisitNoteRequest(string Note);
    private sealed record VisitOverrideRequest(string Summary, string DetailsJson);
    private sealed record AccessLinkRequest(
        DateTimeOffset ExpiresAt,
        string? Channel,
        string? Recipient);
    private sealed record CreditAdjustmentRequest(int Amount, string Reason);
    private sealed record CompanyCheckoutRequest(string BundleCode);
    private sealed record CreateApiKeyRequest(string Name);
}
