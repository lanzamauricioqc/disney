using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Disney.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Disney.Api;

internal sealed class CompanyJwtOptions
{
    public const string SectionName = "CompanyAuthentication:Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int LifetimeMinutes { get; init; } = 60;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        Encoding.UTF8.GetByteCount(SigningKey) >= 32 &&
        LifetimeMinutes is >= 5 and <= 1440;
}

internal sealed class CompanyJwtTokenService(
    IOptions<CompanyJwtOptions> options,
    TimeProvider timeProvider) : ICompanyTokenService
{
    private readonly CompanyJwtOptions _options = options.Value;

    public bool IsConfigured => _options.IsValid;

    public CompanyAccessToken Issue(CompanyUser user)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Company JWT authentication is not configured.");
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(CompanyClaimNames.OrganizationId, user.OrganizationId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", user.Role.ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);
        return new CompanyAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
