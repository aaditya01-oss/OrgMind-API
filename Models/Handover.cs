namespace OrgMind.API.Models;

// One hand-over of a ticket or form request from one department to another. Staff-only history.
public class Handover
{
    public int Id { get; set; }
    public string OrgId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;        // "ticket" or "submission"
    public int RefId { get; set; }
    public string? FromDepartment { get; set; }
    public string ToDepartment { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string HandedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}