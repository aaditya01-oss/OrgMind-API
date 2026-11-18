using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using OrgMind.API.Models;
using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

public class StaffMessageActionsTests
{
    private static readonly Caller HrA = new("o1", "a@x.com", "member", "hr");
    private static readonly Caller HrD = new("o1", "d@x.com", "member", "hr");
    private static readonly Caller ItB = new("o1", "b@x.com", "member", "it");
    private static readonly Caller Admin = new("o1", "boss@x.com", "admin", null);
    private static readonly Caller Customer = new("o1", "cust@x.com", "customer", null);
    private static readonly Caller OtherAdmin = new("o2", "boss2@y.com", "admin", null);

    // 1: hr group message by A   2: private message A -> B   3: hr group message by D
    private static async Task<(OrgMindDbContext Db, int Group, int Direct, int ByD)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<OrgMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new OrgMindDbContext(options);

        var group = new StaffMessage { OrgId = "o1", Channel = "dept:hr", Author = "a@x.com", Body = "secret group text" };
        var direct = new StaffMessage { OrgId = "o1", Channel = "dm:a@x.com|b@x.com", Author = "a@x.com", Body = "secret direct text" };
        var byD = new StaffMessage { OrgId = "o1", Channel = "dept:hr", Author = "d@x.com", Body = "text from d" };
        db.StaffMessages.AddRange(group, direct, byD);
        await db.SaveChangesAsync();
        return (db, group.Id, direct.Id, byD.Id);
    }

    [Fact]
    public async Task The_author_can_unsend_and_the_text_is_really_gone()
    {
        var (db, group, _, _) = await SeedAsync();

        var result = await StaffMessageActions.RemoveAsync(db, HrA, group);

        Assert.Equal(200, result.Status);
        Assert.True(result.Changed);
        Assert.False(result.ByAdmin);
        var row = await db.StaffMessages.SingleAsync(m => m.Id == group);
        Assert.Equal(StaffMessageActions.RemovedBody, row.Body);
        Assert.NotNull(row.RemovedAt);
        Assert.Equal("a@x.com", row.RemovedBy);
    }

    [Fact]
    public async Task Unsending_twice_is_harmless()
    {
        var (db, group, _, _) = await SeedAsync();
        await StaffMessageActions.RemoveAsync(db, HrA, group);

        var again = await StaffMessageActions.RemoveAsync(db, HrA, group);

        Assert.Equal(200, again.Status);
        Assert.False(again.Changed);   // nothing new to record in the audit log
    }

    [Fact]
    public async Task A_colleague_cannot_remove_someone_elses_message()
    {
        var (db, _, _, byD) = await SeedAsync();

        var result = await StaffMessageActions.RemoveAsync(db, HrA, byD);

        Assert.Equal(403, result.Status);
        Assert.Equal("text from d", (await db.StaffMessages.SingleAsync(m => m.Id == byD)).Body);
    }

    [Fact]
    public async Task An_admin_can_remove_a_department_group_message()
    {
        var (db, _, _, byD) = await SeedAsync();

        var result = await StaffMessageActions.RemoveAsync(db, Admin, byD);

        Assert.Equal(200, result.Status);
        Assert.True(result.ByAdmin);
        Assert.Equal("department", result.Kind);
        Assert.Equal("hr", result.Department);
    }

    [Fact]
    public async Task An_admin_cannot_touch_other_peoples_private_messages_and_is_not_told_they_exist()
    {
        var (db, _, direct, _) = await SeedAsync();

        var result = await StaffMessageActions.RemoveAsync(db, Admin, direct);

        Assert.Equal(404, result.Status);
        Assert.Equal("secret direct text", (await db.StaffMessages.SingleAsync(m => m.Id == direct)).Body);
    }

    [Fact]
    public async Task Customers_and_other_companies_are_refused()
    {
        var (db, group, _, _) = await SeedAsync();

        Assert.Equal(403, (await StaffMessageActions.RemoveAsync(db, Customer, group)).Status);
        Assert.Equal(404, (await StaffMessageActions.RemoveAsync(db, OtherAdmin, group)).Status);
        Assert.Equal("secret group text", (await db.StaffMessages.SingleAsync(m => m.Id == group)).Body);
    }

    [Fact]
    public async Task A_removed_message_reaches_readers_without_its_text()
    {
        var (db, group, _, _) = await SeedAsync();
        await StaffMessageActions.RemoveAsync(db, HrA, group);

        var json = JsonSerializer.Serialize(await StaffChat.ReadAsync(db, HrD, "dept:hr"));

        Assert.DoesNotContain("secret group text", json);
        Assert.Contains("\"removed\":true", json);
        Assert.Contains("text from d", json);
    }

    [Fact]
    public async Task Delete_for_me_hides_it_only_from_me()
    {
        var (db, _, _, byD) = await SeedAsync();

        var result = await StaffMessageActions.HideAsync(db, HrA, byD);

        Assert.Equal(200, result.Status);
        Assert.DoesNotContain("text from d", JsonSerializer.Serialize(await StaffChat.ReadAsync(db, HrA, "dept:hr")));
        Assert.Contains("text from d", JsonSerializer.Serialize(await StaffChat.ReadAsync(db, HrD, "dept:hr")));
        Assert.Equal("text from d", (await db.StaffMessages.SingleAsync(m => m.Id == byD)).Body);   // not deleted
    }

    [Fact]
    public async Task Hiding_a_message_of_a_conversation_I_cannot_read_is_refused()
    {
        var (db, group, direct, _) = await SeedAsync();

        Assert.Equal(404, (await StaffMessageActions.HideAsync(db, ItB, group)).Status);       // another department
        Assert.Equal(404, (await StaffMessageActions.HideAsync(db, Admin, direct)).Status);    // someone's private chat
        Assert.Equal(403, (await StaffMessageActions.HideAsync(db, Customer, group)).Status);
    }

    [Fact]
    public async Task Hiding_twice_is_harmless()
    {
        var (db, _, _, byD) = await SeedAsync();
        await StaffMessageActions.HideAsync(db, HrA, byD);

        var again = await StaffMessageActions.HideAsync(db, HrA, byD);

        Assert.Equal(200, again.Status);
        Assert.Equal(1, await db.HiddenMessages.CountAsync());
    }
}