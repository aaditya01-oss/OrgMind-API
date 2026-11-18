using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public static class StaffProfiles
{
    // Only staff (admins and employees) may look at other people's profiles.
    public static bool MayView(Caller me) => StaffChat.IsStaff(me);

    // A colleague of my own organization. Customers are never returned, and other companies never are.
    public static async Task<User?> FindStaffAsync(OrgMindDbContext db, Caller me, string? email)
    {
        if (!MayView(me)) return null;

        var target = (email ?? "").Trim().ToLowerInvariant();
        if (target.Length == 0) return null;

        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u =>
            u.OrgId == me.OrgId && u.Email == target && (u.Role == "admin" || u.Role == "member"));
    }

    // What a colleague may see. No phone number, no password data, no sign-in history.
    public static object CardOf(User u) => new
    {
        email = u.Email,
        role = u.Role,
        department = u.Department,
        displayName = u.DisplayName,
        jobTitle = u.JobTitle,
        bio = u.Bio
    };
}