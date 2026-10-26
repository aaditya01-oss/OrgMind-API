using System.Text;
using System.Text.Json;
using OrgMind.API.Services;

namespace OrgMind.API.Services;

public class ChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public ChatService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GroqApiKey"] ?? "";
        _model = configuration["GroqModel"] ?? "openai/gpt-oss-20b";
    }

 public async Task<string> ChatAsync(string userMessage, string userId, string orgId, string orgName)
 {
    var requestBody = new
    {
        user_message = userMessage,
        user_id = userId,
        org_id = orgId,
        org_name = orgName
    };

    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    _httpClient.DefaultRequestHeaders.Clear();

    var response = await _httpClient.PostAsync(
        "http://localhost:8000/agent/chat",
        content
    );

    var responseJson = await response.Content.ReadAsStringAsync();
    var parsed = JsonDocument.Parse(responseJson);

    return parsed.RootElement
        .GetProperty("response")
        .GetString() ?? "No response";
}
}