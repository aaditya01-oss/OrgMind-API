using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OrgMind.API.Services;

public record FormTemplateRequest(string? Name, string? Description, string? Audience, string? Department, JsonElement Fields, bool? Active);
public record FormSubmitRequest(Dictionary<string, JsonElement>? Data);
public record FormStatusRequest(string? Status);

// Who is asking, read from the signed token. Same claims the ticket endpoints use.
public record Caller(string? OrgId, string? Email, string? Role, string? Department)
{
    public bool IsAdmin => Role == "admin";
    public bool IsDeptStaff => Role == "member" && Department != null;
    public bool IsKnown => !string.IsNullOrEmpty(OrgId) && !string.IsNullOrEmpty(Email);

    public static Caller From(ClaimsPrincipal user)
    {
        var department = user.FindFirst("department")?.Value;
        if (string.IsNullOrWhiteSpace(department)) department = null;
        return new Caller(user.FindFirst("orgId")?.Value, user.FindFirst("email")?.Value,
                          user.FindFirst("role")?.Value, department);
    }
}

public static class FormRules
{
    public static readonly string[] Audiences = { "customer", "employee", "public" };
    public static readonly string[] Statuses = { "new", "in_progress", "approved", "rejected", "done" };

    private static readonly string[] Types = { "text", "number", "date", "phone", "email", "select", "textarea" };
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9_]{0,39}$");
    private static readonly Regex AutofillPattern = new("^[a-z][a-z_]{0,29}$");
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    private static readonly Regex PhonePattern = new(@"^\+?[\d\s\-()]{6,20}$");

    // Which forms each kind of signed-in person may see and fill.
    public static string[] AudiencesFor(string? role) => role switch
    {
        "admin" => new[] { "customer", "employee", "public" },
        "member" => new[] { "employee", "public" },
        "customer" => new[] { "customer", "public" },
        _ => Array.Empty<string>()
    };

    private static string Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";

    private static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    // Checks an admin's form definition and returns a clean version.
    public static bool TryValidateTemplate(FormTemplateRequest r, out string name, out string? description,
        out string audience, out string? department, out string fieldsJson, out string error)
    {
        name = (r.Name ?? "").Trim();
        description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        audience = (r.Audience ?? "").Trim().ToLowerInvariant();
        department = string.IsNullOrWhiteSpace(r.Department) ? null : r.Department.Trim().ToLowerInvariant();
        fieldsJson = "[]";
        error = "";

        if (name.Length is < 2 or > 100) { error = "The form name must be 2 to 100 characters."; return false; }
        if (description is { Length: > 500 }) { error = "The description is too long (500 characters at most)."; return false; }
        if (!Audiences.Contains(audience)) { error = "Audience must be customer, employee or public."; return false; }
        if (department is { Length: > 50 }) { error = "The department name is too long."; return false; }
        return TryNormalizeFields(r.Fields, out fieldsJson, out error);
    }

    public static bool TryNormalizeFields(JsonElement fields, out string json, out string error)
    {
        json = "[]";
        error = "";
        if (fields.ValueKind != JsonValueKind.Array) { error = "The questions must be a list."; return false; }
        var count = fields.GetArrayLength();
        if (count < 1) { error = "Add at least one question."; return false; }
        if (count > 30) { error = "A form can have 30 questions at most."; return false; }

        var seen = new HashSet<string>();
        var list = new List<Dictionary<string, object?>>();

        foreach (var f in fields.EnumerateArray())
        {
            if (f.ValueKind != JsonValueKind.Object) { error = "Each question must be an object."; return false; }

            var key = Text(f, "key");
            var label = Text(f, "label");
            var type = Text(f, "type").ToLowerInvariant();

            if (!KeyPattern.IsMatch(key)) { error = $"Question key \"{key}\" must use lowercase letters, digits or underscores."; return false; }
            if (!seen.Add(key)) { error = $"Question key \"{key}\" is used twice."; return false; }
            if (label.Length is < 1 or > 80) { error = $"Question \"{key}\" needs a label of 1 to 80 characters."; return false; }
            if (!Types.Contains(type)) { error = $"Question \"{label}\" has an unknown type."; return false; }

            var options = new List<string>();
            if (type == "select")
            {
                if (f.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in opts.EnumerateArray())
                    {
                        if (o.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(o.GetString()))
                            options.Add(o.GetString()!.Trim());
                    }
                }
                if (options.Count is < 1 or > 30 || options.Any(o => o.Length > 80))
                {
                    error = $"Question \"{label}\" needs 1 to 30 options.";
                    return false;
                }
            }

            var autofill = Text(f, "autofill");
            if (autofill != "" && !AutofillPattern.IsMatch(autofill)) { error = $"Question \"{label}\" has an invalid autofill name."; return false; }

            var item = new Dictionary<string, object?>
            {
                ["key"] = key,
                ["label"] = label,
                ["type"] = type,
                ["required"] = Flag(f, "required"),
                ["sensitive"] = Flag(f, "sensitive")
            };
            if (type == "select") item["options"] = options;
            if (autofill != "") item["autofill"] = autofill;
            list.Add(item);
        }

        json = JsonSerializer.Serialize(list);
        return true;
    }

    // Checks what a person typed against the form. Keeps only the form's own questions.
    public static bool TryCleanAnswers(string fieldsJson, Dictionary<string, JsonElement> data, out string json, out string error)
    {
        json = "{}";
        error = "";
        var clean = new Dictionary<string, string>();

        using var doc = JsonDocument.Parse(fieldsJson);
        foreach (var f in doc.RootElement.EnumerateArray())
        {
            var key = Text(f, "key");
            var label = Text(f, "label");
            var type = Text(f, "type");

            var value = "";
            if (data.TryGetValue(key, out var raw))
                value = (raw.ValueKind == JsonValueKind.String ? raw.GetString() : raw.ToString())?.Trim() ?? "";

            if (value == "")
            {
                if (Flag(f, "required")) { error = $"{label} is required."; return false; }
                continue;
            }

            var max = type == "textarea" ? 2000 : 200;
            if (value.Length > max) { error = $"{label} is too long."; return false; }

            switch (type)
            {
                case "number":
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    { error = $"{label} must be a number."; return false; }
                    break;
                case "email":
                    if (!EmailPattern.IsMatch(value)) { error = $"{label} must be a valid email address."; return false; }
                    break;
                case "phone":
                    if (!PhonePattern.IsMatch(value)) { error = $"{label} must be a valid phone number."; return false; }
                    break;
                case "date":
                    if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    { error = $"{label} must be a date written as YYYY-MM-DD."; return false; }
                    break;
                case "select":
                    var ok = f.TryGetProperty("options", out var opts)
                             && opts.EnumerateArray().Any(o => o.GetString() == value);
                    if (!ok) { error = $"{label}: please choose one of the listed options."; return false; }
                    break;
            }
            clean[key] = value;
        }

        json = JsonSerializer.Serialize(clean);
        return true;
    }
}