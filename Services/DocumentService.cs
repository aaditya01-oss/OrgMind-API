namespace OrgMind.API.Services;

public interface IDocumentService
{
    Task<(int Status, string Body)> ForwardAsync(string route, IFormFile file, string orgId, string userId, string docName);
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

    public async Task<(int Status, string Body)> ForwardAsync(string route, IFormFile file, string orgId, string userId, string docName)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        form.Add(new StreamContent(stream), "file", file.FileName);
        form.Add(new StringContent(orgId), "org_id");
        form.Add(new StringContent(docName), "doc_name");
        form.Add(new StringContent(userId), "user_id");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{route}") { Content = form };
        httpRequest.Headers.Add("X-Internal-Key", _internalKey);

        var response = await _http.SendAsync(httpRequest);
        var body = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, body);
    }
}