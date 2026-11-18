using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;
using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

public class StaffProfilesTests
{
    private static User Person(string org, string email, string role) => new()
    {
        OrgId = org, Email = email, PasswordHash = "x", Role = role,
        DisplayName = email.Split('@')[0], Phone = "9800000000", Bio = "A short bio."
    };

    private static async Task<OrgMindDbContext> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<OrgMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new OrgMindDbContext(options);
        db.Users.AddRange(
            Person("o1", "a@x.com", "member"),
            Person("o1", "boss@x.com", "admin"),
            Person("o1", "cust@x.com", "customer"),
            Person("o2", "other@y.com", "member"));
        await db.SaveChangesAsync();
        return db;
    }

    private static readonly Caller Employee = new("o1", "a@x.com", "member", "hr");
    private static readonly Caller Admin = new("o1", "boss@x.com", "admin", null);
    private static readonly Caller Customer = new("o1", "cust@x.com", "customer", null);

    [Fact]
    public async Task An_employee_can_see_an_admin_and_an_admin_can_see_an_employee()
    {
        var db = await SeedAsync();
        Assert.NotNull(await StaffProfiles.FindStaffAsync(db, Employee, "boss@x.com"));
        Assert.NotNull(await StaffProfiles.FindStaffAsync(db, Admin, "a@x.com"));
    }

    [Fact]
    public async Task Staff_never_see_a_customers_profile()
    {
        var db = await SeedAsync();
        Assert.Null(await StaffProfiles.FindStaffAsync(db, Employee, "cust@x.com"));
        Assert.Null(await StaffProfiles.FindStaffAsync(db, Admin, "cust@x.com"));
    }

    [Fact]
    public async Task Staff_never_see_another_companys_people()
    {
        var db = await SeedAsync();
        Assert.Null(await StaffProfiles.FindStaffAsync(db, Employee, "other@y.com"));
        Assert.Null(await StaffProfiles.FindStaffAsync(db, Admin, "other@y.com"));
    }

    [Fact]
    public async Task A_customer_sees_nobody()
    {
        var db = await SeedAsync();
        Assert.False(StaffProfiles.MayView(Customer));
        Assert.Null(await StaffProfiles.FindStaffAsync(db, Customer, "boss@x.com"));
        Assert.Null(await StaffProfiles.FindStaffAsync(db, new Caller("o1", null, null, null), "boss@x.com"));
    }

    [Fact]
    public async Task Empty_and_oddly_written_emails_are_handled()
    {
        var db = await SeedAsync();
        Assert.Null(await StaffProfiles.FindStaffAsync(db, Employee, ""));
        Assert.Null(await StaffProfiles.FindStaffAsync(db, Employee, null));
        Assert.NotNull(await StaffProfiles.FindStaffAsync(db, Employee, "  BOSS@x.com "));
    }

    [Fact]
    public async Task The_card_never_contains_private_fields()
    {
        var db = await SeedAsync();
        var person = await StaffProfiles.FindStaffAsync(db, Employee, "boss@x.com");
        var names = StaffProfiles.CardOf(person!).GetType().GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("phone", names);
        Assert.DoesNotContain("passwordHash", names);
        Assert.Contains("bio", names);
        Assert.Contains("displayName", names);
    }
}