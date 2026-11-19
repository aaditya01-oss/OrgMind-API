using System.Text;
using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;

namespace OrgMind.API.Services;

public record ReviewSection(string Key, string Title, string Note, string[] Columns, List<string[]> Rows);
public record ReviewStamp(string By, DateTime At);
public record ReviewReport(DateTime GeneratedAt, string From, string To, ReviewStamp? LastReview, List<ReviewSection> Sections);
public record ReviewedRequest(DateTime? From, DateTime? To);

public class DocumentVisibilityRow
{
    public string Visibility { get; set; } = "";
    public string Department { get; set; } = "";
    public int Count { get; set; }
}

// The access review: who can do what, and what happened to access during a period.
// It only reads. Nothing here changes any data, and no message text or answer text is ever included.
public static class AccessReview
{
    private static readonly string[] EventColumns = { "When (UTC)", "Person", "What happened", "Details (ids only)" };

    // Start is inclusive, end is exclusive (the day after "to"), so "to" includes the whole last day.
        // Start is inclusive, end is exclusive (the day after "to"), so "to" includes the whole last day.
    // The database stores times in UTC, so the range is UTC too.
    public static (DateTime Start, DateTime End) Range(DateTime? from, DateTime? to, DateTime now)
    {
        var end = DateTime.SpecifyKind((to ?? now).Date.AddDays(1), DateTimeKind.Utc);
        var start = DateTime.SpecifyKind((from ?? end.AddDays(-31)).Date, DateTimeKind.Utc);
        if (start >= end) start = end.AddDays(-1);
        if ((end - start).TotalDays > 366) start = end.AddDays(-366);
        return (start, end);
    }

    // A spreadsheet treats a cell starting with = + - @ as a formula. A leading ' stops that.
    public static string CsvCell(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+-@\t\r".IndexOf(text[0]) >= 0) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    public static string ToCsv(ReviewSection section)
    {
        var csv = new StringBuilder("\uFEFF");
        csv.Append(string.Join(",", section.Columns.Select(CsvCell))).Append("\r\n");
        foreach (var row in section.Rows)
            csv.Append(string.Join(",", row.Select(CsvCell))).Append("\r\n");
        return csv.ToString();
    }

    private static string Time(DateTime t) => t.ToString("yyyy-MM-dd HH:mm");
    private static string Time(DateTime? t) => t is null ? "" : Time(t.Value);

    private static string Label(string eventType)
    {
        var text = eventType.Replace('_', ' ').ToLowerInvariant();
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static string VisibilityLabel(string v) => v switch
    {
        "public" => "Anyone",
        "registered" => "Signed-in people",
        "internal" => "Staff only",
        "department" => "One department",
        "admin_only" => "Admins only",
        _ => v
    };

    private static int VisibilityRank(string v) => v switch
    {
        "public" => 0, "registered" => 1, "internal" => 2, "department" => 3, "admin_only" => 4, _ => 5
    };

    private static async Task<List<string[]>> EventRowsAsync(
        OrgMindDbContext db, string orgId, DateTime start, DateTime end, string[] types)
    {
        var rows = await db.AuditLogs.AsNoTracking()
            .Where(a => a.OrgId == orgId && types.Contains(a.EventType) && a.Timestamp >= start && a.Timestamp < end)
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            .Take(2000)
            .ToListAsync();
        return rows.Select(a => new[] { Time(a.Timestamp), a.UserId ?? "", Label(a.EventType), a.Details ?? "" }).ToList();
    }

    public static async Task<ReviewReport> BuildAsync(OrgMindDbContext db, string orgId, DateTime? from, DateTime? to)
    {
        var (start, end) = Range(from, to, DateTime.UtcNow);

        var staff = await db.Users.AsNoTracking()
            .Where(u => u.OrgId == orgId && u.Role != "customer")
            .OrderBy(u => u.Email).ToListAsync();

        var lastSignIn = (await db.AuditLogs.AsNoTracking()
                .Where(a => a.OrgId == orgId && a.EventType == "LOGIN_SUCCESS")
                .GroupBy(a => a.UserId)
                .Select(g => new { Who = g.Key, At = g.Max(a => a.Timestamp) })
                .ToListAsync())
            .ToDictionary(x => (x.Who ?? "").ToLowerInvariant(), x => x.At);

        DateTime? LastFor(string email) =>
            lastSignIn.TryGetValue(email.ToLowerInvariant(), out var at) ? at : null;

        string Status(DateTime? last) =>
            last is null ? "Never signed in" : last < start ? "No sign-in in this period" : "Active";

        var admins = new ReviewSection("admins", "Administrators",
            "Admins can see and change everything in the organization. Keep this list short and current. Last sign-in comes from the audit log.",
            new[] { "Email", "Name", "Employee ID", "Last sign-in (UTC)", "Account created (UTC)" },
            staff.Where(u => u.Role == "admin")
                 .Select(u => new[] { u.Email, u.DisplayName ?? "", u.EmployeeId ?? "", Time(LastFor(u.Email)), Time(u.CreatedAt) }).ToList());

        var employees = new ReviewSection("staff", "Employees by department",
            "Everyone who can see internal information. \"Never signed in\" or \"No sign-in in this period\" accounts are worth a look: close or remove access that is no longer needed.",
            new[] { "Email", "Name", "Department", "Employee ID", "Last sign-in (UTC)", "Status" },
            staff.Where(u => u.Role != "admin")
                 .OrderBy(u => u.Department ?? "~").ThenBy(u => u.Email)
                 .Select(u => new[] { u.Email, u.DisplayName ?? "", u.Department ?? "", u.EmployeeId ?? "", Time(LastFor(u.Email)), Status(LastFor(u.Email)) }).ToList());

        var docRows = await db.Database.SqlQuery<DocumentVisibilityRow>(
            $"SELECT visibility AS \"Visibility\", coalesce(department, '') AS \"Department\", count(*)::int AS \"Count\" FROM documents WHERE org_id = {orgId} GROUP BY visibility, department")
            .ToListAsync();
        var documents = new ReviewSection("documents", "Who can read the knowledge base",
            "How many documents each group of people can read. Counts only: document names are not listed here.",
            new[] { "Who can read", "Department", "Documents" },
            docRows.OrderBy(r => VisibilityRank(r.Visibility)).ThenBy(r => r.Department)
                   .Select(r => new[] { VisibilityLabel(r.Visibility), r.Department, r.Count.ToString() }).ToList());

        var documentChanges = new ReviewSection("documentchanges", "Document changes",
            "Uploads, deletions and changes to who can read a document, in the chosen period.",
            EventColumns,
            await EventRowsAsync(db, orgId, start, end, new[] { "DOCUMENT_UPLOADED", "DOCUMENT_DELETED", "DOCUMENT_RELABELED" }));

        var reveals = new ReviewSection("reveals", "Sensitive answers viewed",
            "Each time someone opened a sensitive form answer. The answer itself is never recorded here.",
            EventColumns,
            await EventRowsAsync(db, orgId, start, end, new[] { "FORM_SENSITIVE_REVEALED" }));

        var failed = await db.AuditLogs.AsNoTracking()
            .Where(a => a.OrgId == orgId && a.EventType == "LOGIN_FAILED" && a.Timestamp >= start && a.Timestamp < end)
            .ToListAsync();
        var signIns = new ReviewSection("signins", "Failed sign-ins",
            "Wrong passwords per account. Many failures on one account can mean someone is guessing.",
            new[] { "Account", "Failed attempts", "Most recent (UTC)" },
            failed.GroupBy(a => a.UserId ?? "")
                  .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                  .Select(g => new[] { g.Key, g.Count().ToString(), Time(g.Max(a => a.Timestamp)) }).ToList());

        var accountChanges = new ReviewSection("accountchanges", "Account and access changes",
            "Password resets and changes, new members, invites, role and department changes.",
            EventColumns,
            await EventRowsAsync(db, orgId, start, end, new[]
            {
                "PASSWORD_RESET_BY_ADMIN", "PASSWORD_CHANGED", "USER_UPDATED", "USER_JOINED", "INVITE_CREATED",
                "DEPARTMENT_ADDED", "DEPARTMENT_RENAMED", "DEPARTMENT_DELETED"
            }));

        var removals = new ReviewSection("removals", "Messages removed",
            "Staff messages that were removed (including removals by an administrator in department groups). The text is not kept.",
            EventColumns,
            await EventRowsAsync(db, orgId, start, end, new[] { "STAFF_MESSAGE_REMOVED" }));

        var last = await db.AuditLogs.AsNoTracking()
            .Where(a => a.OrgId == orgId && a.EventType == "ACCESS_REVIEWED")
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new { a.UserId, a.Timestamp })
            .FirstOrDefaultAsync();

        return new ReviewReport(
            DateTime.UtcNow,
            start.ToString("yyyy-MM-dd"),
            end.AddDays(-1).ToString("yyyy-MM-dd"),
            last is null ? null : new ReviewStamp(last.UserId ?? "", last.Timestamp),
            new List<ReviewSection> { admins, employees, documents, documentChanges, reveals, signIns, accountChanges, removals });
    }
}