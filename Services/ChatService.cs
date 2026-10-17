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
            model = _model,
            messages = new[]
            {
                new { role = "system", content = $"You are OrgBot, an intelligent assistant for {orgName}." },
                new { role = "user", content = userMessage }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.PostAsync(
            "https://api.groq.com/openai/v1/chat/completions",
            content
        );

        var responseJson = await response.Content.ReadAsStringAsync();
        var parsed = JsonDocument.Parse(responseJson);
        
        return parsed.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "No response";
    }
}