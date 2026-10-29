namespace OrgMind.API.Models;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
}

public class CreateOrgRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string OrgName { get; set; } = string.Empty;
}

public class CreateInviteRequest
{
    public string Role { get; set; } = "member";
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