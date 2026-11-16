namespace OrgMind.API.Models;

public class StaffMessage
{
    public int Id { get; set; }
    public string OrgId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;   // "dm:a|b" or "dept:name"
    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// How far one person has read in one conversation.
public class StaffRead
{
    public string OrgId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public int LastReadId { get; set; }
}