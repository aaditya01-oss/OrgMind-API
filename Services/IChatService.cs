namespace OrgMind.API.Services;

public record ChatResult(string Response, List<string> Sources);

public interface IChatService
{
    Task<ChatResult> ChatAsync(string userMessage, string userId, string orgId, string orgName);
}