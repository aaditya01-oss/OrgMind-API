using Microsoft.EntityFrameworkCore;
using OrgMind.API.Models;
using OrgMind.API.Services;

namespace OrgMind.Tests;

// These check rules the REAL database enforces, which the in-memory test database ignores.
[Collection("real-db")]
[Trait("Category", "RealDb")]
public class RealConstraintTests
{
    private readonly RealDbFixture _fx;
    public RealConstraintTests(RealDbFixture fx) => _fx = fx;

    private static string NewOrg() => "org-" + Guid.NewGuid().ToString("N")[..10];

    private static StaffMessage Message(string org, string body) => new()
    {
        OrgId = org,
        Channel = "dept:hr",
        Author = $"a@{org}.test",
        Body = body,
        CreatedAt = DateTime.UtcNow
    };

    private static User Person(string org, string email, string? employeeId = null) => new()
    {
        Email = email,
        PasswordHash = "x",
        OrgId = org,
        Role = "member",
        EmployeeId = employeeId
    };

    // ---- messages ----

    [Fact]
    public async Task An_empty_message_is_refused_by_the_database()
    {
        await using var db = _fx.NewDb();
        db.Set<StaffMessage>().Add(Message(NewOrg(), ""));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_message_over_1000_characters_is_refused_by_the_database()
    {
        await using var db = _fx.NewDb();
        db.Set<StaffMessage>().Add(Message(NewOrg(), new string('x', 1001)));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_message_of_exactly_1000_characters_is_accepted()
    {
        await using var db = _fx.NewDb();
        db.Set<StaffMessage>().Add(Message(NewOrg(), new string('x', 1000)));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_removed_message_placeholder_is_accepted_by_the_database()
    {
        var org = NewOrg();
        await using (var db = _fx.NewDb())
        {
            var m = Message(org, "hello");
            db.Set<StaffMessage>().Add(m);
            await db.SaveChangesAsync();

            m.Body = StaffMessageActions.RemovedBody;   // exactly what "unsend" does
            m.RemovedAt = DateTime.UtcNow;
            m.RemovedBy = m.Author;
            await db.SaveChangesAsync();
        }

        await using var fresh = _fx.NewDb();
        var saved = await fresh.Set<StaffMessage>().SingleAsync(x => x.OrgId == org);
        Assert.Equal(StaffMessageActions.RemovedBody, saved.Body);
        Assert.NotNull(saved.RemovedAt);
    }

    // ---- people ----

    [Fact]
    public async Task Two_emails_that_differ_only_in_capitals_are_refused()
    {
        var org = NewOrg();
        await using var db = _fx.NewDb();
        db.Users.Add(Person(org, $"Sam@{org}.test"));
        await db.SaveChangesAsync();

        await using var second = _fx.NewDb();
        second.Users.Add(Person(org, $"sam@{org}.test"));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task An_employee_id_cannot_repeat_in_the_same_company()
    {
        var org = NewOrg();
        await using var db = _fx.NewDb();
        db.Users.Add(Person(org, $"one@{org}.test", "EMP-1"));
        await db.SaveChangesAsync();

        await using var second = _fx.NewDb();
        second.Users.Add(Person(org, $"two@{org}.test", "EMP-1"));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_employee_id_is_fine_in_a_different_company()
    {
        var a = NewOrg();
        var b = NewOrg();
        await using var db = _fx.NewDb();
        db.Users.Add(Person(a, $"one@{a}.test", "EMP-1"));
        db.Users.Add(Person(b, $"one@{b}.test", "EMP-1"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Many_people_may_have_no_employee_id()
    {
        var org = NewOrg();
        await using var db = _fx.NewDb();
        db.Users.Add(Person(org, $"one@{org}.test"));
        db.Users.Add(Person(org, $"two@{org}.test"));
        await db.SaveChangesAsync();
    }
}