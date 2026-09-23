using Disney.Application;
using Disney.Domain;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Disney.Infrastructure;

internal sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public string SuccessUrl { get; init; } = string.Empty;
    public string CancelUrl { get; init; } = string.Empty;
    public string VisitPassPriceId { get; init; } = string.Empty;
    public string TripPassPriceId { get; init; } = string.Empty;
    public string CompanySuccessUrl { get; init; } = string.Empty;
    public string CompanyCancelUrl { get; init; } = string.Empty;
    public Dictionary<string, CompanyCreditBundleOptions> CompanyCreditBundles { get; init; } = [];
}

internal sealed class CompanyCreditBundleOptions
{
    public string PriceId { get; init; } = string.Empty;
    public int Credits { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? DisplayPrice { get; init; }
}

internal static class StripeCheckoutSession
{
    public static async Task<Session> CreateAsync(
        string secretKey,
        string priceId,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var sessionService = new SessionService(new StripeClient(secretKey));
        var session = await sessionService.CreateAsync(
            new SessionCreateOptions
            {
                Mode = "payment",
                CustomerEmail = customerEmail,
                SuccessUrl = AppendSessionPlaceholder(successUrl),
                CancelUrl = cancelUrl,
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                ],
                Metadata = metadata
            },
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            throw new InvalidOperationException(
                "Stripe created a Checkout Session without a redirect URL.");
        }

        return session;
    }

    private static string AppendSessionPlaceholder(string successUrl)
    {
        var separator = successUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{successUrl}{separator}session_id={{CHECKOUT_SESSION_ID}}";
    }
}

internal sealed class StripePaymentCheckoutGateway(
    IOptions<StripeOptions> options) : IPaymentCheckoutGateway
{
    private readonly StripeOptions _options = options.Value;

    public async Task<PaymentCheckoutSession> CreateAsync(
        EmailAddress emailAddress,
        CommercialProduct product,
        CancellationToken cancellationToken)
    {
        var priceId = GetPriceId(product.Code);
        ValidateConfiguration(priceId);

        var session = await StripeCheckoutSession.CreateAsync(
            _options.SecretKey,
            priceId,
            emailAddress.Value,
            _options.SuccessUrl,
            _options.CancelUrl,
            new Dictionary<string, string>
            {
                ["product_code"] = product.Code,
                ["purchaser_email"] = emailAddress.Value
            },
            cancellationToken);

        return new PaymentCheckoutSession(session.Id, session.Url);
    }

    private string GetPriceId(string productCode) =>
        productCode switch
        {
            "visit-pass" => _options.VisitPassPriceId,
            "trip-pass" => _options.TripPassPriceId,
            _ => string.Empty
        };

    private void ValidateConfiguration(string priceId)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey) ||
            string.IsNullOrWhiteSpace(priceId) ||
            string.IsNullOrWhiteSpace(_options.SuccessUrl) ||
            string.IsNullOrWhiteSpace(_options.CancelUrl))
        {
            throw new PaymentConfigurationException(
                "Stripe Checkout is not configured yet. Add the Stripe secret, price IDs, and return URLs.");
        }
    }
}

internal sealed class StripePaymentWebhookHandler(
    IOptions<StripeOptions> options,
    ICommercialRepository repository,
    ICompanyBillingRepository companyBillingRepository) : IPaymentWebhookHandler
{
    private readonly StripeOptions _options = options.Value;

    public async Task<bool> HandleAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            throw new PaymentConfigurationException(
                "Stripe webhook processing is not configured yet.");
        }

        var stripeEvent = ConstructEvent(payload, signature);
        if (stripeEvent.Data.Object is not Session session)
        {
            return false;
        }

        if (session.Metadata.TryGetValue("checkout_type", out var checkoutType) &&
            string.Equals(checkoutType, "company_credits", StringComparison.Ordinal))
        {
            if (!IsPaid(stripeEvent.Type, session.PaymentStatus))
            {
                return false;
            }

            return await companyBillingRepository.ApplyCompanyPaymentAsync(
                new CompanyPaymentEvent(
                    stripeEvent.Id,
                    session.Id,
                    stripeEvent.Type,
                    session.AmountTotal,
                    session.Currency,
                    new DateTimeOffset(stripeEvent.Created.ToUniversalTime())),
                cancellationToken);
        }

        var status = GetStatus(stripeEvent.Type, session.PaymentStatus);
        if (!status.HasValue)
        {
            return false;
        }

        var paymentEvent = new PaymentEvent(
            stripeEvent.Id,
            stripeEvent.Type,
            session.Id,
            status.Value,
            session.CustomerId,
            session.PaymentIntentId,
            session.AmountTotal,
            session.Currency,
            new DateTimeOffset(stripeEvent.Created.ToUniversalTime()));
        return await repository.ApplyPaymentEventAsync(paymentEvent, cancellationToken);
    }

    private Event ConstructEvent(string payload, string signature)
    {
        try
        {
            return EventUtility.ConstructEvent(
                payload,
                signature,
                _options.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException exception)
        {
            throw new PaymentValidationException(exception.Message, exception);
        }
    }

    private static PaymentStatus? GetStatus(
        string eventType,
        string? stripePaymentStatus) =>
        eventType switch
        {
            "checkout.session.completed" =>
                string.Equals(stripePaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                    ? PaymentStatus.Paid
                    : PaymentStatus.Pending,
            "checkout.session.async_payment_succeeded" => PaymentStatus.Paid,
            "checkout.session.async_payment_failed" => PaymentStatus.Failed,
            "checkout.session.expired" => PaymentStatus.Expired,
            _ => null
        };

    private static bool IsPaid(string eventType, string? stripePaymentStatus) =>
        eventType == "checkout.session.async_payment_succeeded" ||
        eventType == "checkout.session.completed" &&
        string.Equals(stripePaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
}

internal sealed class StripeCompanyBillingGateway(
    IOptions<StripeOptions> options) : ICompanyBillingGateway
{
    private readonly StripeOptions _options = options.Value;

    public IReadOnlyList<CompanyCreditBundle> GetCreditBundles() =>
        _options.CompanyCreditBundles
            .Where(bundle => bundle.Value.Credits > 0)
            .Select(bundle => new CompanyCreditBundle(
                bundle.Key,
                string.IsNullOrWhiteSpace(bundle.Value.DisplayName)
                    ? bundle.Key
                    : bundle.Value.DisplayName,
                bundle.Value.Credits,
                bundle.Value.DisplayPrice))
            .OrderBy(bundle => bundle.Credits)
            .ToList();

    public async Task<CompanyCheckoutSession> CreateCheckoutSessionAsync(
        Guid organizationId,
        string email,
        string bundleCode,
        CancellationToken cancellationToken)
    {
        if (!_options.CompanyCreditBundles.TryGetValue(bundleCode, out var bundle) ||
            string.IsNullOrWhiteSpace(_options.SecretKey) ||
            string.IsNullOrWhiteSpace(bundle.PriceId) ||
            bundle.Credits <= 0 ||
            string.IsNullOrWhiteSpace(_options.CompanySuccessUrl) ||
            string.IsNullOrWhiteSpace(_options.CompanyCancelUrl))
        {
            throw new PaymentConfigurationException(
                "Company credit bundles are not configured.");
        }

        var session = await StripeCheckoutSession.CreateAsync(
            _options.SecretKey,
            bundle.PriceId,
            email,
            _options.CompanySuccessUrl,
            _options.CompanyCancelUrl,
            new Dictionary<string, string>
            {
                ["checkout_type"] = "company_credits",
                ["organization_id"] = organizationId.ToString(),
                ["bundle_code"] = bundleCode,
                ["credits"] = bundle.Credits.ToString()
            },
            cancellationToken);

        return new CompanyCheckoutSession(
            session.Id,
            session.Url,
            bundleCode,
            bundle.Credits);
    }
}
