using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrgMind.API.Data;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public class AuthService : IAuthService
{
    private readonly OrgMindDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(OrgMindDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private static string? Validate(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !email.Contains('@')
            || email.StartsWith('@') || email.EndsWith('@'))
            return "Enter a valid email address.";
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return "Password must be at least 8 characters.";
        if (password.Length > 72)
            return "Password must be at most 72 characters.";
        return null;
    }

    public async Task<AuthResult> RegisterWithInviteAsync(string email, string password, string inviteCode)
    {
        var problem = Validate(email, password);
        if (problem != null) return new AuthResult(false, Error: problem);

        email = email.Trim().ToLowerInvariant();
        var code = (inviteCode ?? string.Empty).Trim();

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email))
            return new AuthResult(false, Error: "That email is already registered.");

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Atomically claim the invite so two people can't use the same code.
            var claimed = await _context.Invites
                .Where(i => i.Code == code && i.UsedAt == null && i.ExpiresAt > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsedAt, (DateTime?)DateTime.UtcNow));

            if (claimed == 0)
                return new AuthResult(false, Error: "Invalid or expired invite code.");

            var invite = await _context.Invites.AsNoTracking().FirstAsync(i => i.Code == code);

            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                OrgId = invite.OrgId,
                Role = invite.Role
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return new AuthResult(true, GenerateJwtToken(user), user.OrgId, user.Role);
        }
        catch (DbUpdateException)
        {
            return new AuthResult(false, Error: "Could not create the account.");
        }
    }

    public async Task<AuthResult> CreateOrganizationAsync(string email, string password, string orgName)
    {
        var problem = Validate(email, password);
        if (problem != null) return new AuthResult(false, Error: problem);

        if (string.IsNullOrWhiteSpace(orgName) || orgName.Trim().Length > 100)
            return new AuthResult(false, Error: "Organization name is required (max 100 characters).");

        email = email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email))
            return new AuthResult(false, Error: "That email is already registered.");

        var orgId = Guid.NewGuid().ToString("N")[..10];

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Organizations.Add(new Organization { Id = orgId, Name = orgName.Trim() });
            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                OrgId = orgId,
                Role = "admin"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return new AuthResult(true, GenerateJwtToken(user), orgId, "admin");
        }
        catch (DbUpdateException)
        {
            return new AuthResult(false, Error: "Could not create the organization.");
        }
    }

    public async Task<string?> LoginAsync(string email, string password)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);

        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;

        return GenerateJwtToken(user);
    }

    public async Task<(string Code, DateTime ExpiresAt)> CreateInviteAsync(string orgId, string role, string createdBy)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)); // 16 hex characters
        var invite = new Invite
        {
            Code = code,
            OrgId = orgId,
            Role = role,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedBy = createdBy
        };
        _context.Invites.Add(invite);
        await _context.SaveChangesAsync();
        return (code, invite.ExpiresAt);
    }

    private string GenerateJwtToken(User user)
    {
        var secret = _configuration["JwtSecret"]
            ?? throw new InvalidOperationException("JwtSecret is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim("orgId", user.OrgId),
            new Claim("role", user.Role)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}