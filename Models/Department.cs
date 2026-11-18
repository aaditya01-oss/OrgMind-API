namespace OrgMind.API.Models;

// The list of departments an administrator has set up for one organization.
public class Department
{
    public string OrgId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}