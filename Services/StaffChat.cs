using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public static class StaffChat
{
    public const int MaxLength = 1000;

    public static bool IsStaff(Caller me) => me.IsAdmin || me.Role == "member";

    // The two emails are sorted, so both people use the same key.
    public static string DirectKey(string a, string b) =>
        "dm:" + string.Join("|", new[] { a, b }.OrderBy(x => x, StringComparer.Ordinal));

    public static string DeptKey(string department) => "dept:" + department;

    // The key of a direct conversation, or null when it is not allowed (not staff, not found, or yourself).
    public static async Task<string?> DirectChannelAsync(OrgMindDbContext db, Caller me, string? person)
    {
        if (!IsStaff(me) || string.IsNullOrWhiteSpace(person)) return null;
        var other = person.Trim().ToLowerInvariant();
        if (other == me.Email) return null;

        var exists = await db.Users.AsNoTracking().AnyAsync(u =>
            u.OrgId == me.OrgId && u.Email == other && (u.Role == "admin" || u.Role == "member"));
        return exists ? DirectKey(me.Email!, other) : null;
    }

    // The key of a department group, or null. An admin may use any department; an employee only their own.
    public static async Task<string?> DepartmentChannelAsync(OrgMindDbContext db, Caller me, string? name)
    {
        if (!IsStaff(me)) return null;
        var dept = (name ?? "").Trim().ToLowerInvariant();
        if (dept == "") return null;
        if (!me.IsAdmin && !(me.IsDeptStaff && (me.Department ?? "").ToLowerInvariant() == dept)) return null;

        var exists = await db.Users.AsNoTracking().AnyAsync(u =>
            u.OrgId == me.OrgId && u.Role == "member" && u.Department != null && u.Department.ToLower() == dept);
        return exists ? DeptKey(dept) : null;
    }

    private static async Task MarkReadAsync(OrgMindDbContext db, Caller me, string channel, int lastId)
    {
        var read = await db.StaffReads.FirstOrDefaultAsync(r =>
            r.OrgId == me.OrgId && r.UserEmail == me.Email && r.Channel == channel);
        if (read is null)
            db.StaffReads.Add(new StaffRead { OrgId = me.OrgId!, UserEmail = me.Email!, Channel = channel, LastReadId = lastId });
        else if (read.LastReadId < lastId)
            read.LastReadId = lastId;
        await db.SaveChangesAsync();
    }

    // The latest 200 messages. Opening a conversation counts as reading it.
    public static async Task<object> ReadAsync(OrgMindDbContext db, Caller me, string channel)
    {
        var messages = await db.StaffMessages.AsNoTracking()
            .Where(m => m.OrgId == me.OrgId && m.Channel == channel)
            .OrderByDescending(m => m.Id).Take(200).ToListAsync();
        messages.Reverse();

        if (messages.Count > 0) await MarkReadAsync(db, me, channel, messages[^1].Id);

        return new
        {
            messages = messages.Select(m => new
            {
                id = m.Id,
                author = m.Author,
                fromYou = m.Author == me.Email,
                body = m.Body,
                createdAt = m.CreatedAt
            })
        };
    }

    // Returns an error text, or null when the message was saved.
    public static async Task<string?> PostAsync(OrgMindDbContext db, Caller me, string channel, string? text)
    {
        var body = (text ?? "").Trim();
        if (body.Length == 0) return "Please write a message.";
        if (body.Length > MaxLength) return $"The message is too long ({MaxLength} characters at most).";

        var message = new StaffMessage { OrgId = me.OrgId!, Channel = channel, Author = me.Email!, Body = body };
        db.StaffMessages.Add(message);
        await db.SaveChangesAsync();
        await MarkReadAsync(db, me, channel, message.Id);   // your own message is read by you
        return null;
    }
}