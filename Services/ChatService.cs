using System.Text;
using System.Text.Json;

namespace OrgMind.API.Services;

public class ChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _internalKey;

    public ChatService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["AiEngineUrl"] ?? "http://localhost:8000";
        _internalKey = configuration["InternalApiKey"]
            ?? throw new InvalidOperationException("InternalApiKey is not configured.");
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<string> ChatAsync(string userMessage, string userId, string orgId, string orgName)
    {
        var json = JsonSerializer.Serialize(new
        {
            user_message = userMessage,
            user_id = userId,
            org_id = orgId,
            org_name = orgName
        });

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/agent/chat")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("X-Internal-Key", _internalKey);

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var parsed = JsonDocument.Parse(responseJson);
        return parsed.RootElement.GetProperty("response").GetString() ?? "No response";
    }
}