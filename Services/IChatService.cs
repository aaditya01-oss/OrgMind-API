namespace OrgMind.API.Services;

public interface IChatService
{
    Task<string> ChatAsync(string userMessage, string userId, string orgId, string orgName);
}