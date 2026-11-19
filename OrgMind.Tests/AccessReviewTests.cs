using OrgMind.API.Models;
using OrgMind.API.Services;
using Microsoft.EntityFrameworkCore;

namespace OrgMind.Tests;

public class AccessReviewRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0);

    [Fact]
    public void With_no_dates_the_last_month_up_to_today_is_used()
    {
        var (start, end) = AccessReview.Range(null, null, Now);
        Assert.Equal(new DateTime(2026, 10, 20), start);
        Assert.Equal(new DateTime(2026, 11, 20), end);   // exclusive: includes all of today
    }

    [Fact]
    public void The_last_chosen_day_is_included()
    {
        var (start, end) = AccessReview.Range(new DateTime(2026, 11, 10), new DateTime(2026, 11, 12), Now);
        Assert.Equal(new DateTime(2026, 11, 10), start);
        Assert.Equal(new DateTime(2026, 11, 13), end);
    }

    [Fact]
    public void Dates_in_the_wrong_order_still_give_a_valid_range()
    {
        var (start, end) = AccessReview.Range(new DateTime(2026, 11, 15), new DateTime(2026, 11, 10), Now);
        Assert.True(start < end);
    }

    [Fact]
    public void A_range_is_never_longer_than_a_year()
    {
        var (start, end) = AccessReview.Range(new DateTime(2020, 1, 1), null, Now);
        Assert.Equal(366, (end - start).TotalDays);
    }

    [Theory]
    [InlineData("hello", "\"hello\"")]
    [InlineData(null, "\"\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=SUM(A1)", "\"'=SUM(A1)\"")]
    [InlineData("+1", "\"'+1\"")]
    [InlineData("@x", "\"'@x\"")]
    public void A_csv_cell_is_quoted_and_cannot_become_a_formula(string? input, string expected)
        => Assert.Equal(expected, AccessReview.CsvCell(input));
}

[Collection("real-db")]
[Trait("Category", "RealDb")]
public class RealAccessReviewTests
{
    private readonly RealDbFixture _fx;
    public RealAccessReviewTests(RealDbFixture fx) => _fx = fx;

    private static string NewOrg() => "org-" + Guid.NewGuid().ToString("N")[..10];

    private static AuditLog Log(string org, string type, string? who, DateTime at) => new()
    {
        EventType = type, UserId = who, OrgId = org, Details = "{}", Severity = "INFO", Timestamp = at
    };

    private static User Person(string org, string name, string role, string? dept = null) => new()
    {
        Email = $"{name}@{org}.test", PasswordHash = "x", OrgId = org, Role = role, Department = dept
    };

    [Fact]
    public async Task The_report_shows_this_company_only_and_only_the_chosen_period()
    {
        var org = NewOrg();
        var now = DateTime.UtcNow;

        await using (var db = _fx.NewDb())
        {
            db.Users.AddRange(
                Person(org, "boss", "admin"),
                Person(org, "sam", "member", "hr"),
                Person(org, "lee", "member", "hr"),
                Person(org, "cust", "customer"));
            db.AuditLogs.AddRange(
                Log(org, "LOGIN_SUCCESS", $"sam@{org}.test", now.AddDays(-1)),
                Log(org, "LOGIN_FAILED", $"lee@{org}.test", now.AddHours(-3)),
                Log(org, "LOGIN_FAILED", $"lee@{org}.test", now.AddHours(-2)),
                Log(org, "PASSWORD_RESET_BY_ADMIN", $"boss@{org}.test", now.AddDays(-2)),
                Log(org, "PASSWORD_RESET_BY_ADMIN", $"boss@{org}.test", now.AddDays(-90)),   // outside the period
                Log("some-other-org", "PASSWORD_RESET_BY_ADMIN", "x@y.test", now));          // another company
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO documents (org_id, doc_name, visibility, department) VALUES ({org}, 'a.pdf', 'public', NULL), ({org}, 'b.pdf', 'department', 'hr'), ({org}, 'c.pdf', 'internal', NULL)");
        }

        await using var read = _fx.NewDb();
        var report = await AccessReview.BuildAsync(read, org, null, null);
        ReviewSection Section(string key) => report.Sections.Single(s => s.Key == key);

        // admins and staff are separate, and customers are in neither
        Assert.Single(Section("admins").Rows);
        Assert.Equal($"boss@{org}.test", Section("admins").Rows[0][0]);
        Assert.Equal(2, Section("staff").Rows.Count);
        Assert.DoesNotContain(Section("staff").Rows, r => r[0].StartsWith("cust@"));

        var sam = Section("staff").Rows.Single(r => r[0].StartsWith("sam@"));
        var lee = Section("staff").Rows.Single(r => r[0].StartsWith("lee@"));
        Assert.Equal("Active", sam[5]);
        Assert.Equal("Never signed in", lee[5]);

        // documents are counted by who can read them
        Assert.Equal("1", Section("documents").Rows.Single(r => r[0] == "Anyone")[2]);
        Assert.Equal("1", Section("documents").Rows.Single(r => r[0] == "One department" && r[1] == "hr")[2]);
        Assert.Equal("1", Section("documents").Rows.Single(r => r[0] == "Staff only")[2]);

        // only this company, only this period
        Assert.Single(Section("accountchanges").Rows);

        // failed sign-ins are added up per account
        var failedRow = Assert.Single(Section("signins").Rows);
        Assert.Equal($"lee@{org}.test", failedRow[0]);
        Assert.Equal("2", failedRow[1]);

        Assert.Null(report.LastReview);
    }

    [Fact]
    public async Task The_latest_review_stamp_is_reported()
    {
        var org = NewOrg();
        await using (var db = _fx.NewDb())
        {
            db.AuditLogs.AddRange(
                Log(org, "ACCESS_REVIEWED", $"old@{org}.test", DateTime.UtcNow.AddDays(-30)),
                Log(org, "ACCESS_REVIEWED", $"boss@{org}.test", DateTime.UtcNow.AddHours(-1)));
            await db.SaveChangesAsync();
        }

        await using var read = _fx.NewDb();
        var report = await AccessReview.BuildAsync(read, org, null, null);
        Assert.Equal($"boss@{org}.test", report.LastReview?.By);
    }
}