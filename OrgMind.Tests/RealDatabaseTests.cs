using Microsoft.EntityFrameworkCore;
using OrgMind.API.Models;
using OrgMind.API.Services;

namespace OrgMind.Tests;

[Collection("real-db")]
[Trait("Category", "RealDb")]
public class RealDatabaseTests
{
    private readonly RealDbFixture _fx;
    public RealDatabaseTests(RealDbFixture fx) => _fx = fx;

    private static string NewOrg() => "org-" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task The_must_change_password_flag_is_really_saved()
    {
        var org = NewOrg();
        await using (var db = _fx.NewDb())
        {
            db.Users.Add(new User { Email = $"a@{org}.test", PasswordHash = "x", OrgId = org, Role = "member", MustChangePassword = true });
            await db.SaveChangesAsync();
        }

        await using var fresh = _fx.NewDb();
        var saved = await fresh.Users.SingleAsync(u => u.OrgId == org);
        Assert.True(saved.MustChangePassword);
    }

    [Fact]
    public async Task Adding_the_same_department_twice_leaves_one_row()
    {
        var org = NewOrg();
        await using (var db = _fx.NewDb())
        {
            await DepartmentOps.AddAsync(db, org, "hr");
            await DepartmentOps.AddAsync(db, org, "hr");
        }

        await using var fresh = _fx.NewDb();
        Assert.Equal(1, await fresh.Departments.CountAsync(d => d.OrgId == org && d.Name == "hr"));
    }
}