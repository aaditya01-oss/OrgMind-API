using System.Text;
using System.Text.RegularExpressions;
using OrgMind.API.Models;

namespace OrgMind.API.Services;

public record ProfileRequest(string? DisplayName, string? JobTitle, string? Phone, string? Bio);
public record CleanProfile(string? DisplayName, string? JobTitle, string? Phone, string? Bio);

public static class ProfileRules
{
    public const int MaxName = 60;
    public const int MaxTitle = 60;
    public const int MaxPhone = 30;
    public const int MaxBio = 300;

    private static readonly Regex PhoneChars = new(@"^[0-9+()\-\s]+$", RegexOptions.Compiled);

    // Returns an error text, or null when the profile is fine. "clean" is what to store.
    public static string? Validate(ProfileRequest request, out CleanProfile clean)
    {
        clean = new CleanProfile(null, null, null, null);

        var name = Tidy(request.DisplayName, false);
        var title = Tidy(request.JobTitle, false);
        var phone = Tidy(request.Phone, false);
        var bio = Tidy(request.Bio, true);

        if (name is { Length: > MaxName }) return $"Your name can be at most {MaxName} characters.";
        if (title is { Length: > MaxTitle }) return $"Your job title can be at most {MaxTitle} characters.";
        if (bio is { Length: > MaxBio }) return $"Your bio can be at most {MaxBio} characters.";
        if (phone is not null)
        {
            if (phone.Length > MaxPhone) return $"Your phone number can be at most {MaxPhone} characters.";
            if (!PhoneChars.IsMatch(phone) || phone.Count(char.IsDigit) < 3)
                return "A phone number may only contain digits, spaces and + - ( ), with at least 3 digits.";
        }

        clean = new CleanProfile(name, title, phone, bio);
        return null;
    }

    // Trim, drop control characters, and turn empty text into null. Line breaks survive only in the bio.
    private static string? Tidy(string? text, bool keepNewlines)
    {
        if (text is null) return null;
        var sb = new StringBuilder();
        foreach (var c in text.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (c == '\n') sb.Append(keepNewlines ? '\n' : ' ');
            else if (!char.IsControl(c)) sb.Append(c);
        }
        var result = sb.ToString().Trim();
        return result.Length == 0 ? null : result;
    }

    // What the profile page receives. The password hash is never included.
    public static object View(User u) => new
    {
        email = u.Email,
        role = u.Role,
        department = u.Department,
        employeeId = u.EmployeeId,
        orgId = u.OrgId,
        displayName = u.DisplayName,
        jobTitle = u.JobTitle,
        phone = u.Phone,
        bio = u.Bio,
        createdAt = u.CreatedAt
    };
}