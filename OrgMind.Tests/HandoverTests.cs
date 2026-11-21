using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OrgMind.API.Models;
using OrgMind.API.Services;

namespace OrgMind.Tests;

public class HandoverRuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hi")]
    public void A_missing_or_tiny_reason_is_refused(string? reason)
        => Assert.NotNull(HandoverRules.CheckReason(reason, out _));

    [Fact]
    public void A_reason_over_the_limit_is_refused()
        => Assert.NotNull(HandoverRules.CheckReason(new string('x', 301), out _));

    [Fact]
    public void A_normal_reason_is_accepted_and_trimmed()
    {
        Assert.Null(HandoverRules.CheckReason("  Needs the network team  ", out var clean));
        Assert.Equal("Needs the network team", clean);
    }
}

[Collection("real-db")]
[Trait("Category", "RealDb")]
public class RealHandoverTests
{
    private readonly RealDbFixture _fx;
    public RealHandoverTests(RealDbFixture fx) => _fx = fx;

    private static string NewOrg() => "org-" + Guid.NewGuid().ToString("N")[..10];

    private static Caller Who(string org, string name, string role, string? dept)
    {
        var claims = new List<Claim>
        {
            new("userId", "1"), new("orgId", org), new("email", $"{name}@{org}.test"), new("role", role)
        };
        if (dept is not null) claims.Add(new Claim("department", dept));
        return Caller.From(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
    }

    private static Ticket NewTicket(string org, string by, string? dept, string status = "open") => new()
    {
        OrgId = org, CreatedBy = by, Issue = "Cannot sign in to the portal", Priority = "medium",
        Status = status, Department = dept, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    // A company with departments hr and it, and one open ticket with hr.
    private async Task<(string Org, int TicketId)> SetupAsync(string status = "open")
    {
        var org = NewOrg();
        await using var db = _fx.NewDb();
        db.Departments.AddRange(
            new Department { OrgId = org, Name = "hr", CreatedAt = DateTime.UtcNow },
            new Department { OrgId = org, Name = "it", CreatedAt = DateTime.UtcNow });
        var ticket = NewTicket(org, $"cust@{org}.test", "hr", status);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return (org, ticket.Id);
    }

    [Fact]
    public async Task The_handling_department_can_hand_a_ticket_over_and_the_history_is_kept()
    {
        var (org, id) = await SetupAsync();
        await using (var db = _fx.NewDb())
        {
            var r = await HandoverOps.HandOverAsync(db, Who(org, "sam", "member", "hr"), "ticket", id, "IT", "Needs the network team");
            Assert.Equal(200, r.Status);
            Assert.Equal("hr", r.From);
            Assert.Equal("it", r.To);
        }

        await using var fresh = _fx.NewDb();
        Assert.Equal("it", (await fresh.Tickets.SingleAsync(t => t.Id == id)).Department);
        var h = await fresh.Handovers.SingleAsync(x => x.OrgId == org);
        Assert.Equal("hr", h.FromDepartment);
        Assert.Equal("it", h.ToDepartment);
        Assert.Equal("Needs the network team", h.Reason);
        Assert.Equal($"sam@{org}.test", h.HandedBy);
    }

    [Fact]
    public async Task An_admin_can_hand_over_any_ticket()
    {
        var (org, id) = await SetupAsync();
        await using var db = _fx.NewDb();
        var r = await HandoverOps.HandOverAsync(db, Who(org, "boss", "admin", null), "ticket", id, "it", "Better fit for IT");
        Assert.Equal(200, r.Status);
    }

    [Fact]
    public async Task Without_a_reason_nothing_moves()
    {
        var (org, id) = await SetupAsync();
        await using (var db = _fx.NewDb())
        {
            var r = await HandoverOps.HandOverAsync(db, Who(org, "sam", "member", "hr"), "ticket", id, "it", "  ");
            Assert.Equal(400, r.Status);
        }
        await using var fresh = _fx.NewDb();
        Assert.Equal("hr", (await fresh.Tickets.SingleAsync(t => t.Id == id)).Department);
        Assert.Equal(0, await fresh.Handovers.CountAsync(h => h.OrgId == org));
    }

    [Fact]
    public async Task A_department_that_does_not_exist_is_refused()
    {
        var (org, id) = await SetupAsync();
        await using var db = _fx.NewDb();
        var r = await HandoverOps.HandOverAsync(db, Who(org, "sam", "member", "hr"), "ticket", id, "legal", "Needs a lawyer");
        Assert.Equal(400, r.Status);
    }

    [Fact]
    public async Task Handing_over_to_the_same_department_is_refused()
    {
        var (org, id) = await SetupAsync();
        await using var db = _fx.NewDb();
        var r = await HandoverOps.HandOverAsync(db, Who(org, "sam", "member", "hr"), "ticket", id, "hr", "Just checking");
        Assert.Equal(400, r.Status);
    }

    [Fact]
    public async Task Someone_from_another_department_cannot_hand_it_over()
    {
        var (org, id) = await SetupAsync();
        await using var db = _fx.NewDb();
        var r = await HandoverOps.HandOverAsync(db, Who(org, "lee", "member", "it"), "ticket", id, "it", "Taking it myself");
        Assert.Equal(404, r.Status);
    }

    [Fact]
    public async Task A_customer_cannot_hand_anything_over()
    {
        var (org, id) = await SetupAsync();
        await using var db = _fx.NewDb();
        var r = await HandoverOps.HandOverAsync(db, Who(org, "cust", "customer", null), "ticket", id, "it", "Please move it");
        Assert.Equal(404, r.Status);
    }

    [Fact]
    public async Task Staff_cannot_hand_over_their_own_ticket()
    {
        var org = NewOrg();
        int id;
        await using (var db = _fx.NewDb())
        {
            db.Departments.AddRange(
                new Department { OrgId = org, Name = "hr", CreatedAt = DateTime.UtcNow },
                new Department { OrgId = org, Name = "it", CreatedAt = DateTime.UtcNow });
            var t = NewTicket(org, $"sam@{org}.test", "hr");
            db.Tickets.Add(t);
            await db.SaveChangesAsync();
            id = t.Id;
        }
        await using var read = _fx.NewDb();
        var r = await HandoverOps.HandOverAsync(read, Who(org, "sam", "member", "hr"), "ticket", id, "it", "Not mine to keep");
        Assert.Equal(403, r.Status);
    }

    [Fact]
    public async Task A_resolved_ticket_cannot_be_handed_over()
    {
        var (org, id) = await SetupAsync("resolved");
        await using var db = _fx.NewDb();
        var r = await HandoverOps.HandOverAsync(db, Who(org, "sam", "member", "hr"), "ticket", id, "it", "Too late for this");
        Assert.Equal(400, r.Status);
    }

    [Fact]
    public async Task Another_companys_ticket_is_never_found()
    {
        var (org, id) = await SetupAsync();
        var otherOrg = NewOrg();
        await using var db = _fx.NewDb();
        db.Departments.Add(new Department { OrgId = otherOrg, Name = "hr", CreatedAt = DateTime.UtcNow });
        db.Departments.Add(new Department { OrgId = otherOrg, Name = "it", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var r = await HandoverOps.HandOverAsync(db, Who(otherOrg, "boss", "admin", null), "ticket", id, "it", "Not even mine");
        Assert.Equal(404, r.Status);
    }
}