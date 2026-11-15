using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;

namespace OrgMind.API.Services;

// Employee IDs look like EMP-0001 and are numbered separately inside each organization.
public static class EmployeeIds
{
    public static async Task<string> NextAsync(OrgMindDbContext db, string orgId)
    {
        var existing = await db.Users
            .Where(u => u.OrgId == orgId && u.EmployeeId != null && u.EmployeeId.StartsWith("EMP-"))
            .Select(u => u.EmployeeId!)
            .ToListAsync();

        var highest = 0;
        foreach (var id in existing)
        {
            if (int.TryParse(id.AsSpan(4), out var n) && n > highest) highest = n;
        }

        return "EMP-" + (highest + 1).ToString("D4");
    }
}