namespace OrgMind.API.Services;

public interface IDocumentService
{
    Task<(int Status, string Body)> ForwardAsync(string route, IFormFile file, string orgId, string userId, string docName, string visibility = "internal", string department = "");
    Task<(int Status, string Body)> ListAsync(string orgId);
    Task<(int Status, string Body)> RelabelAsync(string orgId, string docName, string visibility, string? department, string userId);
    Task<(int Status, string Body)> DeleteAsync(string orgId, string docName, string userId);
}

public class DocumentService : IDocumentService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _internalKey;

    public DocumentService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["AiEngineUrl"] ?? "http://localhost:8000";
        _internalKey = config["InternalApiKey"]
            ?? throw new InvalidOperationException("InternalApiKey is not configured.");
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<(int Status, string Body)> ForwardAsync(string route, IFormFile file, string orgId, string userId, string docName, string visibility = "internal", string department = "")
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        form.Add(new StreamContent(stream), "file", file.FileName);
        form.Add(new StringContent(orgId), "org_id");
        form.Add(new StringContent(docName), "doc_name");
        form.Add(new StringContent(userId), "user_id");
        form.Add(new StringContent(visibility), "visibility");
        form.Add(new StringContent(department), "department");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{route}") { Content = form };
        httpRequest.Headers.Add("X-Internal-Key", _internalKey);

        var response = await _http.SendAsync(httpRequest);
        var body = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, body);
    }

    // Small JSON calls to the AI engine. The internal key proves the call comes from this API.
    private async Task<(int Status, string Body)> SendAsync(HttpMethod method, string path, object? payload = null)
    {
        using var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
        if (payload is not null)
        {
            request.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");
        }
        request.Headers.Add("X-Internal-Key", _internalKey);

        var response = await _http.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public Task<(int Status, string Body)> ListAsync(string orgId) =>
        SendAsync(HttpMethod.Get, $"/documents/list?org_id={Uri.EscapeDataString(orgId)}");

    public Task<(int Status, string Body)> RelabelAsync(string orgId, string docName, string visibility, string? department, string userId) =>
        SendAsync(HttpMethod.Post, "/documents/relabel",
            new { org_id = orgId, doc_name = docName, visibility, department, user_id = userId });

    public Task<(int Status, string Body)> DeleteAsync(string orgId, string docName, string userId) =>
        SendAsync(HttpMethod.Post, "/documents/delete",
            new { org_id = orgId, doc_name = docName, user_id = userId });
}