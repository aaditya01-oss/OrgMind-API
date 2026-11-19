namespace OrgMind.API.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public string? Department { get; set; }
    public string? DisplayName { get; set; }
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? EmployeeId { get; set; }
    public bool MustChangePassword { get; set; }

        public int TokenVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}