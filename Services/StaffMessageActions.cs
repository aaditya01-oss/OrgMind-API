using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public record MessageActionResult(
    int Status, string? Error, bool Changed = false, string Kind = "", string? Department = null, bool ByAdmin = false);

public static class StaffMessageActions
{

     
    private static string KindOf(string channel) => channel.StartsWith("dept:") ? "department" : "direct";
    private static string? DepartmentOf(string channel) => channel.StartsWith("dept:") ? channel.Substring(5) : null;

     // The text of a removed message is replaced by this. (The database does not allow an empty message.)
    public const string RemovedBody = "[message removed]";

    // May this person read the conversation? Direct: only its two people. Department: its members and admins.
    public static bool CanRead(Caller me, string channel)
    {
        if (!StaffChat.IsStaff(me)) return false;

        if (channel.StartsWith("dm:"))
            return channel.Substring(3).Split('|').Contains(me.Email);

        if (channel.StartsWith("dept:"))
        {
            var dept = channel.Substring(5);
            return me.IsAdmin || (me.IsDeptStaff && (me.Department ?? "").ToLowerInvariant() == dept);
        }
        return false;
    }

    // Removing a message for everyone: its author, or an admin inside a department group.
    // An admin never gets to remove other people's private direct messages.
    public static bool CanRemove(Caller me, StaffMessage m)
    {
        if (!StaffChat.IsStaff(me) || m.OrgId != me.OrgId) return false;
        if (m.Author == me.Email) return true;
        return me.IsAdmin && m.Channel.StartsWith("dept:");
    }

    public static async Task<MessageActionResult> RemoveAsync(OrgMindDbContext db, Caller me, int id)
    {
        if (!StaffChat.IsStaff(me)) return new MessageActionResult(403, "Only staff can do this.");

        var m = await db.StaffMessages.FirstOrDefaultAsync(x => x.Id == id && x.OrgId == me.OrgId);
        if (m is null) return new MessageActionResult(404, "Message not found.");

        if (!CanRemove(me, m))
        {
            // Someone who could not even read the conversation is told it does not exist.
            return CanRead(me, m.Channel)
                ? new MessageActionResult(403, "You cannot remove this message.")
                : new MessageActionResult(404, "Message not found.");
        }

        if (m.RemovedAt is not null) return new MessageActionResult(200, null);   // already removed

        var byAdmin = m.Author != me.Email;
        m.Body = RemovedBody;             // the text is really gone, not just hidden                      // the text is really gone, not just hidden
        m.RemovedAt = DateTime.UtcNow;
        m.RemovedBy = me.Email;
        await db.SaveChangesAsync();
        return new MessageActionResult(200, null, true, KindOf(m.Channel), DepartmentOf(m.Channel), byAdmin);
    }

    // "Delete for me": hide it on my side only.
    public static async Task<MessageActionResult> HideAsync(OrgMindDbContext db, Caller me, int id)
    {
        if (!StaffChat.IsStaff(me)) return new MessageActionResult(403, "Only staff can do this.");

        var m = await db.StaffMessages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrgId == me.OrgId);
        if (m is null || (m.Author != me.Email && !CanRead(me, m.Channel)))
            return new MessageActionResult(404, "Message not found.");

        var already = await db.HiddenMessages.AnyAsync(h =>
            h.OrgId == me.OrgId && h.UserEmail == me.Email && h.MessageId == id);
        if (!already)
        {
            db.HiddenMessages.Add(new StaffHidden { OrgId = me.OrgId!, UserEmail = me.Email!, MessageId = id });
            await db.SaveChangesAsync();
        }
        return new MessageActionResult(200, null, !already);
    }
}