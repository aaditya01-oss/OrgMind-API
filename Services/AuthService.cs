using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

    public async Task<string> RegisterAsync(string email, string password, string orgId)
    {
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var user = new User
        {
            Email = email,
            PasswordHash = hashedPassword,
            OrgId = orgId,
            Role = "member"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return await LoginAsync(email, password) ?? "Registration failed";
    }

    public async Task<string?> LoginAsync(string email, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;

        return GenerateJwtToken(user);
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["JwtSecret"] ?? "default-secret-key")
        );

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
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}