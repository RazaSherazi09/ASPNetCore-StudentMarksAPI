using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MarksApi.Common;
using MarksApi.Data;
using MarksApi.Data.Entities;
using MarksApi.Dtos.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MarksApi.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var normalizedUsername = request.Username.Trim();

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == normalizedUsername, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Authentication failed: unknown username '{Username}'.", normalizedUsername);
            throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "Invalid credentials",
                "Invalid username or password.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Authentication failed: invalid password for username '{Username}' (UserId: {UserId}).", user.Username, user.Id);
            throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "Invalid credentials",
                "Invalid username or password.");
        }

        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key is not configured or is shorter than 32 characters.");
        }

        var issuer = _configuration["Jwt:Issuer"] ?? "MarksApi";
        var audience = _configuration["Jwt:Audience"] ?? "MarksApiClients";
        var lifetimeMinutes = _configuration.GetValue<int?>("Jwt:LifetimeMinutes") ?? 60;

        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = nowUtc.AddMinutes(lifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.StudentId.HasValue)
        {
            claims.Add(new Claim("student_id", user.StudentId.Value.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: nowUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        _logger.LogInformation("User '{Username}' (Role: {Role}) logged in successfully.", user.Username, user.Role);

        return new LoginResponseDto
        {
            Token = tokenString,
            ExpiresAtUtc = expiresAtUtc,
            Role = user.Role
        };
    }
}

