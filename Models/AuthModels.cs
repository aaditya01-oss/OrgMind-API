namespace OrgMind.API.Models;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}