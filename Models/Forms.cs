namespace OrgMind.API.Models;

public class FormTemplate
{
    public int Id { get; set; }
    public string OrgId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Audience { get; set; } = "customer";   // customer | employee | public
    public string? Department { get; set; }               // handles the submissions
    public string Fields { get; set; } = "[]";            // JSON list of questions
    public bool Active { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FormSubmission
{
    public int Id { get; set; }
    public string OrgId { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? SubmittedBy { get; set; }              // empty for public forms
    public string? Department { get; set; }
    public string Data { get; set; } = "{}";              // JSON object: question key -> answer
    public string Status { get; set; } = "new";
    public string? HandledBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}