namespace OrgMind.API.Models;

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}