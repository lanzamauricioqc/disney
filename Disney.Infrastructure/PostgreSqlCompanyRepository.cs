using System.Data.Common;
using System.Text.Json;
using Dapper;
using Disney.Application;
using Disney.Domain;

namespace Disney.Infrastructure;

internal sealed partial class PostgreSqlCompanyRepository(
    PostgreSqlConnectionFactory connectionFactory) : ICompanyRepository
{
    public async Task<CompanyUser?> BootstrapAsync(
        string organizationName,
        string normalizedEmail,
        string passwordHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "LOCK TABLE public.company_organizations IN EXCLUSIVE MODE;",
            transaction: transaction,
            cancellationToken: cancellationToken));

        var organizationExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM public.company_organizations);",
                transaction: transaction,
                cancellationToken: cancellationToken));
        if (organizationExists)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_organizations
                (id, name, display_name, created_at, updated_at)
            VALUES (@OrganizationId, @Name, @Name, @CreatedAt, @CreatedAt);

            INSERT INTO public.company_users
                (id, organization_id, email, password_hash, role, is_active,
                 created_at, updated_at)
            VALUES
                (@UserId, @OrganizationId, @Email, @PasswordHash, 'owner', true,
                 @CreatedAt, @CreatedAt);

            INSERT INTO public.company_audit_logs
                (organization_id, actor_user_id, action, entity_type, entity_id, created_at)
            VALUES
                (@OrganizationId, @UserId, 'organization.bootstrapped',
                 'organization', @OrganizationId::text, @CreatedAt);
            """,
            new
            {
                OrganizationId = organizationId,
                UserId = userId,
                Name = organizationName,
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new CompanyUser(
            userId,
            organizationId,
            normalizedEmail,
            CompanyRole.Owner,
            true,
            createdAt);
    }

    public async Task<CompanyAuthenticationUser?> FindAuthenticationUserAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyAuthenticationUser>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       organization_id AS OrganizationId,
                       email AS Email,
                       password_hash AS PasswordHash,
                       CASE role
                           WHEN 'owner' THEN 0
                           WHEN 'administrator' THEN 1
                           WHEN 'planner' THEN 2
                           WHEN 'support' THEN 3
                       END AS Role,
                       is_active AS IsActive,
                       created_at AS CreatedAt
                FROM public.company_users
                WHERE email = @Email;
                """,
                new { Email = normalizedEmail },
                cancellationToken: cancellationToken));
    }

    public async Task<CompanyInvitation?> CreateInvitationAsync(
        CompanyActor actor,
        Guid invitationId,
        string normalizedEmail,
        CompanyRole role,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_invitations
            SET revoked_at = @CreatedAt
            WHERE email = @Email
              AND accepted_at IS NULL
              AND revoked_at IS NULL
              AND expires_at <= @CreatedAt;
            """,
            new { Email = normalizedEmail, CreatedAt = createdAt },
            transaction,
            cancellationToken: cancellationToken));
        var invitation = await connection.QuerySingleOrDefaultAsync<CompanyInvitation>(
            new CommandDefinition(
                """
                INSERT INTO public.company_invitations
                    (id, organization_id, email, role, token_hash, invited_by_user_id,
                     expires_at, created_at)
                SELECT
                    @InvitationId, @OrganizationId, @Email, @Role, @TokenHash, @ActorUserId,
                    @ExpiresAt, @CreatedAt
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM public.company_users
                    WHERE email = @Email
                      AND is_active
                )
                ON CONFLICT DO NOTHING
                RETURNING id AS Id,
                          email AS Email,
                          CASE role
                              WHEN 'owner' THEN 0
                              WHEN 'administrator' THEN 1
                              WHEN 'planner' THEN 2
                              WHEN 'support' THEN 3
                          END AS Role,
                          expires_at AS ExpiresAt,
                          created_at AS CreatedAt,
                          accepted_at AS AcceptedAt,
                          revoked_at AS RevokedAt;
                """,
                new
                {
                    InvitationId = invitationId,
                    actor.OrganizationId,
                    Email = normalizedEmail,
                    Role = ToDatabase(role),
                    TokenHash = tokenHash,
                    ActorUserId = actor.UserId,
                    ExpiresAt = expiresAt,
                    CreatedAt = createdAt
                },
                transaction,
                cancellationToken: cancellationToken));
        if (invitation is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await WriteAuditAsync(
            connection,
            transaction,
            actor,
            "team.invited",
            "invitation",
            invitationId.ToString(),
            new { Email = normalizedEmail, Role = ToDatabase(role) },
            createdAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return invitation;
    }

    public async Task<CompanyUser?> AcceptInvitationAsync(
        string tokenHash,
        string passwordHash,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var invitation = await connection.QuerySingleOrDefaultAsync<InvitationAcceptanceRow>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       organization_id AS OrganizationId,
                       email AS Email,
                       role AS Role,
                       created_at AS CreatedAt
                FROM public.company_invitations
                WHERE token_hash = @TokenHash
                  AND accepted_at IS NULL
                  AND revoked_at IS NULL
                  AND expires_at > @AcceptedAt
                FOR UPDATE;
                """,
                new { TokenHash = tokenHash, AcceptedAt = acceptedAt },
                transaction,
                cancellationToken: cancellationToken));
        if (invitation is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var userId = Guid.NewGuid();
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_users
                (id, organization_id, email, password_hash, role, is_active,
                 created_at, updated_at)
            VALUES
                (@UserId, @OrganizationId, @Email, @PasswordHash, @Role, true,
                 @AcceptedAt, @AcceptedAt)
            ON CONFLICT (email) DO NOTHING;
            """,
            new
            {
                UserId = userId,
                invitation.OrganizationId,
                invitation.Email,
                PasswordHash = passwordHash,
                invitation.Role,
                AcceptedAt = acceptedAt
            },
            transaction,
            cancellationToken: cancellationToken));
        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.company_invitations
            SET accepted_at = @AcceptedAt
            WHERE id = @InvitationId
              AND organization_id = @OrganizationId;
            """,
            new
            {
                AcceptedAt = acceptedAt,
                InvitationId = invitation.Id,
                invitation.OrganizationId
            },
            transaction,
            cancellationToken: cancellationToken));
        await WriteAuditAsync(
            connection,
            transaction,
            new CompanyActor(invitation.OrganizationId, userId, ParseRole(invitation.Role)),
            "team.invitation_accepted",
            "user",
            userId.ToString(),
            new { invitation.Email },
            acceptedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CompanyUser(
            userId,
            invitation.OrganizationId,
            invitation.Email,
            ParseRole(invitation.Role),
            true,
            acceptedAt);
    }

    public async Task<OrganizationDetails?> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await QueryOrganizationAsync(connection, organizationId, cancellationToken);
    }

    public Task<OrganizationDetails?> UpdateOrganizationAsync(
        CompanyActor actor,
        OrganizationUpdate update,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken) =>
        UpdateOrganizationCoreAsync(
            actor,
            """
            UPDATE public.company_organizations
            SET name = @Name,
                support_email = @SupportEmail,
                support_phone = @SupportPhone,
                updated_at = @UpdatedAt
            WHERE id = @OrganizationId;
            """,
            new
            {
                update.Name,
                update.SupportEmail,
                update.SupportPhone,
                UpdatedAt = updatedAt,
                actor.OrganizationId
            },
            "organization.updated",
            updatedAt,
            cancellationToken);

    public Task<OrganizationDetails?> UpdateBrandingAsync(
        CompanyActor actor,
        BrandingUpdate update,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken) =>
        UpdateOrganizationCoreAsync(
            actor,
            """
            UPDATE public.company_organizations
            SET display_name = @DisplayName,
                logo_url = @LogoUrl,
                primary_color = @PrimaryColor,
                secondary_color = @SecondaryColor,
                welcome_message = @WelcomeMessage,
                support_email = @SupportEmail,
                support_phone = @SupportPhone,
                updated_at = @UpdatedAt
            WHERE id = @OrganizationId;
            """,
            new
            {
                update.DisplayName,
                update.LogoUrl,
                update.PrimaryColor,
                update.SecondaryColor,
                update.WelcomeMessage,
                update.SupportEmail,
                update.SupportPhone,
                UpdatedAt = updatedAt,
                actor.OrganizationId
            },
            "organization.branding_updated",
            updatedAt,
            cancellationToken);

    public async Task<IReadOnlyList<CompanyUser>> ListTeamAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var users = await connection.QueryAsync<CompanyUser>(new CommandDefinition(
            """
            SELECT id AS Id,
                   organization_id AS OrganizationId,
                   email AS Email,
                   CASE role
                       WHEN 'owner' THEN 0
                       WHEN 'administrator' THEN 1
                       WHEN 'planner' THEN 2
                       WHEN 'support' THEN 3
                   END AS Role,
                   is_active AS IsActive,
                   created_at AS CreatedAt
            FROM public.company_users
            WHERE organization_id = @OrganizationId
            ORDER BY is_active DESC, email;
            """,
            new { OrganizationId = organizationId },
            cancellationToken: cancellationToken));
        return users.AsList();
    }

    public async Task<IReadOnlyList<CompanyInvitation>> ListInvitationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var invitations = await connection.QueryAsync<CompanyInvitation>(new CommandDefinition(
            """
            SELECT id AS Id,
                   email AS Email,
                   CASE role
                       WHEN 'owner' THEN 0
                       WHEN 'administrator' THEN 1
                       WHEN 'planner' THEN 2
                       WHEN 'support' THEN 3
                   END AS Role,
                   expires_at AS ExpiresAt,
                   created_at AS CreatedAt,
                   accepted_at AS AcceptedAt,
                   revoked_at AS RevokedAt
            FROM public.company_invitations
            WHERE organization_id = @OrganizationId
            ORDER BY created_at DESC;
            """,
            new { OrganizationId = organizationId },
            cancellationToken: cancellationToken));
        return invitations.AsList();
    }

    public Task<bool> ChangeRoleAsync(
        CompanyActor actor,
        Guid userId,
        CompanyRole role,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken) =>
        ChangeTeamMemberAsync(
            actor,
            userId,
            """
            UPDATE public.company_users target
            SET role = @Role,
                updated_at = @ChangedAt
            WHERE target.id = @UserId
              AND target.organization_id = @OrganizationId
              AND target.is_active
              AND (@ActorRole = 'owner' OR target.role <> 'owner')
              AND (
                    target.role <> 'owner'
                    OR (SELECT count(*) FROM public.company_users owners
                        WHERE owners.organization_id = @OrganizationId
                          AND owners.role = 'owner'
                          AND owners.is_active) > 1
              );
            """,
            new
            {
                Role = ToDatabase(role),
                ActorRole = ToDatabase(actor.Role),
                ChangedAt = changedAt,
                UserId = userId,
                actor.OrganizationId
            },
            "team.role_changed",
            new { Role = ToDatabase(role) },
            changedAt,
            cancellationToken);

    public Task<bool> DeactivateUserAsync(
        CompanyActor actor,
        Guid userId,
        DateTimeOffset deactivatedAt,
        CancellationToken cancellationToken) =>
        ChangeTeamMemberAsync(
            actor,
            userId,
            """
            UPDATE public.company_users target
            SET is_active = false,
                deactivated_at = @DeactivatedAt,
                updated_at = @DeactivatedAt
            WHERE target.id = @UserId
              AND target.organization_id = @OrganizationId
              AND target.is_active
              AND (@ActorRole = 'owner' OR target.role <> 'owner')
              AND (
                    target.role <> 'owner'
                    OR (SELECT count(*) FROM public.company_users owners
                        WHERE owners.organization_id = @OrganizationId
                          AND owners.role = 'owner'
                          AND owners.is_active) > 1
              );
            """,
            new
            {
                DeactivatedAt = deactivatedAt,
                ActorRole = ToDatabase(actor.Role),
                UserId = userId,
                actor.OrganizationId
            },
            "team.deactivated",
            new { },
            deactivatedAt,
            cancellationToken);

    private async Task<OrganizationDetails?> UpdateOrganizationCoreAsync(
        CompanyActor actor,
        string sql,
        object parameters,
        string auditAction,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                parameters,
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
            auditAction,
            "organization",
            actor.OrganizationId.ToString(),
            new { },
            updatedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await QueryOrganizationAsync(connection, actor.OrganizationId, cancellationToken);
    }

    private async Task<bool> ChangeTeamMemberAsync(
        CompanyActor actor,
        Guid userId,
        string sql,
        object parameters,
        string auditAction,
        object details,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
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
            auditAction,
            "user",
            userId.ToString(),
            details,
            changedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static Task<OrganizationDetails?> QueryOrganizationAsync(
        DbConnection connection,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<OrganizationDetails>(new CommandDefinition(
            """
            SELECT id AS Id,
                   name AS Name,
                   display_name AS DisplayName,
                   logo_url AS LogoUrl,
                   primary_color AS PrimaryColor,
                   secondary_color AS SecondaryColor,
                   welcome_message AS WelcomeMessage,
                   support_email AS SupportEmail,
                   support_phone AS SupportPhone,
                   created_at AS CreatedAt,
                   updated_at AS UpdatedAt
            FROM public.company_organizations
            WHERE id = @OrganizationId;
            """,
            new { OrganizationId = organizationId },
            cancellationToken: cancellationToken));

    private static Task WriteAuditAsync(
        DbConnection connection,
        DbTransaction transaction,
        CompanyActor actor,
        string action,
        string entityType,
        string? entityId,
        object details,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_audit_logs
                (organization_id, actor_user_id, action, entity_type, entity_id,
                 details_json, created_at)
            VALUES
                (@OrganizationId, @ActorUserId, @Action, @EntityType, @EntityId,
                 CAST(@DetailsJson AS jsonb), @CreatedAt);
            """,
            new
            {
                actor.OrganizationId,
                ActorUserId = actor.UserId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                DetailsJson = JsonSerializer.Serialize(details),
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));

    private static string ToDatabase(CompanyRole role) =>
        role.ToString().ToLowerInvariant();

    private static string ToDatabase(CompanyVisitStatus status) =>
        status.ToString().ToLowerInvariant();

    private static CompanyRole ParseRole(string role) =>
        Enum.Parse<CompanyRole>(role, true);

    private sealed class InvitationAcceptanceRow
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
