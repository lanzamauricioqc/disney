using Disney.Domain;

namespace Disney.Application;

public enum WaitlistRegistrationOutcome
{
    Registered,
    AlreadyRegistered,
    InvalidEmail
}

public sealed record CommercialProduct(
    string Code,
    string Name,
    string Description,
    long DisplayPriceInCents,
    string BillingPeriod);

public sealed record CheckoutSessionResult(
    bool IsSuccessful,
    string? CheckoutUrl,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static CheckoutSessionResult Success(string checkoutUrl) =>
        new(true, checkoutUrl, null, null);

    public static CheckoutSessionResult Failure(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}

public sealed record PaymentCheckoutSession(
    string ProviderSessionId,
    string CheckoutUrl);

public sealed record PaymentEvent(
    string ProviderEventId,
    string EventType,
    string ProviderSessionId,
    PaymentStatus Status,
    string? ProviderCustomerId,
    string? ProviderPaymentIntentId,
    long? AmountTotal,
    string? Currency,
    DateTimeOffset OccurredAt);

public interface IWaitlistRepository
{
    Task<bool> RegisterAsync(
        EmailAddress emailAddress,
        DateTimeOffset registeredAt,
        CancellationToken cancellationToken);
}

public interface ICommercialRepository
{
    Task SaveCheckoutSessionAsync(
        PaymentCheckoutSession checkoutSession,
        EmailAddress emailAddress,
        CommercialProduct product,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<bool> ApplyPaymentEventAsync(
        PaymentEvent paymentEvent,
        CancellationToken cancellationToken);
}

public interface IPaymentCheckoutGateway
{
    Task<PaymentCheckoutSession> CreateAsync(
        EmailAddress emailAddress,
        CommercialProduct product,
        CancellationToken cancellationToken);
}

public interface IPaymentWebhookHandler
{
    Task<bool> HandleAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken);
}

public sealed class WaitlistService(
    IWaitlistRepository repository,
    TimeProvider timeProvider)
{
    public async Task<WaitlistRegistrationOutcome> RegisterAsync(
        string? email,
        CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(email, out var emailAddress))
        {
            return WaitlistRegistrationOutcome.InvalidEmail;
        }

        var wasRegistered = await repository.RegisterAsync(
            emailAddress!,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return wasRegistered
            ? WaitlistRegistrationOutcome.Registered
            : WaitlistRegistrationOutcome.AlreadyRegistered;
    }
}

public sealed class CheckoutService(
    IPaymentCheckoutGateway paymentGateway,
    ICommercialRepository repository,
    TimeProvider timeProvider)
{
    private static readonly IReadOnlyList<CommercialProduct> Products =
    [
        new(
            "visit-pass",
            "Visit Pass",
            "Premium planning and live guidance for one park day.",
            800,
            "park day"),
        new(
            "trip-pass",
            "Trip Pass",
            "Premium planning across multiple park days during one trip.",
            2000,
            "trip")
    ];

    public IReadOnlyList<CommercialProduct> GetProducts() => Products;

    public async Task<CheckoutSessionResult> CreateAsync(
        string? email,
        string? productCode,
        CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(email, out var emailAddress))
        {
            return CheckoutSessionResult.Failure(
                "invalid_email",
                "Enter a valid email address.");
        }

        var product = Products.SingleOrDefault(product =>
            string.Equals(product.Code, productCode, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            return CheckoutSessionResult.Failure(
                "invalid_product",
                "Select a supported visit product.");
        }

        try
        {
            var checkoutSession = await paymentGateway.CreateAsync(
                emailAddress!,
                product,
                cancellationToken);
            await repository.SaveCheckoutSessionAsync(
                checkoutSession,
                emailAddress!,
                product,
                timeProvider.GetUtcNow(),
                cancellationToken);
            return CheckoutSessionResult.Success(checkoutSession.CheckoutUrl);
        }
        catch (PaymentConfigurationException exception)
        {
            return CheckoutSessionResult.Failure(
                "payments_not_configured",
                exception.Message);
        }
    }
}

public sealed class PaymentConfigurationException(string message) : Exception(message);

/// <summary>
/// Raised when a payment provider rejects a webhook payload or signature, so
/// callers never need to reference a provider-specific exception type.
/// </summary>
public sealed class PaymentValidationException(string message, Exception innerException)
    : Exception(message, innerException);
