using Dapper;
using Disney.Application;
using Disney.Domain;

namespace Disney.Infrastructure;

internal sealed partial class PostgreSqlCompanyRepository
{
    public async Task<CreditBalance> GetCreditBalanceAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<CreditBalance>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(amount), 0)::integer AS Remaining,
                   COALESCE(SUM(CASE WHEN amount < 0 THEN -amount ELSE 0 END), 0)::integer
                       AS Consumed
            FROM public.company_credit_ledger
            WHERE organization_id = @OrganizationId;
            """,
            new { OrganizationId = organizationId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CreditLedgerEntry>> ListCreditLedgerAsync(
        Guid organizationId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var entries = await connection.QueryAsync<CreditLedgerEntry>(new CommandDefinition(
            """
            SELECT id AS Id,
                   organization_id AS OrganizationId,
                   amount AS Amount,
                   reason AS Reason,
                   source AS Source,
                   source_reference AS SourceReference,
                   created_by_user_id AS CreatedByUserId,
                   created_at AS CreatedAt
            FROM public.company_credit_ledger
            WHERE organization_id = @OrganizationId
            ORDER BY created_at DESC, id DESC
            LIMIT @Limit;
            """,
            new { OrganizationId = organizationId, Limit = limit },
            cancellationToken: cancellationToken));
        return entries.AsList();
    }

    public async Task<CreditLedgerEntry> AddCreditAdjustmentAsync(
        CompanyActor actor,
        int amount,
        string reason,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var entry = await connection.QuerySingleAsync<CreditLedgerEntry>(new CommandDefinition(
            """
            INSERT INTO public.company_credit_ledger
                (organization_id, amount, reason, source, created_by_user_id, created_at)
            VALUES
                (@OrganizationId, @Amount, @Reason, 'manual', @CreatedByUserId, @CreatedAt)
            RETURNING id AS Id,
                      organization_id AS OrganizationId,
                      amount AS Amount,
                      reason AS Reason,
                      source AS Source,
                      source_reference AS SourceReference,
                      created_by_user_id AS CreatedByUserId,
                      created_at AS CreatedAt;
            """,
            new
            {
                actor.OrganizationId,
                Amount = amount,
                Reason = reason,
                CreatedByUserId = actor.UserId,
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));
        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "credits.adjusted",
            "credit_ledger",
            entry.Id.ToString(),
            new { Amount = amount, Reason = reason },
            createdAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return entry;
    }

    public async Task SaveCompanyCheckoutSessionAsync(
        Guid organizationId,
        CompanyCheckoutSession session,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_checkout_sessions
                (organization_id, provider_session_id, bundle_code, credits, status,
                 created_at, updated_at)
            VALUES
                (@OrganizationId, @ProviderSessionId, @BundleCode, @Credits, 'pending',
                 @CreatedAt, @CreatedAt)
            ON CONFLICT (provider_session_id) DO NOTHING;
            """,
            new
            {
                OrganizationId = organizationId,
                session.ProviderSessionId,
                session.BundleCode,
                session.Credits,
                CreatedAt = createdAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ApplyCompanyPaymentAsync(
        CompanyPaymentEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var eventInserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_payment_events
                (provider_event_id, provider_session_id, event_type, occurred_at)
            VALUES
                (@ProviderEventId, @ProviderSessionId, @EventType, @OccurredAt)
            ON CONFLICT (provider_event_id) DO NOTHING;
            """,
            paymentEvent,
            transaction,
            cancellationToken: cancellationToken));
        if (eventInserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var checkout = await connection.QuerySingleOrDefaultAsync<CompanyCheckoutRow>(
            new CommandDefinition(
                """
                UPDATE public.company_checkout_sessions
                SET status = 'paid',
                    amount_paid = @AmountPaid,
                    currency = @Currency,
                    updated_at = @OccurredAt
                WHERE provider_session_id = @ProviderSessionId
                RETURNING organization_id AS OrganizationId,
                          credits AS Credits,
                          bundle_code AS BundleCode;
                """,
                paymentEvent,
                transaction,
                cancellationToken: cancellationToken));
        if (checkout is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_credit_ledger
                (organization_id, amount, reason, source, source_reference, created_at)
            VALUES
                (@OrganizationId, @Credits, @Reason, 'stripe_checkout',
                 @ProviderSessionId, @OccurredAt)
            ON CONFLICT (organization_id, source, source_reference)
                WHERE source_reference IS NOT NULL
                DO NOTHING;
            """,
            new
            {
                checkout.OrganizationId,
                checkout.Credits,
                Reason = $"Credit bundle {checkout.BundleCode}",
                paymentEvent.ProviderSessionId,
                paymentEvent.OccurredAt
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<CompanyDashboard> GetDashboardAsync(
        Guid organizationId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<CompanyDashboard>(new CommandDefinition(
            """
            SELECT
                (SELECT count(*)::integer
                 FROM public.company_customers
                 WHERE organization_id = @OrganizationId AND is_active) AS Customers,
                (SELECT count(*)::integer
                 FROM public.company_visits
                 WHERE organization_id = @OrganizationId
                   AND is_active
                   AND status = 'planned'
                   AND visit_date >= @Today) AS UpcomingVisits,
                (SELECT count(*)::integer
                 FROM public.company_visits
                 WHERE organization_id = @OrganizationId
                   AND is_active
                   AND status = 'active') AS ActiveVisits,
                (SELECT count(*)::integer
                 FROM public.company_visits
                 WHERE organization_id = @OrganizationId
                   AND is_active
                   AND status = 'completed') AS CompletedVisits,
                (SELECT COALESCE(SUM(CASE WHEN amount < 0 THEN -amount ELSE 0 END), 0)::integer
                 FROM public.company_credit_ledger
                 WHERE organization_id = @OrganizationId) AS CreditsConsumed,
                (SELECT COALESCE(SUM(amount), 0)::integer
                 FROM public.company_credit_ledger
                 WHERE organization_id = @OrganizationId) AS CreditsRemaining;
            """,
            new { OrganizationId = organizationId, Today = today },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MonthlyUsage>> GetMonthlyUsageAsync(
        Guid organizationId,
        DateOnly fromMonth,
        DateOnly toMonth,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var usage = await connection.QueryAsync<MonthlyUsage>(new CommandDefinition(
            """
            WITH months AS (
                SELECT generate_series(
                    @FromMonth::date,
                    @ToMonth::date,
                    interval '1 month')::date AS month
            ),
            credit_usage AS (
                SELECT date_trunc('month', created_at)::date AS month,
                       SUM(CASE WHEN amount < 0 THEN -amount ELSE 0 END)::integer AS consumed
                FROM public.company_credit_ledger
                WHERE organization_id = @OrganizationId
                  AND created_at >= @FromMonth::date
                  AND created_at < (@ToMonth::date + interval '1 month')
                GROUP BY date_trunc('month', created_at)::date
            ),
            visit_usage AS (
                SELECT date_trunc('month', created_at)::date AS month,
                       count(*)::integer AS visits
                FROM public.company_visits
                WHERE organization_id = @OrganizationId
                  AND created_at >= @FromMonth::date
                  AND created_at < (@ToMonth::date + interval '1 month')
                GROUP BY date_trunc('month', created_at)::date
            )
            SELECT months.month AS Month,
                   COALESCE(credit_usage.consumed, 0) AS CreditsConsumed,
                   COALESCE(visit_usage.visits, 0) AS VisitsCreated
            FROM months
            LEFT JOIN credit_usage USING (month)
            LEFT JOIN visit_usage USING (month)
            ORDER BY months.month;
            """,
            new
            {
                OrganizationId = organizationId,
                FromMonth = fromMonth,
                ToMonth = toMonth
            },
            cancellationToken: cancellationToken));
        return usage.AsList();
    }

    public async Task<CompanyApiKey> CreateApiKeyAsync(
        CompanyActor actor,
        Guid apiKeyId,
        string name,
        string prefix,
        string secretHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var apiKey = await connection.QuerySingleAsync<CompanyApiKey>(new CommandDefinition(
            """
            INSERT INTO public.company_api_keys
                (id, organization_id, name, key_prefix, secret_hash, created_by_user_id,
                 created_at)
            VALUES
                (@ApiKeyId, @OrganizationId, @Name, @Prefix, @SecretHash,
                 @CreatedByUserId, @CreatedAt)
            RETURNING id AS Id,
                      name AS Name,
                      key_prefix AS Prefix,
                      created_at AS CreatedAt,
                      last_used_at AS LastUsedAt,
                      revoked_at AS RevokedAt;
            """,
            new
            {
                ApiKeyId = apiKeyId,
                actor.OrganizationId,
                Name = name,
                Prefix = prefix,
                SecretHash = secretHash,
                CreatedByUserId = actor.UserId,
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));
        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "integration.api_key_created",
            "api_key",
            apiKeyId.ToString(),
            new { Name = name, Prefix = prefix },
            createdAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return apiKey;
    }

    public async Task<IReadOnlyList<CompanyApiKey>> ListApiKeysAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var apiKeys = await connection.QueryAsync<CompanyApiKey>(new CommandDefinition(
            """
            SELECT id AS Id,
                   name AS Name,
                   key_prefix AS Prefix,
                   created_at AS CreatedAt,
                   last_used_at AS LastUsedAt,
                   revoked_at AS RevokedAt
            FROM public.company_api_keys
            WHERE organization_id = @OrganizationId
            ORDER BY created_at DESC;
            """,
            new { OrganizationId = organizationId },
            cancellationToken: cancellationToken));
        return apiKeys.AsList();
    }

    public async Task<bool> RevokeApiKeyAsync(
        CompanyActor actor,
        Guid apiKeyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_api_keys
            SET revoked_at = @RevokedAt
            WHERE id = @ApiKeyId
              AND organization_id = @OrganizationId
              AND revoked_at IS NULL;
            """,
            new
            {
                RevokedAt = revokedAt,
                ApiKeyId = apiKeyId,
                actor.OrganizationId
            },
            transaction,
            cancellationToken: cancellationToken));
        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "integration.api_key_revoked",
            "api_key",
            apiKeyId.ToString(),
            new { },
            revokedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<Guid?> AuthenticateApiKeyAsync(
        string secretHash,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            UPDATE public.company_api_keys
            SET last_used_at = @UsedAt
            WHERE secret_hash = @SecretHash
              AND revoked_at IS NULL
            RETURNING organization_id;
            """,
            new { SecretHash = secretHash, UsedAt = usedAt },
            cancellationToken: cancellationToken));
    }

    public async Task<ReservationImportResult> UpsertReservationAsync(
        Guid organizationId,
        ReservationImport reservation,
        DateTimeOffset importedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var customerId = Guid.NewGuid();
        var insertedCustomerId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                """
                INSERT INTO public.company_customers
                    (id, organization_id, name, email, phone, external_reference,
                     notes, is_active, created_at, updated_at)
                VALUES
                    (@CustomerId, @OrganizationId, @Name, @Email, @Phone,
                     @ExternalReference, @Notes, true, @ImportedAt, @ImportedAt)
                ON CONFLICT (organization_id, external_reference)
                    WHERE external_reference IS NOT NULL AND is_active
                    DO NOTHING
                RETURNING id;
                """,
                new
                {
                    CustomerId = customerId,
                    OrganizationId = organizationId,
                    Name = reservation.CustomerName.Trim(),
                    Email = NormalizeEmail(reservation.CustomerEmail),
                    Phone = CleanOptional(reservation.CustomerPhone),
                    ExternalReference = reservation.CustomerExternalReference.Trim(),
                    Notes = CleanOptional(reservation.CustomerNotes),
                    ImportedAt = importedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        var customerCreated = insertedCustomerId.HasValue;
        if (!customerCreated)
        {
            customerId = await connection.QuerySingleAsync<Guid>(new CommandDefinition(
                """
                UPDATE public.company_customers
                SET name = @Name,
                    email = @Email,
                    phone = @Phone,
                    notes = @Notes,
                    updated_at = @ImportedAt
                WHERE organization_id = @OrganizationId
                  AND external_reference = @ExternalReference
                  AND is_active
                RETURNING id;
                """,
                new
                {
                    OrganizationId = organizationId,
                    Name = reservation.CustomerName.Trim(),
                    Email = NormalizeEmail(reservation.CustomerEmail),
                    Phone = CleanOptional(reservation.CustomerPhone),
                    ExternalReference = reservation.CustomerExternalReference.Trim(),
                    Notes = CleanOptional(reservation.CustomerNotes),
                    ImportedAt = importedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        var visitId = Guid.NewGuid();
        var insertedVisitId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                """
                INSERT INTO public.company_visits
                    (id, organization_id, customer_id, park_name, visit_date, time_zone,
                     status, party_size, instructions, meeting_point,
                     transportation_details, external_reference, is_active,
                     created_at, updated_at)
                VALUES
                    (@VisitId, @OrganizationId, @CustomerId, @ParkName, @VisitDate,
                     @TimeZone, 'planned', @PartySize, @Instructions, @MeetingPoint,
                     @TransportationDetails, @ExternalReference, true,
                     @ImportedAt, @ImportedAt)
                ON CONFLICT (organization_id, external_reference)
                    WHERE external_reference IS NOT NULL AND is_active
                    DO NOTHING
                RETURNING id;
                """,
                new
                {
                    VisitId = visitId,
                    OrganizationId = organizationId,
                    CustomerId = customerId,
                    ParkName = reservation.ParkName.Trim(),
                    reservation.VisitDate,
                    TimeZone = reservation.TimeZone.Trim(),
                    reservation.PartySize,
                    Instructions = CleanOptional(reservation.Instructions),
                    MeetingPoint = CleanOptional(reservation.MeetingPoint),
                    TransportationDetails = CleanOptional(reservation.TransportationDetails),
                    ExternalReference = reservation.ReservationExternalReference.Trim(),
                    ImportedAt = importedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        var visitCreated = insertedVisitId.HasValue;
        if (!visitCreated)
        {
            visitId = await connection.QuerySingleAsync<Guid>(new CommandDefinition(
                """
                UPDATE public.company_visits
                SET customer_id = @CustomerId,
                    park_name = @ParkName,
                    visit_date = @VisitDate,
                    time_zone = @TimeZone,
                    party_size = @PartySize,
                    instructions = @Instructions,
                    meeting_point = @MeetingPoint,
                    transportation_details = @TransportationDetails,
                    updated_at = @ImportedAt
                WHERE organization_id = @OrganizationId
                  AND external_reference = @ExternalReference
                  AND is_active
                RETURNING id;
                """,
                new
                {
                    OrganizationId = organizationId,
                    CustomerId = customerId,
                    ParkName = reservation.ParkName.Trim(),
                    reservation.VisitDate,
                    TimeZone = reservation.TimeZone.Trim(),
                    reservation.PartySize,
                    Instructions = CleanOptional(reservation.Instructions),
                    MeetingPoint = CleanOptional(reservation.MeetingPoint),
                    TransportationDetails = CleanOptional(reservation.TransportationDetails),
                    ExternalReference = reservation.ReservationExternalReference.Trim(),
                    ImportedAt = importedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await WriteAuditAsync(
            connection,
            transaction,
            organizationId,
            null,
            "integration.reservation_upserted",
            "visit",
            visitId.ToString(),
            new
            {
                customer_created = customerCreated,
                visit_created = visitCreated,
                reservation_external_reference =
                    reservation.ReservationExternalReference.Trim()
            },
            importedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ReservationImportResult(
            customerId,
            visitId,
            customerCreated,
            visitCreated);
    }

    public async Task<IReadOnlyList<NotificationOutboxItem>> ListNotificationsAsync(
        Guid organizationId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var notifications = await connection.QueryAsync<NotificationOutboxItem>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       channel AS Channel,
                       recipient AS Recipient,
                       template AS Template,
                       CASE status
                           WHEN 'queued' THEN 0
                           WHEN 'processing' THEN 1
                           WHEN 'delivered' THEN 2
                           WHEN 'failed' THEN 3
                       END AS Status,
                       attempts AS Attempts,
                       last_error AS LastError,
                       created_at AS CreatedAt,
                       processed_at AS ProcessedAt
                FROM public.company_notification_outbox
                WHERE organization_id = @OrganizationId
                ORDER BY created_at DESC
                LIMIT @Limit;
                """,
                new { OrganizationId = organizationId, Limit = limit },
                cancellationToken: cancellationToken));
        return notifications.AsList();
    }

    private static string? NormalizeEmail(string? value) =>
        EmailAddress.TryCreate(value, out var email) ? email!.Value : null;

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CompanyCheckoutRow(
        Guid OrganizationId,
        int Credits,
        string BundleCode);
}
