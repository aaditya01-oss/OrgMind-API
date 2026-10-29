namespace OrgMind.API.Models;

public class Organization
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Invite
{
    public string Code { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}