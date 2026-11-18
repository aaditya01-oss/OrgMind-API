namespace OrgMind.API.Models;

// One profile picture per person, kept apart from the users table so normal user lookups stay light.
public class UserAvatar
{
    public string OrgId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime UpdatedAt { get; set; }
}