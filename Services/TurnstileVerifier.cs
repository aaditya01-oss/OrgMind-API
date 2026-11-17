using System.Text.Json;

namespace OrgMind.API.Services;

public interface ITurnstileVerifier
{
    Task<bool> VerifyAsync(string? token, string? ip);
}

// Asks Cloudflare whether the "I'm human" check was really passed.
// With no secret key configured the check is skipped (development only; a warning is logged at startup).
// If the key IS set, anything unclear counts as a failure.
public class TurnstileVerifier : ITurnstileVerifier
{
    private readonly HttpClient _http;
    private readonly string? _secret;

    public TurnstileVerifier(HttpClient http, IConfiguration config)
    {
        _http = http;
        _secret = config["TurnstileSecretKey"];
    }

    public async Task<bool> VerifyAsync(string? token, string? ip)
    {
        if (string.IsNullOrWhiteSpace(_secret)) return true;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;

        try
        {
            var form = new Dictionary<string, string> { ["secret"] = _secret, ["response"] = token };
            if (!string.IsNullOrEmpty(ip)) form["remoteip"] = ip;

            using var response = await _http.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify", new FormUrlEncodedContent(form));
            if (!response.IsSuccessStatusCode) return false;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True;
        }
        catch (Exception)
        {
            return false;   // cannot reach Cloudflare: do not let the sign-up through
        }
    }
}