namespace OrgMind.API.Models;

public class AuditLog
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? OrgId { get; set; }
    public string Details { get; set; } = "{}";
    public string Severity { get; set; } = "INFO";
    public DateTime Timestamp { get; set; }
}