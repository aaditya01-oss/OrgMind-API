using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;
using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

public class DepartmentsTests
{
    private static OrgMindDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<OrgMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new OrgMindDbContext(options);
    }

    private static User Person(string org, string email, string? dept) =>
        new() { OrgId = org, Email = email, PasswordHash = "x", Role = "member", Department = dept };

    // Two companies, both with an "hr" department, so every test also proves they stay apart.
    private static async Task<OrgMindDbContext> SeedAsync()
    {
        var db = NewDb();
        db.Departments.AddRange(
            new Department { OrgId = "o1", Name = "hr" },
            new Department { OrgId = "o1", Name = "it" },
            new Department { OrgId = "o2", Name = "hr" });
        db.Users.AddRange(
            Person("o1", "a@x.com", "hr"), Person("o1", "b@x.com", "hr"),
            Person("o1", "c@x.com", "it"), Person("o2", "z@y.com", "hr"));
        db.StaffMessages.Add(new StaffMessage { OrgId = "o1", Channel = "dept:hr", Author = "a@x.com", Body = "hi" });
        db.StaffReads.Add(new StaffRead { OrgId = "o1", UserEmail = "a@x.com", Channel = "dept:hr", LastReadId = 1 });
        await db.SaveChangesAsync();
        return db;
    }

    // ---- names ----

    [Fact]
    public void Names_are_cleaned_the_same_way_every_time()
    {
        Assert.Null(DepartmentRules.Clean("  Human   Resources ", out var name));
        Assert.Equal("human resources", name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>")]
    [InlineData("-leading dash")]
    [InlineData("Unassigned")]
    [InlineData("none")]
    public void Bad_names_are_refused(string input)
        => Assert.NotNull(DepartmentRules.Clean(input, out _));

    [Fact]
    public void A_name_that_is_too_long_is_refused()
        => Assert.NotNull(DepartmentRules.Clean(new string('a', DepartmentRules.MaxLength + 1), out _));

    // ---- add ----

    [Fact]
    public async Task Adding_works_once_and_a_duplicate_is_refused()
    {
        var db = await SeedAsync();
        Assert.Equal(200, (await DepartmentOps.AddAsync(db, "o1", "finance")).Status);
        Assert.Equal(409, (await DepartmentOps.AddAsync(db, "o1", "finance")).Status);
        Assert.Equal(200, (await DepartmentOps.AddAsync(db, "o2", "finance")).Status);   // another company: allowed
    }

    // ---- rename ----

    [Fact]
    public async Task Renaming_moves_people_chat_and_read_markers_of_that_company_only()
    {
        var db = await SeedAsync();

        var result = await DepartmentOps.RenameAsync(db, "o1", "hr", "people");

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.PeopleMoved);
        Assert.Equal(2, await db.Users.CountAsync(u => u.OrgId == "o1" && u.Department == "people"));
        Assert.Equal(0, await db.Users.CountAsync(u => u.OrgId == "o1" && u.Department == "hr"));
        Assert.Equal("dept:people", (await db.StaffMessages.SingleAsync()).Channel);
        Assert.Equal("dept:people", (await db.StaffReads.SingleAsync()).Channel);
        Assert.True(await DepartmentOps.ExistsAsync(db, "o1", "people"));
        Assert.False(await DepartmentOps.ExistsAsync(db, "o1", "hr"));

        // The other company is untouched.
        Assert.Equal("hr", (await db.Users.SingleAsync(u => u.OrgId == "o2")).Department);
        Assert.True(await DepartmentOps.ExistsAsync(db, "o2", "hr"));
    }

    [Fact]
    public async Task Renaming_to_an_existing_or_unknown_or_same_name_is_refused()
    {
        var db = await SeedAsync();
        Assert.Equal(409, (await DepartmentOps.RenameAsync(db, "o1", "hr", "it")).Status);
        Assert.Equal(404, (await DepartmentOps.RenameAsync(db, "o1", "nope", "x")).Status);
        Assert.Equal(400, (await DepartmentOps.RenameAsync(db, "o1", "hr", "hr")).Status);
    }

    // ---- delete ----

    [Fact]
    public async Task An_unused_department_can_be_deleted_and_its_chat_goes_with_it()
    {
        var db = await SeedAsync();
        await DepartmentOps.AddAsync(db, "o1", "empty");
        db.StaffMessages.Add(new StaffMessage { OrgId = "o1", Channel = "dept:empty", Author = "a@x.com", Body = "old" });
        await db.SaveChangesAsync();

        var result = await DepartmentOps.DeleteAsync(db, "o1", "empty", null, 0);

        Assert.Equal(200, result.Status);
        Assert.False(await DepartmentOps.ExistsAsync(db, "o1", "empty"));
        Assert.Equal(0, await db.StaffMessages.CountAsync(m => m.Channel == "dept:empty"));
    }

    [Fact]
    public async Task A_department_in_use_needs_somewhere_to_move_everything()
    {
        var db = await SeedAsync();

        var blocked = await DepartmentOps.DeleteAsync(db, "o1", "hr", null, 0);
        Assert.Equal(409, blocked.Status);
        Assert.Equal(2, blocked.Usage!.People);
        Assert.True(await DepartmentOps.ExistsAsync(db, "o1", "hr"));   // nothing was deleted

        Assert.Equal(400, (await DepartmentOps.DeleteAsync(db, "o1", "hr", "hr", 0)).Status);
        Assert.Equal(400, (await DepartmentOps.DeleteAsync(db, "o1", "hr", "ghost", 0)).Status);
    }

    [Fact]
    public async Task Documents_also_count_as_use()
    {
        var db = await SeedAsync();
        await DepartmentOps.AddAsync(db, "o1", "legal");
        Assert.Equal(409, (await DepartmentOps.DeleteAsync(db, "o1", "legal", null, 3)).Status);
    }

    [Fact]
    public async Task Deleting_with_a_destination_moves_people_and_removes_the_department()
    {
        var db = await SeedAsync();

        var result = await DepartmentOps.DeleteAsync(db, "o1", "hr", "it", 0);

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.PeopleMoved);
        Assert.Equal(3, await db.Users.CountAsync(u => u.OrgId == "o1" && u.Department == "it"));
        Assert.False(await DepartmentOps.ExistsAsync(db, "o1", "hr"));
        Assert.Equal(0, await db.StaffMessages.CountAsync());     // the old hr group chat is gone
        Assert.Equal("hr", (await db.Users.SingleAsync(u => u.OrgId == "o2")).Department);   // other company untouched
    }

    // ---- older data ----

    [Fact]
    public async Task Departments_found_in_older_data_are_added_to_the_list()
    {
        var db = NewDb();
        db.Users.Add(Person("o1", "a@x.com", "Legacy Dept "));
        await db.SaveChangesAsync();

        await DepartmentOps.EnsureKnownAsync(db, "o1");

        Assert.True(await DepartmentOps.ExistsAsync(db, "o1", "legacy dept"));
    }
}