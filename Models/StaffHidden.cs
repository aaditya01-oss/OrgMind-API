namespace OrgMind.API.Models;

// "Delete for me": a message one person has hidden on their own side only.
public class StaffHidden
{
    public string OrgId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int MessageId { get; set; }
}