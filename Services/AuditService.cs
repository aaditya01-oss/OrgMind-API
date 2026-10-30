using System.Text.Json;
using OrgMind.API.Data;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public interface IAuditService
{
    Task LogAsync(string eventType, string? userId, string? orgId, object details, string severity = "INFO");
}

public class AuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IServiceScopeFactory scopes, ILogger<AuditService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    private static string? Cut(string? s, int max) => s is null ? null : (s.Length <= max ? s : s[..max]);

    public async Task LogAsync(string eventType, string? userId, string? orgId, object details, string severity = "INFO")
    {
        // Never throws: an audit failure must not break the request, but it is logged loudly.
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrgMindDbContext>();
            db.AuditLogs.Add(new AuditLog
            {
                EventType = Cut(eventType, 50)!,
                UserId = Cut(userId, 320),
                OrgId = Cut(orgId, 100),
                Details = JsonSerializer.Serialize(details),
                Severity = Cut(severity, 20)!
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AUDIT WRITE FAILED: {EventType} user={UserId} org={OrgId}", eventType, userId, orgId);
        }
    }
}