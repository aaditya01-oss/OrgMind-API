using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;

namespace OrgMind.API.Services;

// The live connection. Clients can only listen: the hub has no methods they can call.
[Authorize]
public class LiveHub : Hub
{
    public static string UserGroup(string orgId, string email) => $"user:{orgId}:{email}";

    public override async Task OnConnectedAsync()
    {
        var orgId = Context.User?.FindFirst("orgId")?.Value;
        var email = Context.User?.FindFirst("email")?.Value;
        if (!string.IsNullOrEmpty(orgId) && !string.IsNullOrEmpty(email))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(orgId, email));
        await base.OnConnectedAsync();
    }
}

// Tells people that something new is waiting. Never sends text, only "look again".
// A failure here must never stop a message from being saved, so errors are swallowed.
public class LiveNotifier
{
    private readonly IHubContext<LiveHub> _hub;
    private readonly OrgMindDbContext _db;

    public LiveNotifier(IHubContext<LiveHub> hub, OrgMindDbContext db)
    {
        _hub = hub;
        _db = db;
    }

    public async Task ToPerson(string orgId, string email, object payload)
    {
        try { await _hub.Clients.Group(LiveHub.UserGroup(orgId, email)).SendAsync("refresh", payload); }
        catch (Exception) { /* the page also refreshes by itself */ }
    }

    // The members of a department plus every admin of the organization, except the sender.
    public async Task ToDepartment(string orgId, string? department, string exceptEmail, object payload)
    {
        try
        {
            var dept = (department ?? "").Trim().ToLower();
            var query = _db.Users.AsNoTracking().Where(u => u.OrgId == orgId);
            query = dept == ""
                ? query.Where(u => u.Role == "admin")
                : query.Where(u => u.Role == "admin" ||
                    (u.Role == "member" && u.Department != null && u.Department.ToLower() == dept));

            var groups = (await query.Select(u => u.Email).ToListAsync())
                .Where(e => e != exceptEmail)
                .Select(e => LiveHub.UserGroup(orgId, e))
                .ToList();
            if (groups.Count > 0) await _hub.Clients.Groups(groups).SendAsync("refresh", payload);
        }
        catch (Exception) { /* the page also refreshes by itself */ }
    }
}