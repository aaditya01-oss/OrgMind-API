namespace OrgMind.API.Services;

public record AuthResult(bool Success, string? Token = null, string? OrgId = null, string? Role = null, string? Error = null);

public interface IAuthService
{
    Task<AuthResult> RegisterWithInviteAsync(string email, string password, string inviteCode);
    Task<AuthResult> CreateOrganizationAsync(string email, string password, string orgName);

    Task<AuthResult> RegisterCustomerAsync(string email, string password, string orgId);
    Task<string?> LoginAsync(string email, string password);
    Task<(string Code, DateTime ExpiresAt)> CreateInviteAsync(string orgId, string role, string createdBy, string? department = null);
    
}