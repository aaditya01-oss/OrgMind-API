namespace OrgMind.API.Services;

public record FeedbackRequest(string? Rating, string? Question, string? Answer, string? Note);
public record FeedbackStatusRequest(string? Status);

public static class FeedbackRules
{
    public const int MaxQuestion = 500;
    public const int MaxAnswer = 1500;
    public const int MaxNote = 300;

    private static string? Cut(string? text, int max)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return null;
        return t.Length > max ? t.Substring(0, max) : t;
    }

    // A thumbs-up keeps no text at all. A thumbs-down keeps the question, part of the answer
    // and an optional reason, so an admin can see what to improve.
    public static bool TryClean(FeedbackRequest r, out string rating, out string? question,
        out string? answer, out string? note, out string error)
    {
        rating = (r.Rating ?? "").Trim().ToLowerInvariant();
        question = null;
        answer = null;
        note = null;
        error = "";

        if (rating is not ("up" or "down")) { error = "Rating must be up or down."; return false; }
        if (rating == "up") return true;

        question = Cut(r.Question, MaxQuestion);
        if (question is null) { error = "The question is missing."; return false; }
        answer = Cut(r.Answer, MaxAnswer);
        note = Cut(r.Note, MaxNote);
        return true;
    }
}