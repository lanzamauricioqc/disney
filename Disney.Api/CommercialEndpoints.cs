using Disney.Application;

namespace Disney.Api;

internal static class CommercialEndpoints
{
    public static IEndpointRouteBuilder MapCommercialEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/v1/waitlist",
                RegisterInterestAsync)
            .WithTags("Commercial");

        var billing = endpoints.MapGroup("/api/v1/billing")
            .WithTags("Billing");
        billing.MapGet(
            "/products",
            (CheckoutService checkoutService) => Results.Ok(checkoutService.GetProducts()));
        billing.MapPost(
            "/checkout-sessions",
            CreateCheckoutSessionAsync);
        billing.MapPost(
            "/webhooks/stripe",
            ProcessStripeWebhookAsync);
        return endpoints;
    }

    private static async Task<IResult> RegisterInterestAsync(
        WaitlistRequest request,
        WaitlistService waitlistService,
        CancellationToken cancellationToken)
    {
        var outcome = await waitlistService.RegisterAsync(
            request.Email,
            cancellationToken);
        return outcome switch
        {
            WaitlistRegistrationOutcome.Registered => Results.Created(
                "/api/v1/waitlist",
                new WaitlistResponse("registered")),
            WaitlistRegistrationOutcome.AlreadyRegistered => Results.Ok(
                new WaitlistResponse("already_registered")),
            _ => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["Enter a valid email address."]
            })
        };
    }

    private static async Task<IResult> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        CheckoutService checkoutService,
        CancellationToken cancellationToken)
    {
        var result = await checkoutService.CreateAsync(
            request.Email,
            request.ProductCode,
            cancellationToken);
        if (result.IsSuccessful)
        {
            return Results.Ok(new CheckoutSessionResponse(result.CheckoutUrl!));
        }

        var statusCode = result.ErrorCode == "payments_not_configured"
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status400BadRequest;
        return Results.Problem(
            statusCode: statusCode,
            title: result.ErrorCode,
            detail: result.ErrorMessage);
    }

    private static async Task<IResult> ProcessStripeWebhookAsync(
        HttpRequest request,
        IPaymentWebhookHandler webhookHandler,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue("Stripe-Signature", out var signature))
        {
            return Results.BadRequest(new { error = "Missing Stripe-Signature header." });
        }

        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            var processed = await webhookHandler.HandleAsync(
                payload,
                signature.ToString(),
                cancellationToken);
            return Results.Ok(new { processed });
        }
        catch (Stripe.StripeException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (PaymentConfigurationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "payments_not_configured",
                detail: exception.Message);
        }
    }

    private sealed record WaitlistRequest(string? Email);
    private sealed record WaitlistResponse(string Status);
    private sealed record CheckoutSessionRequest(string? Email, string? ProductCode);
    private sealed record CheckoutSessionResponse(string CheckoutUrl);
}
