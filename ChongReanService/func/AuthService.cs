using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Cloud.Firestore;
using Microsoft.IdentityModel.Tokens;

namespace ChongReanProject.Func;

// This API's own auth - unrelated to Firebase Auth. Firestore is only ever
// reached via the service account (see docs/firebase-security-rules.md §5),
// so callers of this API are authenticated with a token we issue and verify
// ourselves, not a Firebase ID token.
public class AuthService
{
    private const string RevokedTokensCollection = "revoked_tokens";
    public const string Issuer = "ChongReanService";

    private readonly SymmetricSecurityKey _signingKey;
    private readonly FirebaseStroing _firebaseStroing;

    public AuthService(IConfiguration configuration, FirebaseStroing firebaseStroing)
    {
        var secret = configuration["Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Missing 'Secret' configuration value.");
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _firebaseStroing = firebaseStroing;
    }

    public string GenerateToken(string subject, TimeSpan? expiresIn = null)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            expires: DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromDays(3650)),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<ClaimsPrincipal> VerifyTokenAsync(string token)
    {
        var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = _signingKey,
        }, out var validatedToken);

        var jti = validatedToken.Id;
        if (await IsRevokedAsync(jti))
        {
            throw new SecurityTokenException("Token has been revoked.");
        }

        return principal;
    }

    public async Task RevokeTokenAsync(string token)
    {
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(token).Id;

        await _firebaseStroing.WriteAsync(RevokedTokensCollection, jti, new Dictionary<string, object>
        {
            ["revokedAt"] = Timestamp.GetCurrentTimestamp()
        });

    }

    // Shared by VerifyTokenAsync and the JwtBearerEvents.OnTokenValidated hook in
    // Program.cs, so the framework's own [Authorize] pipeline honors revocation too.
    public async Task<bool> IsRevokedAsync(string jti)
    {
        var data = await _firebaseStroing.ReadAsync<Dictionary<string, object>>(RevokedTokensCollection, jti);
        return data is not null;
    }
}
