using System.Text.Json;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public static class AuditQuery
{
    // One place for the filters, so the page and the CSV download always agree.
    public static IQueryable<AuditLog> Filter(IQueryable<AuditLog> query, string orgId, string? type,
        string? user, DateTime? from, DateTime? to, bool warningsOnly)
    {
        query = query.Where(a => a.OrgId == orgId);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(a => a.EventType == type);

        if (!string.IsNullOrWhiteSpace(user))
        {
            var u = user.Trim().ToLower();
            query = query.Where(a => a.UserId != null && a.UserId.ToLower().Contains(u));
        }

                // The log stores UTC, and the database driver only accepts dates marked as UTC.
        if (from is not null)
        {
            var start = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.Timestamp >= start);
        }
        if (to is not null)
        {
            var end = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(a => a.Timestamp < end);
        }
        if (warningsOnly) query = query.Where(a => a.Severity != null && a.Severity != "INFO");

        return query;
    }

    public static JsonElement ParseDetails(string? details)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(details) ? "{}" : details);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }

    // A spreadsheet treats a cell starting with = + - @ as a formula, so those get a leading quote.
    public static string CsvCell(string? value)
    {
        var v = value ?? "";
        if (v.Length > 0 && "=+-@\t\r".IndexOf(v[0]) >= 0) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}