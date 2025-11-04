namespace BudgetBuddy.Services;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using BudgetBuddy.Models;

public class AuthService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IConfiguration config, ILogger<AuthService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Hash password using BCrypt
    /// </summary>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// Verify password against BCrypt hash
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Password verification error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generate JWT token with 6 hours expiration
    /// </summary>
    public string GenerateToken(User user)
    {
        var jwtSecret = _config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new System.Security.Claims.Claim("id", user.Id.ToString()),
            new System.Security.Claims.Claim("username", user.Username),
            new System.Security.Claims.Claim("email", user.Email),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["JWT_ISSUER"] ?? "BudgetBuddy",
            audience: _config["JWT_AUDIENCE"] ?? "BudgetBuddy",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(6), // 6 hours expiry
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generate refresh token
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    /// Validate token format
    /// </summary>
    public bool ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtSecret = _config["JWT_SECRET"];
        
        if (string.IsNullOrEmpty(jwtSecret))
            return false;

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = _config["JWT_ISSUER"],
                ValidateAudience = true,
                ValidAudience = _config["JWT_AUDIENCE"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }
}