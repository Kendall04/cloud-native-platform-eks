using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShipmentService.Application.Common.Authorization;

namespace ShipmentService.Api.Infrastructure;

public sealed class GatewayJwtAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    IOptions<PlatformAuthOptions> platformAuthOptions,
    IHostEnvironment hostEnvironment,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly PlatformAuthOptions _platformAuthOptions = platformAuthOptions.Value;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (TryAuthenticateVerifiedGatewayIdentity(out var verifiedResult))
        {
            return Task.FromResult(verifiedResult);
        }

        if (_platformAuthOptions.AllowDevelopmentJwtPassthrough && _hostEnvironment.IsDevelopment())
        {
            return Task.FromResult(AuthenticateFromBearerToken());
        }

        if (Request.Headers.ContainsKey("Authorization"))
        {
            Logger.LogWarning(
                "Rejected request because shipment-service only accepts API-Gateway-verified identity headers outside Development.");
            return Task.FromResult(AuthenticateResult.Fail("Verified API Gateway identity headers were required."));
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }

    private bool TryAuthenticateVerifiedGatewayIdentity(out AuthenticateResult result)
    {
        result = AuthenticateResult.NoResult();

        var userId = Request.Headers[PlatformHeaderNames.UserId].ToString();
        var email = Request.Headers[PlatformHeaderNames.Email].ToString();
        var rolesHeader = Request.Headers[PlatformHeaderNames.Roles].ToString();
        var proxySecret = Request.Headers[PlatformHeaderNames.TrustedProxySecret].ToString();
        var hasVerifiedHeaders = !string.IsNullOrWhiteSpace(userId) ||
                                 !string.IsNullOrWhiteSpace(email) ||
                                 !string.IsNullOrWhiteSpace(rolesHeader) ||
                                 !string.IsNullOrWhiteSpace(proxySecret);

        if (!hasVerifiedHeaders)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_platformAuthOptions.TrustedProxySecret))
        {
            Logger.LogError(
                "Rejected verified-header authentication because PlatformAuth:TrustedProxySecret is not configured.");
            result = AuthenticateResult.Fail("Trusted proxy secret is not configured.");
            return true;
        }

        if (!SecretsEqual(proxySecret, _platformAuthOptions.TrustedProxySecret))
        {
            Logger.LogWarning("Rejected request because verified identity headers did not include a valid proxy secret.");
            result = AuthenticateResult.Fail("Verified identity headers were invalid.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            result = AuthenticateResult.Fail("Verified identity headers did not include a user identifier.");
            return true;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimNames.UserId, userId),
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
            claims.Add(new Claim(ClaimNames.Email, email));
        }

        foreach (var role in rolesHeader
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim(ClaimNames.Roles, role));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        result = AuthenticateResult.Success(ticket);
        return true;
    }

    private AuthenticateResult AuthenticateFromBearerToken()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var authorizationHeader = authorizationHeaderValues.ToString();

        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.Fail("Bearer token was empty.");
        }

        try
        {
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var claims = new List<Claim>(jwtToken.Claims);

            foreach (var role in jwtToken.Claims
                         .Where(claim => claim.Type == ClaimNames.Roles || claim.Type == ClaimTypes.Role)
                         .Select(claim => claim.Value)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            if (!claims.Any(claim => claim.Type == ClaimTypes.NameIdentifier))
            {
                var userId = claims.FirstOrDefault(claim => claim.Type == ClaimNames.UserId)?.Value
                    ?? claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Sub)?.Value;

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
                }
            }

            if (!claims.Any(claim => claim.Type == ClaimTypes.Email))
            {
                var email = claims.FirstOrDefault(claim => claim.Type == ClaimNames.Email)?.Value;

                if (!string.IsNullOrWhiteSpace(email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.NameIdentifier, ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to parse bearer token in Development passthrough mode.");
            return AuthenticateResult.Fail("Unable to parse bearer token.");
        }
    }

    private static bool SecretsEqual(string candidate, string expected)
    {
        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return candidateBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes);
    }
}
