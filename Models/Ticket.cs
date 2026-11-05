namespace OrgMind.API.Models;

public class Ticket
{
    public int Id { get; set; }
    public string OrgId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}