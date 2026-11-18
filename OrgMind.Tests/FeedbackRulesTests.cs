using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

public class FeedbackRulesTests
{
    [Fact]
    public void Thumbs_up_keeps_no_text_at_all()
    {
        var ok = FeedbackRules.TryClean(new FeedbackRequest("up", "my account number is 123", "some answer", "note"),
            out var rating, out var q, out var a, out var n, out _);
        Assert.True(ok);
        Assert.Equal("up", rating);
        Assert.Null(q);
        Assert.Null(a);
        Assert.Null(n);
    }

    [Fact]
    public void Thumbs_down_keeps_question_answer_and_reason()
    {
        var ok = FeedbackRules.TryClean(new FeedbackRequest("DOWN", "  What are the hours? ", "We open at 9.", "wrong"),
            out var rating, out var q, out var a, out var n, out _);
        Assert.True(ok);
        Assert.Equal("down", rating);
        Assert.Equal("What are the hours?", q);
        Assert.Equal("We open at 9.", a);
        Assert.Equal("wrong", n);
    }

    [Fact]
    public void Thumbs_down_without_a_question_is_refused()
    {
        Assert.False(FeedbackRules.TryClean(new FeedbackRequest("down", "   ", "answer", null),
            out _, out _, out _, out _, out _));
    }

    [Fact]
    public void Unknown_rating_is_refused()
    {
        Assert.False(FeedbackRules.TryClean(new FeedbackRequest("meh", "q", "a", null),
            out _, out _, out _, out _, out _));
    }

    [Fact]
    public void Long_text_is_cut_to_the_limits()
    {
        var ok = FeedbackRules.TryClean(
            new FeedbackRequest("down", new string('q', 900), new string('a', 5000), new string('n', 900)),
            out _, out var q, out var a, out var n, out _);
        Assert.True(ok);
        Assert.Equal(FeedbackRules.MaxQuestion, q!.Length);
        Assert.Equal(FeedbackRules.MaxAnswer, a!.Length);
        Assert.Equal(FeedbackRules.MaxNote, n!.Length);
    }
}