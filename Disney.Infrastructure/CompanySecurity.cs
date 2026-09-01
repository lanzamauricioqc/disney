using System.Security.Cryptography;
using System.Text;
using Dapper;
using Disney.Application;
using Microsoft.AspNetCore.Identity;

namespace Disney.Infrastructure;

internal sealed class CompanyPasswordService : ICompanyPasswordService
{
    private readonly PasswordHasher<object> _passwordHasher = new();
    private readonly object _user = new();
    private readonly string _dummyPasswordHash;

    public CompanyPasswordService()
    {
        _dummyPasswordHash = _passwordHasher.HashPassword(
            _user,
            "TimingOnly-Password-Not-Used-123!");
    }

    public string Hash(string password) =>
        _passwordHasher.HashPassword(_user, password);

    public bool Verify(string? passwordHash, string password) =>
        _passwordHasher.VerifyHashedPassword(
            _user,
            passwordHash ?? _dummyPasswordHash,
            password) !=
        PasswordVerificationResult.Failed;
}

internal sealed class CompanySecretService : ICompanySecretService
{
    public string Generate(int bytes = 32) =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(bytes));

    public string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public string GetPrefix(string secret) =>
        secret[..Math.Min(secret.Length, 12)];

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

internal sealed class PostgreSqlCompanyNotificationOutbox(
    PostgreSqlConnectionFactory connectionFactory) : ICompanyNotificationOutbox
{
    public async Task<Guid> QueueAsync(
        NotificationRequest request,
        DateTimeOffset queuedAt,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.company_notification_outbox
                (id, organization_id, channel, recipient, template, payload_json,
                 status, attempts, created_at)
            VALUES
                (@Id, @OrganizationId, @Channel, @Recipient, @Template,
                 CAST(@PayloadJson AS jsonb), 'queued', 0, @QueuedAt);
            """,
            new
            {
                Id = id,
                request.OrganizationId,
                request.Channel,
                request.Recipient,
                request.Template,
                request.PayloadJson,
                QueuedAt = queuedAt
            },
            cancellationToken: cancellationToken));
        return id;
    }
}
