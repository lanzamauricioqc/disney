using System.Data.Common;
using Dapper;
using Disney.Application;
using Disney.Domain;

namespace Disney.Infrastructure;

internal sealed partial class PostgreSqlCompanyRepository
{
    public async Task<IReadOnlyList<CompanyCustomer>> SearchCustomersAsync(
        Guid organizationId,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var customers = await connection.QueryAsync<CompanyCustomer>(new CommandDefinition(
            """
            SELECT id AS Id,
                   organization_id AS OrganizationId,
                   name AS Name,
                   email AS Email,
                   phone AS Phone,
                   external_reference AS ExternalReference,
                   notes AS Notes,
                   is_active AS IsActive,
                   created_at AS CreatedAt,
                   updated_at AS UpdatedAt
            FROM public.company_customers
            WHERE organization_id = @OrganizationId
              AND is_active
              AND (
                    @Search IS NULL
                    OR name ILIKE '%' || @Search || '%'
                    OR email ILIKE '%' || @Search || '%'
                    OR phone ILIKE '%' || @Search || '%'
                    OR external_reference ILIKE '%' || @Search || '%'
              )
            ORDER BY name
            LIMIT @Limit;
            """,
            new { OrganizationId = organizationId, Search = search, Limit = limit },
            cancellationToken: cancellationToken));
        return customers.AsList();
    }

    public async Task<CompanyCustomer?> GetCustomerAsync(
        Guid organizationId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await QueryCustomerAsync(
            connection,
            organizationId,
            customerId,
            cancellationToken);
    }

    public async Task<CompanyCustomer> CreateCustomerAsync(
        CompanyActor actor,
        Guid customerId,
        CompanyCustomerInput input,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_customers
                (id, organization_id, name, email, phone, external_reference,
                 notes, is_active, created_at, updated_at)
            VALUES
                (@CustomerId, @OrganizationId, @Name, @Email, @Phone,
                 @ExternalReference, @Notes, true, @CreatedAt, @CreatedAt);
            """,
            new
            {
                CustomerId = customerId,
                actor.OrganizationId,
                input.Name,
                input.Email,
                input.Phone,
                input.ExternalReference,
                input.Notes,
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));
        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "customer.created",
            "customer",
            customerId.ToString(),
            new { input.Name, input.ExternalReference },
            createdAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await QueryCustomerAsync(
            connection,
            actor.OrganizationId,
            customerId,
            cancellationToken))!;
    }

    public async Task<CompanyCustomer?> UpdateCustomerAsync(
        CompanyActor actor,
        Guid customerId,
        CompanyCustomerInput input,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_customers
            SET name = @Name,
                email = @Email,
                phone = @Phone,
                external_reference = @ExternalReference,
                notes = @Notes,
                updated_at = @UpdatedAt
            WHERE id = @CustomerId
              AND organization_id = @OrganizationId
              AND is_active;
            """,
            new
            {
                CustomerId = customerId,
                actor.OrganizationId,
                input.Name,
                input.Email,
                input.Phone,
                input.ExternalReference,
                input.Notes,
                UpdatedAt = updatedAt
            },
            transaction,
            cancellationToken: cancellationToken));
        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "customer.updated",
            "customer",
            customerId.ToString(),
            new { input.Name, input.ExternalReference },
            updatedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await QueryCustomerAsync(
            connection,
            actor.OrganizationId,
            customerId,
            cancellationToken);
    }

    public async Task<bool> DeleteCustomerAsync(
        CompanyActor actor,
        Guid customerId,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_customers
            SET is_active = false,
                deleted_at = @DeletedAt,
                updated_at = @DeletedAt
            WHERE id = @CustomerId
              AND organization_id = @OrganizationId
              AND is_active;
            """,
            new
            {
                CustomerId = customerId,
                actor.OrganizationId,
                DeletedAt = deletedAt
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
            "customer.deactivated",
            "customer",
            customerId.ToString(),
            new { },
            deletedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CompanyVisit>> ListVisitsAsync(
        Guid organizationId,
        DateOnly? from,
        DateOnly? to,
        CompanyVisitStatus? status,
        Guid? customerId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var visits = await connection.QueryAsync<CompanyVisit>(new CommandDefinition(
            VisitSelect + Environment.NewLine +
            """
            WHERE visit.organization_id = @OrganizationId
              AND visit.is_active
              AND (@From IS NULL OR visit.visit_date >= @From)
              AND (@To IS NULL OR visit.visit_date <= @To)
              AND (@Status IS NULL OR visit.status = @Status)
              AND (@CustomerId IS NULL OR visit.customer_id = @CustomerId)
            ORDER BY visit.visit_date, visit.created_at
            LIMIT @Limit;
            """,
            new
            {
                OrganizationId = organizationId,
                From = from,
                To = to,
                Status = status.HasValue ? ToDatabase(status.Value) : null,
                CustomerId = customerId,
                Limit = limit
            },
            cancellationToken: cancellationToken));
        return visits.AsList();
    }

    public async Task<CompanyVisit?> GetVisitAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await QueryVisitAsync(connection, organizationId, visitId, cancellationToken);
    }

    public async Task<VisitEntitlement?> GetVisitEntitlementAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<VisitEntitlement>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       visit_id AS VisitId,
                       credit_ledger_entry_id AS CreditLedgerEntryId,
                       assigned_by_user_id AS AssignedByUserId,
                       assigned_at AS AssignedAt
                FROM public.company_visit_entitlements
                WHERE organization_id = @OrganizationId
                  AND visit_id = @VisitId;
                """,
                new { OrganizationId = organizationId, VisitId = visitId },
                cancellationToken: cancellationToken));
    }

    public async Task<VisitEntitlement?> AssignVisitEntitlementAsync(
        CompanyActor actor,
        Guid visitId,
        Guid entitlementId,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var lockedOrganizationId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                """
                SELECT id
                FROM public.company_organizations
                WHERE id = @OrganizationId
                FOR UPDATE;
                """,
                new { actor.OrganizationId },
                transaction,
                cancellationToken: cancellationToken));
        if (!lockedOrganizationId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var ledgerEntryId = await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(
                """
                INSERT INTO public.company_credit_ledger
                    (organization_id, amount, reason, source, source_reference,
                     created_by_user_id, created_at)
                SELECT
                    @OrganizationId, -1, 'Visit entitlement assigned',
                    'visit_entitlement', @VisitId::text, @UserId, @AssignedAt
                WHERE EXISTS (
                    SELECT 1
                    FROM public.company_visits
                    WHERE id = @VisitId
                      AND organization_id = @OrganizationId
                      AND is_active
                )
                  AND NOT EXISTS (
                    SELECT 1
                    FROM public.company_visit_entitlements
                    WHERE visit_id = @VisitId
                      AND organization_id = @OrganizationId
                )
                  AND (
                    SELECT COALESCE(SUM(amount), 0)
                    FROM public.company_credit_ledger
                    WHERE organization_id = @OrganizationId
                  ) > 0
                ON CONFLICT (organization_id, source, source_reference)
                    WHERE source_reference IS NOT NULL
                    DO NOTHING
                RETURNING id;
                """,
                new
                {
                    actor.OrganizationId,
                    VisitId = visitId,
                    UserId = actor.UserId,
                    AssignedAt = assignedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        if (!ledgerEntryId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var entitlement = await connection.QuerySingleAsync<VisitEntitlement>(
            new CommandDefinition(
                """
                INSERT INTO public.company_visit_entitlements
                    (id, organization_id, visit_id, credit_ledger_entry_id,
                     assigned_by_user_id, assigned_at)
                VALUES
                    (@Id, @OrganizationId, @VisitId, @CreditLedgerEntryId,
                     @AssignedByUserId, @AssignedAt)
                RETURNING id AS Id,
                          visit_id AS VisitId,
                          credit_ledger_entry_id AS CreditLedgerEntryId,
                          assigned_by_user_id AS AssignedByUserId,
                          assigned_at AS AssignedAt;
                """,
                new
                {
                    Id = entitlementId,
                    actor.OrganizationId,
                    VisitId = visitId,
                    CreditLedgerEntryId = ledgerEntryId.Value,
                    AssignedByUserId = actor.UserId,
                    AssignedAt = assignedAt
                },
                transaction,
                cancellationToken: cancellationToken));
        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "visit.entitlement_assigned",
            "visit",
            visitId.ToString(),
            new { CreditLedgerEntryId = ledgerEntryId.Value },
            assignedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return entitlement;
    }

    public async Task<CompanyVisit?> CreateVisitAsync(
        CompanyActor actor,
        Guid visitId,
        CompanyVisitInput input,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_visits
                (id, organization_id, customer_id, park_name, visit_date, time_zone,
                 status, party_size, instructions, meeting_point, transportation_details,
                 completed_item_count, total_item_count, external_reference,
                 is_active, created_at, updated_at)
            SELECT
                @VisitId, @OrganizationId, @CustomerId, @ParkName, @VisitDate, @TimeZone,
                @Status, @PartySize, @Instructions, @MeetingPoint, @TransportationDetails,
                @CompletedItemCount, @TotalItemCount, @ExternalReference,
                true, @CreatedAt, @CreatedAt
            WHERE EXISTS (
                SELECT 1
                FROM public.company_customers
                WHERE id = @CustomerId
                  AND organization_id = @OrganizationId
                  AND is_active
            );
            """,
            VisitParameters(actor.OrganizationId, visitId, input, createdAt),
            transaction,
            cancellationToken: cancellationToken));
        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "visit.created",
            "visit",
            visitId.ToString(),
            new { input.ParkName, input.VisitDate, input.ExternalReference },
            createdAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await QueryVisitAsync(connection, actor.OrganizationId, visitId, cancellationToken);
    }

    public async Task<CompanyVisit?> UpdateVisitAsync(
        CompanyActor actor,
        Guid visitId,
        CompanyVisitInput input,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_visits visit
            SET customer_id = @CustomerId,
                park_name = @ParkName,
                visit_date = @VisitDate,
                time_zone = @TimeZone,
                status = @Status,
                party_size = @PartySize,
                instructions = @Instructions,
                meeting_point = @MeetingPoint,
                transportation_details = @TransportationDetails,
                completed_item_count = @CompletedItemCount,
                total_item_count = @TotalItemCount,
                external_reference = @ExternalReference,
                updated_at = @UpdatedAt
            WHERE visit.id = @VisitId
              AND visit.organization_id = @OrganizationId
              AND visit.is_active
              AND EXISTS (
                    SELECT 1
                    FROM public.company_customers customer
                    WHERE customer.id = @CustomerId
                      AND customer.organization_id = @OrganizationId
                      AND customer.is_active
              );
            """,
            VisitParameters(actor.OrganizationId, visitId, input, updatedAt),
            transaction,
            cancellationToken: cancellationToken));
        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "visit.updated",
            "visit",
            visitId.ToString(),
            new { input.Status, input.CompletedItemCount, input.TotalItemCount },
            updatedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await QueryVisitAsync(connection, actor.OrganizationId, visitId, cancellationToken);
    }

    public async Task<bool> DeleteVisitAsync(
        CompanyActor actor,
        Guid visitId,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_visits
            SET is_active = false,
                deleted_at = @DeletedAt,
                updated_at = @DeletedAt
            WHERE id = @VisitId
              AND organization_id = @OrganizationId
              AND is_active;

            UPDATE public.company_visitor_access_links
            SET revoked_at = COALESCE(revoked_at, @DeletedAt)
            WHERE visit_id = @VisitId
              AND organization_id = @OrganizationId
              AND revoked_at IS NULL;
            """,
            new
            {
                VisitId = visitId,
                actor.OrganizationId,
                DeletedAt = deletedAt
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
            "visit.deactivated",
            "visit",
            visitId.ToString(),
            new { },
            deletedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<CompanyVisit?> UpdateVisitProgressAsync(
        CompanyActor actor,
        Guid visitId,
        VisitProgressUpdate update,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_visits
            SET status = @Status,
                completed_item_count = @CompletedItemCount,
                total_item_count = @TotalItemCount,
                updated_at = @UpdatedAt
            WHERE id = @VisitId
              AND organization_id = @OrganizationId
              AND is_active;
            """,
            new
            {
                Status = ToDatabase(update.Status),
                update.CompletedItemCount,
                update.TotalItemCount,
                UpdatedAt = updatedAt,
                VisitId = visitId,
                actor.OrganizationId
            },
            transaction,
            cancellationToken: cancellationToken));
        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "visit.progress_updated",
            "visit",
            visitId.ToString(),
            update,
            updatedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await QueryVisitAsync(connection, actor.OrganizationId, visitId, cancellationToken);
    }

    public async Task<VisitNote?> AddVisitNoteAsync(
        CompanyActor actor,
        Guid visitId,
        Guid noteId,
        string note,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var result = await connection.QuerySingleOrDefaultAsync<VisitNote>(new CommandDefinition(
            """
            INSERT INTO public.company_visit_notes
                (id, organization_id, visit_id, author_user_id, note, created_at)
            SELECT
                @NoteId, @OrganizationId, @VisitId, @AuthorUserId, @Note, @CreatedAt
            WHERE EXISTS (
                SELECT 1 FROM public.company_visits
                WHERE id = @VisitId
                  AND organization_id = @OrganizationId
                  AND is_active
            )
            RETURNING id AS Id,
                      visit_id AS VisitId,
                      author_user_id AS AuthorUserId,
                      note AS Note,
                      created_at AS CreatedAt;
            """,
            new
            {
                NoteId = noteId,
                actor.OrganizationId,
                VisitId = visitId,
                AuthorUserId = actor.UserId,
                Note = note,
                CreatedAt = createdAt
            },
            cancellationToken: cancellationToken));
        return result;
    }

    public async Task<IReadOnlyList<VisitNote>> ListVisitNotesAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var notes = await connection.QueryAsync<VisitNote>(new CommandDefinition(
            """
            SELECT note.id AS Id,
                   note.visit_id AS VisitId,
                   note.author_user_id AS AuthorUserId,
                   note.note AS Note,
                   note.created_at AS CreatedAt
            FROM public.company_visit_notes note
            JOIN public.company_visits visit
              ON visit.organization_id = note.organization_id
             AND visit.id = note.visit_id
            WHERE note.organization_id = @OrganizationId
              AND note.visit_id = @VisitId
              AND visit.is_active
            ORDER BY note.created_at DESC;
            """,
            new { OrganizationId = organizationId, VisitId = visitId },
            cancellationToken: cancellationToken));
        return notes.AsList();
    }

    public async Task<VisitOverride?> AddVisitOverrideAsync(
        CompanyActor actor,
        Guid visitId,
        Guid overrideId,
        string summary,
        string detailsJson,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<VisitOverride>(new CommandDefinition(
            """
            INSERT INTO public.company_visit_overrides
                (id, organization_id, visit_id, created_by_user_id, summary,
                 details_json, created_at)
            SELECT
                @OverrideId, @OrganizationId, @VisitId, @CreatedByUserId, @Summary,
                CAST(@DetailsJson AS jsonb), @CreatedAt
            WHERE EXISTS (
                SELECT 1 FROM public.company_visits
                WHERE id = @VisitId
                  AND organization_id = @OrganizationId
                  AND is_active
            )
            RETURNING id AS Id,
                      visit_id AS VisitId,
                      created_by_user_id AS CreatedByUserId,
                      summary AS Summary,
                      details_json::text AS DetailsJson,
                      created_at AS CreatedAt;
            """,
            new
            {
                OverrideId = overrideId,
                actor.OrganizationId,
                VisitId = visitId,
                CreatedByUserId = actor.UserId,
                Summary = summary,
                DetailsJson = detailsJson,
                CreatedAt = createdAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<VisitOverride>> ListVisitOverridesAsync(
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var overrides = await connection.QueryAsync<VisitOverride>(new CommandDefinition(
            """
            SELECT override_record.id AS Id,
                   override_record.visit_id AS VisitId,
                   override_record.created_by_user_id AS CreatedByUserId,
                   override_record.summary AS Summary,
                   override_record.details_json::text AS DetailsJson,
                   override_record.created_at AS CreatedAt
            FROM public.company_visit_overrides override_record
            JOIN public.company_visits visit
              ON visit.organization_id = override_record.organization_id
             AND visit.id = override_record.visit_id
            WHERE override_record.organization_id = @OrganizationId
              AND override_record.visit_id = @VisitId
              AND visit.is_active
            ORDER BY override_record.created_at DESC;
            """,
            new { OrganizationId = organizationId, VisitId = visitId },
            cancellationToken: cancellationToken));
        return overrides.AsList();
    }

    public async Task<VisitorAccessLink?> CreateVisitorAccessLinkAsync(
        CompanyActor actor,
        Guid visitId,
        Guid accessLinkId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<VisitorAccessLink>(new CommandDefinition(
            """
            INSERT INTO public.company_visitor_access_links
                (id, organization_id, visit_id, token_hash, expires_at, created_at)
            SELECT
                @AccessLinkId, @OrganizationId, @VisitId, @TokenHash, @ExpiresAt, @CreatedAt
            WHERE EXISTS (
                SELECT 1 FROM public.company_visits
                WHERE id = @VisitId
                  AND organization_id = @OrganizationId
                  AND is_active
            )
            RETURNING id AS Id,
                      visit_id AS VisitId,
                      expires_at AS ExpiresAt,
                      created_at AS CreatedAt,
                      revoked_at AS RevokedAt;
            """,
            new
            {
                AccessLinkId = accessLinkId,
                actor.OrganizationId,
                VisitId = visitId,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                CreatedAt = createdAt
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> RevokeVisitorAccessLinksAsync(
        CompanyActor actor,
        Guid visitId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_visitor_access_links
            SET revoked_at = @RevokedAt
            WHERE organization_id = @OrganizationId
              AND visit_id = @VisitId
              AND revoked_at IS NULL;
            """,
            new
            {
                RevokedAt = revokedAt,
                actor.OrganizationId,
                VisitId = visitId
            },
            cancellationToken: cancellationToken));
        return changed > 0;
    }

    public async Task<VisitorVisit?> ResolveVisitorAccessAsync(
        string tokenHash,
        DateTimeOffset accessedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var visit = await connection.QuerySingleOrDefaultAsync<VisitorVisit>(new CommandDefinition(
            """
            SELECT visit.id AS VisitId,
                   customer.name AS CustomerName,
                   visit.park_name AS ParkName,
                   visit.visit_date AS VisitDate,
                   visit.time_zone AS TimeZone,
                   CASE visit.status
                       WHEN 'planned' THEN 0
                       WHEN 'active' THEN 1
                       WHEN 'completed' THEN 2
                       WHEN 'cancelled' THEN 3
                   END AS Status,
                   visit.party_size AS PartySize,
                   visit.instructions AS Instructions,
                   visit.meeting_point AS MeetingPoint,
                   visit.transportation_details AS TransportationDetails,
                   visit.completed_item_count AS CompletedItemCount,
                   visit.total_item_count AS TotalItemCount,
                   access.expires_at AS AccessExpiresAt
            FROM public.company_visitor_access_links access
            JOIN public.company_visits visit
              ON visit.organization_id = access.organization_id
             AND visit.id = access.visit_id
            JOIN public.company_customers customer
              ON customer.organization_id = visit.organization_id
             AND customer.id = visit.customer_id
            WHERE access.token_hash = @TokenHash
              AND access.revoked_at IS NULL
              AND access.expires_at > @AccessedAt
              AND visit.is_active
              AND customer.is_active
            FOR UPDATE OF access;
            """,
            new { TokenHash = tokenHash, AccessedAt = accessedAt },
            transaction,
            cancellationToken: cancellationToken));
        if (visit is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_visitor_access_links
            SET last_accessed_at = @AccessedAt
            WHERE token_hash = @TokenHash;
            """,
            new { AccessedAt = accessedAt, TokenHash = tokenHash },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return visit;
    }

    private static object VisitParameters(
        Guid organizationId,
        Guid visitId,
        CompanyVisitInput input,
        DateTimeOffset timestamp) =>
        new
        {
            VisitId = visitId,
            OrganizationId = organizationId,
            input.CustomerId,
            input.ParkName,
            input.VisitDate,
            input.TimeZone,
            Status = ToDatabase(input.Status),
            input.PartySize,
            input.Instructions,
            input.MeetingPoint,
            input.TransportationDetails,
            input.CompletedItemCount,
            input.TotalItemCount,
            input.ExternalReference,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };

    private static Task<CompanyCustomer?> QueryCustomerAsync(
        DbConnection connection,
        Guid organizationId,
        Guid customerId,
        CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<CompanyCustomer>(new CommandDefinition(
            """
            SELECT id AS Id,
                   organization_id AS OrganizationId,
                   name AS Name,
                   email AS Email,
                   phone AS Phone,
                   external_reference AS ExternalReference,
                   notes AS Notes,
                   is_active AS IsActive,
                   created_at AS CreatedAt,
                   updated_at AS UpdatedAt
            FROM public.company_customers
            WHERE id = @CustomerId
              AND organization_id = @OrganizationId
              AND is_active;
            """,
            new { CustomerId = customerId, OrganizationId = organizationId },
            cancellationToken: cancellationToken));

    private static Task<CompanyVisit?> QueryVisitAsync(
        DbConnection connection,
        Guid organizationId,
        Guid visitId,
        CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<CompanyVisit>(new CommandDefinition(
            VisitSelect + Environment.NewLine +
            """
            WHERE visit.id = @VisitId
              AND visit.organization_id = @OrganizationId
              AND visit.is_active;
            """,
            new { VisitId = visitId, OrganizationId = organizationId },
            cancellationToken: cancellationToken));

    private const string VisitSelect =
        """
        SELECT visit.id AS Id,
               visit.organization_id AS OrganizationId,
               visit.customer_id AS CustomerId,
               customer.name AS CustomerName,
               visit.park_name AS ParkName,
               visit.visit_date AS VisitDate,
               visit.time_zone AS TimeZone,
               CASE visit.status
                   WHEN 'planned' THEN 0
                   WHEN 'active' THEN 1
                   WHEN 'completed' THEN 2
                   WHEN 'cancelled' THEN 3
               END AS Status,
               visit.party_size AS PartySize,
               visit.instructions AS Instructions,
               visit.meeting_point AS MeetingPoint,
               visit.transportation_details AS TransportationDetails,
               visit.completed_item_count AS CompletedItemCount,
               visit.total_item_count AS TotalItemCount,
               visit.external_reference AS ExternalReference,
               visit.created_at AS CreatedAt,
               visit.updated_at AS UpdatedAt
        FROM public.company_visits visit
        JOIN public.company_customers customer
          ON customer.organization_id = visit.organization_id
         AND customer.id = visit.customer_id
        """;
}
