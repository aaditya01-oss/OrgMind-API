namespace OrgMind.API.Models;

public class ChatRequest
{
    public string UserMessage { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public string OrgName { get; set; } = "OrgMind";
}