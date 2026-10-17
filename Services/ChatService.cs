namespace OrgMind.API.Services;

public class ChatService : IChatService
{
    private readonly HttpClient _httpClient;

    public ChatService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> ChatAsync(string userMessage, string userId, string orgId, string orgName)
    {
        // We'll implement this next
        return await Task.FromResult("OrgBot is thinking...");
    }
}