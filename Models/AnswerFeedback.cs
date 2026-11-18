namespace OrgMind.API.Models;

public class AnswerFeedback
{
    public int Id { get; set; }
    public string OrgId { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public string Rating { get; set; } = "";
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "new";
    public DateTime CreatedAt { get; set; }
}