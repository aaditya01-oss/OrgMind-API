using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public record DepartmentNameRequest(string? Name);
public record DepartmentRenameRequest(string? NewName);

public record DepartmentUsage(int People, int Tickets, int Forms, int Documents);
public record DepartmentResult(int Status, string? Error, int PeopleMoved, DepartmentUsage? Usage = null);

// Result row of the documents count (the documents table belongs to the AI engine).
public class DocumentDepartmentCount
{
    public string Department { get; set; } = string.Empty;
    public int Count { get; set; }
}

public static class DepartmentRules
{
    public const int MaxLength = 50;

    private static readonly Regex Allowed = new(@"^[\p{L}\p{N}][\p{L}\p{N} _&.-]*$", RegexOptions.Compiled);
    private static readonly string[] Reserved = { "unassigned", "none", "all" };

    // Names are stored trimmed, lowercase, with single spaces, so "HR" and "hr " are the same department.
    // Returns an error text, or null when the name is fine.
    public static string? Clean(string? input, out string name)
    {
        name = "";
        var text = Regex.Replace((input ?? "").Trim(), @"\s+", " ").ToLowerInvariant();
        if (text.Length == 0) return "Please type a department name.";
        if (text.Length > MaxLength) return $"A department name can be at most {MaxLength} characters.";
        if (!Allowed.IsMatch(text)) return "Use letters, numbers, spaces and - _ & . only, starting with a letter or number.";
        if (Reserved.Contains(text)) return $"\"{text}\" is a reserved name. Please choose another one.";
        name = text;
        return null;
    }
}

public static class DepartmentOps
{
    public static Task<bool> ExistsAsync(OrgMindDbContext db, string orgId, string name) =>
        db.Departments.AsNoTracking().AnyAsync(d => d.OrgId == orgId && d.Name == name);

    // Departments already used somewhere (older data) are added to the list, so nothing is invisible.
    public static async Task EnsureKnownAsync(OrgMindDbContext db, string orgId)
    {
        var used = new HashSet<string>();
        void Collect(IEnumerable<string?> names)
        {
            foreach (var n in names)
            {
                var text = (n ?? "").Trim().ToLowerInvariant();
                if (text.Length > 0) used.Add(text);
            }
        }

        Collect(await db.Users.AsNoTracking().Where(u => u.OrgId == orgId && u.Department != null).Select(u => u.Department).Distinct().ToListAsync());
        Collect(await db.Tickets.AsNoTracking().Where(t => t.OrgId == orgId && t.Department != null).Select(t => t.Department).Distinct().ToListAsync());
        Collect(await db.FormTemplates.AsNoTracking().Where(f => f.OrgId == orgId && f.Department != null).Select(f => f.Department).Distinct().ToListAsync());

        var known = await db.Departments.AsNoTracking().Where(d => d.OrgId == orgId).Select(d => d.Name).ToListAsync();
        var missing = used.Except(known).ToList();
        if (missing.Count == 0) return;

        foreach (var name in missing)
            db.Departments.Add(new Department { OrgId = orgId, Name = name, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    public static async Task<DepartmentUsage> UsageAsync(OrgMindDbContext db, string orgId, string name, int documents = 0)
    {
        var people = await db.Users.CountAsync(u => u.OrgId == orgId && u.Department == name);
        var tickets = await db.Tickets.CountAsync(t => t.OrgId == orgId && t.Department == name);
        var forms = await db.FormTemplates.CountAsync(f => f.OrgId == orgId && f.Department == name);
        return new DepartmentUsage(people, tickets, forms, documents);
    }

    public static async Task<DepartmentResult> AddAsync(OrgMindDbContext db, string orgId, string name)
    {
        if (await ExistsAsync(db, orgId, name))
            return new DepartmentResult(409, "That department already exists.", 0);

        db.Departments.Add(new Department { OrgId = orgId, Name = name, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return new DepartmentResult(200, null, 0);
    }

    // Everything that points at "from" now points at "to". Returns how many people were moved.
    // The caller saves. Documents are handled separately (see RelabelDocumentsAsync).
    private static async Task<int> ReassignAsync(OrgMindDbContext db, string orgId, string from, string to)
    {
        var users = await db.Users.Where(u => u.OrgId == orgId && u.Department == from).ToListAsync();
        foreach (var u in users) u.Department = to;

        foreach (var i in await db.Invites.Where(i => i.OrgId == orgId && i.Department == from).ToListAsync())
            i.Department = to;
        foreach (var t in await db.Tickets.Where(t => t.OrgId == orgId && t.Department == from).ToListAsync())
            t.Department = to;
        foreach (var f in await db.FormTemplates.Where(f => f.OrgId == orgId && f.Department == from).ToListAsync())
            f.Department = to;
        foreach (var s in await db.FormSubmissions.Where(s => s.OrgId == orgId && s.Department == from).ToListAsync())
            s.Department = to;

        return users.Count;
    }

    // The department's group chat is stored under its name, so a rename moves it too.
    private static async Task MoveChatAsync(OrgMindDbContext db, string orgId, string from, string to)
    {
        var oldKey = StaffChat.DeptKey(from);
        var newKey = StaffChat.DeptKey(to);

        foreach (var m in await db.StaffMessages.Where(m => m.OrgId == orgId && m.Channel == oldKey).ToListAsync())
            m.Channel = newKey;

        var reads = await db.StaffReads.Where(r => r.OrgId == orgId && r.Channel == oldKey).ToListAsync();
        var existing = await db.StaffReads.Where(r => r.OrgId == orgId && r.Channel == newKey).ToListAsync();
        foreach (var r in reads)
        {
            var same = existing.FirstOrDefault(x => x.UserEmail == r.UserEmail);
            if (same is null)
                db.StaffReads.Add(new StaffRead { OrgId = orgId, UserEmail = r.UserEmail, Channel = newKey, LastReadId = r.LastReadId });
            else if (same.LastReadId < r.LastReadId)
                same.LastReadId = r.LastReadId;
            db.StaffReads.Remove(r);   // the key includes the channel, so the row is replaced, not edited
        }
    }

    public static async Task<DepartmentResult> RenameAsync(OrgMindDbContext db, string orgId, string from, string to)
    {
        if (from == to) return new DepartmentResult(400, "Please choose a different name.", 0);

        var row = await db.Departments.FirstOrDefaultAsync(d => d.OrgId == orgId && d.Name == from);
        if (row is null) return new DepartmentResult(404, "Department not found.", 0);
        if (await ExistsAsync(db, orgId, to))
            return new DepartmentResult(409, "A department with that name already exists.", 0);

        var moved = await ReassignAsync(db, orgId, from, to);
        await MoveChatAsync(db, orgId, from, to);

        db.Departments.Remove(row);
        db.Departments.Add(new Department { OrgId = orgId, Name = to, CreatedAt = row.CreatedAt });
        await db.SaveChangesAsync();   // one save = one database transaction
        return new DepartmentResult(200, null, moved);
    }

    // A department that is still in use can only be deleted by moving everything to another department.
    // "documents" is how many documents carry this department (counted by the caller).
    public static async Task<DepartmentResult> DeleteAsync(OrgMindDbContext db, string orgId, string name, string? moveTo, int documents)
    {
        var row = await db.Departments.FirstOrDefaultAsync(d => d.OrgId == orgId && d.Name == name);
        if (row is null) return new DepartmentResult(404, "Department not found.", 0);

        var usage = await UsageAsync(db, orgId, name, documents);
        var inUse = usage.People + usage.Tickets + usage.Forms + usage.Documents > 0;
        var moved = 0;

        if (inUse)
        {
            if (string.IsNullOrEmpty(moveTo))
                return new DepartmentResult(409, "This department is still in use. Choose where to move everything first.", 0, usage);
            if (moveTo == name)
                return new DepartmentResult(400, "Choose a different department to move everything to.", 0, usage);
            if (!await ExistsAsync(db, orgId, moveTo))
                return new DepartmentResult(400, "The department to move everything to does not exist.", 0, usage);

            moved = await ReassignAsync(db, orgId, name, moveTo);
        }

        // The group chat history goes with the department, so it cannot reappear under a re-used name.
        var key = StaffChat.DeptKey(name);
        db.StaffMessages.RemoveRange(await db.StaffMessages.Where(m => m.OrgId == orgId && m.Channel == key).ToListAsync());
        db.StaffReads.RemoveRange(await db.StaffReads.Where(r => r.OrgId == orgId && r.Channel == key).ToListAsync());
        db.Departments.Remove(row);
        await db.SaveChangesAsync();
        return new DepartmentResult(200, null, moved, usage);
    }

    // ---- documents: the table belongs to the AI engine, only the department label is touched ----

    public static async Task<Dictionary<string, int>> DocumentCountsAsync(OrgMindDbContext db, string orgId)
    {
        var rows = await db.Database.SqlQuery<DocumentDepartmentCount>(
            $"SELECT lower(department) AS \"Department\", count(*)::int AS \"Count\" FROM documents WHERE org_id = {orgId} AND department IS NOT NULL GROUP BY lower(department)")
            .ToListAsync();
        return rows.ToDictionary(r => r.Department, r => r.Count);
    }

    public static Task<int> RelabelDocumentsAsync(OrgMindDbContext db, string orgId, string from, string to) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE documents SET department = {to} WHERE org_id = {orgId} AND lower(department) = {from}");
}