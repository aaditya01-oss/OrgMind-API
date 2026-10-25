namespace OrgMind.API.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}