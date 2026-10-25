namespace OrgMind.API.Services;

public interface IAuthService
{
    Task<string> RegisterAsync(string email, string password, string orgId);
    Task<string?> LoginAsync(string email, string password);
}