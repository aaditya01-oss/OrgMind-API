using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;
using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

// Opens conversations against a throwaway in-memory database (never your real one).
public class ThreadAccessTests
{
    static Caller Admin() => new("org1", "admin@acme.test", "admin", null);
    static Caller Employee(string email, string? dept) => new("org1", email, "member", dept);
    static Caller Customer(string email) => new("org1", email, "customer", null);

    static OrgMindDbContext NewDb() =>
        new(new DbContextOptionsBuilder<OrgMindDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    static async Task<int> AddTicket(OrgMindDbContext db, string createdBy, string? dept, string status = "open")
    {
        var t = new Ticket { OrgId = "org1", CreatedBy = createdBy, Issue = "Card swallowed", Department = dept, Status = status };
        db.Tickets.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    static async Task<int> AddSubmission(OrgMindDbContext db, string submittedBy, string? dept, string status = "new")
    {
        var s = new FormSubmission
        {
            OrgId = "org1", TemplateId = 1, TemplateName = "Loan request",
            SubmittedBy = submittedBy, Department = dept, Data = "{}", Status = status
        };
        db.FormSubmissions.Add(s);
        await db.SaveChangesAsync();
        return s.Id;
    }

    [Fact]
    public async Task Owner_can_open_their_own_ticket_conversation()
    {
        using var db = NewDb();
        var id = await AddTicket(db, "maya@customer.test", "support");
        var access = await Threads.OpenAsync(db, Customer("maya@customer.test"), "ticket", id);
        Assert.NotNull(access);
        Assert.True(access!.IsOwner);
        Assert.False(access.IsStaff);
    }

    [Fact]
    public async Task Another_customer_cannot_open_the_conversation()
    {
        using var db = NewDb();
        var id = await AddTicket(db, "maya@customer.test", "support");
        Assert.Null(await Threads.OpenAsync(db, Customer("other@customer.test"), "ticket", id));
    }

    [Fact]
    public async Task Employee_of_the_handling_department_gets_staff_access()
    {
        using var db = NewDb();
        var id = await AddTicket(db, "maya@customer.test", "support");
        var access = await Threads.OpenAsync(db, Employee("sita@acme.test", "support"), "ticket", id);
        Assert.NotNull(access);
        Assert.True(access!.IsStaff);
        Assert.False(access.IsOwner);
    }

    [Fact]
    public async Task Employee_of_another_department_cannot_open_it()
    {
        using var db = NewDb();
        var id = await AddTicket(db, "maya@customer.test", "support");
        Assert.Null(await Threads.OpenAsync(db, Employee("ram@acme.test", "loans"), "ticket", id));
    }

    [Fact]
    public async Task Admin_gets_staff_access()
    {
        using var db = NewDb();
        var id = await AddTicket(db, "maya@customer.test", "support");
        var access = await Threads.OpenAsync(db, Admin(), "ticket", id);
        Assert.NotNull(access);
        Assert.True(access!.IsStaff);
    }

    [Fact]
    public async Task Employee_who_raised_the_ticket_is_the_owner_and_not_staff_on_it()
    {
        using var db = NewDb();
        var id = await AddTicket(db, "sita@acme.test", "support");
        var access = await Threads.OpenAsync(db, Employee("sita@acme.test", "support"), "ticket", id);
        Assert.NotNull(access);
        Assert.True(access!.IsOwner);
        Assert.False(access.IsStaff);
    }

    [Fact]
    public async Task Resolved_ticket_counts_as_closed()
    {
        using var db = NewDb();
        var id = await AddTicket(db, "maya@customer.test", "support", "resolved");
        var access = await Threads.OpenAsync(db, Customer("maya@customer.test"), "ticket", id);
        Assert.True(access!.IsClosed);
    }

    [Fact]
    public async Task Someone_from_another_organization_gets_nothing()
    {
        using var db = NewDb();
        var id = await AddTicket(db, "maya@customer.test", "support");
        var outsider = new Caller("org2", "admin@other.test", "admin", null);
        Assert.Null(await Threads.OpenAsync(db, outsider, "ticket", id));
    }

    [Fact]
    public async Task Missing_id_and_unknown_kind_give_nothing()
    {
        using var db = NewDb();
        Assert.Null(await Threads.OpenAsync(db, Admin(), "ticket", 999));
        Assert.Null(await Threads.OpenAsync(db, Admin(), "something-else", 1));
    }

    [Fact]
    public async Task Customer_cannot_open_someone_elses_submission_conversation()
    {
        using var db = NewDb();
        var id = await AddSubmission(db, "maya@customer.test", "loans");
        Assert.Null(await Threads.OpenAsync(db, Customer("other@customer.test"), "submission", id));
        Assert.NotNull(await Threads.OpenAsync(db, Customer("maya@customer.test"), "submission", id));
    }

    [Fact]
    public async Task Submission_conversation_is_closed_once_the_request_is_decided()
    {
        using var db = NewDb();
        var id = await AddSubmission(db, "maya@customer.test", "loans", "rejected");
        var access = await Threads.OpenAsync(db, Customer("maya@customer.test"), "submission", id);
        Assert.True(access!.IsClosed);
    }
}