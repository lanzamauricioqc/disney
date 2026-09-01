using Disney.Application;
using Disney.Domain;

namespace Disney.Tests;

public sealed class CommercialTests
{
    [Theory]
    [InlineData("visitor@example.com", "visitor@example.com")]
    [InlineData(" Visitor@Example.com ", "visitor@example.com")]
    public void EmailAddressAcceptsAndNormalizesValidEmail(
        string candidate,
        string expected)
    {
        var wasCreated = EmailAddress.TryCreate(candidate, out var emailAddress);

        Assert.True(wasCreated);
        Assert.Equal(expected, emailAddress!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void EmailAddressRejectsInvalidEmail(string? candidate)
    {
        var wasCreated = EmailAddress.TryCreate(candidate, out var emailAddress);

        Assert.False(wasCreated);
        Assert.Null(emailAddress);
    }

    [Fact]
    public async Task WaitlistServiceRegistersValidEmail()
    {
        var repository = new StubWaitlistRepository(true);
        var service = new WaitlistService(
            repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero)));

        var result = await service.RegisterAsync("Visitor@Example.com", CancellationToken.None);

        Assert.Equal(WaitlistRegistrationOutcome.Registered, result);
        Assert.Equal("visitor@example.com", repository.EmailAddress!.Value);
    }

    [Fact]
    public async Task CheckoutServiceRejectsUnknownProductBeforeCallingStripe()
    {
        var gateway = new StubPaymentGateway();
        var service = new CheckoutService(
            gateway,
            new StubCommercialRepository(),
            TimeProvider.System);

        var result = await service.CreateAsync(
            "visitor@example.com",
            "annual-plan",
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal("invalid_product", result.ErrorCode);
        Assert.False(gateway.WasCalled);
    }

    [Fact]
    public async Task CheckoutServiceReturnsCheckoutUrlAndPersistsSession()
    {
        var gateway = new StubPaymentGateway();
        var repository = new StubCommercialRepository();
        var service = new CheckoutService(
            gateway,
            repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero)));

        var result = await service.CreateAsync(
            "visitor@example.com",
            "visit-pass",
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("https://checkout.stripe.test/session", result.CheckoutUrl);
        Assert.Equal("visit-pass", repository.Product!.Code);
    }

    private sealed class StubWaitlistRepository(bool result) : IWaitlistRepository
    {
        public EmailAddress? EmailAddress { get; private set; }

        public Task<bool> RegisterAsync(
            EmailAddress emailAddress,
            DateTimeOffset registeredAt,
            CancellationToken cancellationToken)
        {
            EmailAddress = emailAddress;
            return Task.FromResult(result);
        }
    }

    private sealed class StubPaymentGateway : IPaymentCheckoutGateway
    {
        public bool WasCalled { get; private set; }

        public Task<PaymentCheckoutSession> CreateAsync(
            EmailAddress emailAddress,
            CommercialProduct product,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new PaymentCheckoutSession(
                "cs_test_draft",
                "https://checkout.stripe.test/session"));
        }
    }

    private sealed class StubCommercialRepository : ICommercialRepository
    {
        public CommercialProduct? Product { get; private set; }

        public Task SaveCheckoutSessionAsync(
            PaymentCheckoutSession checkoutSession,
            EmailAddress emailAddress,
            CommercialProduct product,
            DateTimeOffset createdAt,
            CancellationToken cancellationToken)
        {
            Product = product;
            return Task.CompletedTask;
        }

        public Task<bool> ApplyPaymentEventAsync(
            PaymentEvent paymentEvent,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
