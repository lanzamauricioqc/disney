using Dapper;
using Disney.Application;
using Disney.Domain;

namespace Disney.Infrastructure;

internal sealed class PostgreSqlCommercialRepository(
    PostgreSqlConnectionFactory connectionFactory) :
    IWaitlistRepository,
    ICommercialRepository
{
    public async Task<bool> RegisterAsync(
        EmailAddress emailAddress,
        DateTimeOffset registeredAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var registrationId = await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(
                """
                INSERT INTO public.waitlist_registrations (email, registered_at)
                VALUES (@Email, @RegisteredAt)
                ON CONFLICT (email) DO NOTHING
                RETURNING id;
                """,
                new
                {
                    Email = emailAddress.Value,
                    RegisteredAt = registeredAt
                },
                cancellationToken: cancellationToken));
        return registrationId.HasValue;
    }

    public async Task SaveCheckoutSessionAsync(
        PaymentCheckoutSession checkoutSession,
        EmailAddress emailAddress,
        CommercialProduct product,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.payment_checkout_sessions
                (provider_session_id, email, product_code, status, amount_expected,
                 currency, created_at, updated_at)
            VALUES
                (@ProviderSessionId, @Email, @ProductCode, 'pending',
                 @AmountExpected, 'usd', @CreatedAt, @CreatedAt)
            ON CONFLICT (provider_session_id) DO NOTHING;
            """,
            new
            {
                checkoutSession.ProviderSessionId,
                Email = emailAddress.Value,
                ProductCode = product.Code,
                AmountExpected = product.DisplayPriceInCents,
                CreatedAt = createdAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ApplyPaymentEventAsync(
        PaymentEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var insertedEventId = await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(
                """
                INSERT INTO public.payment_events
                    (provider_event_id, event_type, provider_session_id, occurred_at)
                VALUES
                    (@ProviderEventId, @EventType, @ProviderSessionId, @OccurredAt)
                ON CONFLICT (provider_event_id) DO NOTHING
                RETURNING id;
                """,
                paymentEvent,
                transaction,
                cancellationToken: cancellationToken));
        if (!insertedEventId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.payment_checkout_sessions
            SET status = @Status,
                provider_customer_id = COALESCE(@ProviderCustomerId, provider_customer_id),
                provider_payment_intent_id =
                    COALESCE(@ProviderPaymentIntentId, provider_payment_intent_id),
                amount_paid = COALESCE(@AmountTotal, amount_paid),
                currency = COALESCE(@Currency, currency),
                updated_at = @OccurredAt
            WHERE provider_session_id = @ProviderSessionId;
            """,
            new
            {
                Status = paymentEvent.Status.ToString().ToLowerInvariant(),
                paymentEvent.ProviderCustomerId,
                paymentEvent.ProviderPaymentIntentId,
                paymentEvent.AmountTotal,
                paymentEvent.Currency,
                paymentEvent.OccurredAt,
                paymentEvent.ProviderSessionId
            },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
