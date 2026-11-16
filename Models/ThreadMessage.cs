namespace OrgMind.API.Models;

public class ThreadMessage
{
    public int Id { get; set; }
    public string OrgId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;     // "ticket" or "submission"
    public int RefId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool ReadByOwner { get; set; }
    public DateTime CreatedAt { get; set; }
}