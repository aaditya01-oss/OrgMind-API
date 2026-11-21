using Microsoft.EntityFrameworkCore;
using OrgMind.API.Models;
using OrgMind.API.Services;

namespace OrgMind.Tests;

[Collection("real-db")]
[Trait("Category", "RealDb")]
public class RealDepartmentCheckTests
{
    private readonly RealDbFixture _fx;
    public RealDepartmentCheckTests(RealDbFixture fx) => _fx = fx;

    private static string NewOrg() => "org-" + Guid.NewGuid().ToString("N")[..10];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task No_department_is_always_fine(string? name)
    {
        await using var db = _fx.NewDb();
        Assert.Null(await DepartmentOps.CheckKnownAsync(db, NewOrg(), name));
    }

    [Fact]
    public async Task A_known_department_is_accepted_in_any_spelling()
    {
        var org = NewOrg();
        await using var db = _fx.NewDb();
        db.Departments.Add(new Department { OrgId = org, Name = "finance", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        Assert.Null(await DepartmentOps.CheckKnownAsync(db, org, "finance"));
        Assert.Null(await DepartmentOps.CheckKnownAsync(db, org, "  Finance "));
    }

    [Fact]
    public async Task A_mistyped_department_is_refused()
    {
        var org = NewOrg();
        await using var db = _fx.NewDb();
        db.Departments.Add(new Department { OrgId = org, Name = "finance", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        Assert.NotNull(await DepartmentOps.CheckKnownAsync(db, org, "financ"));
    }

    [Fact]
    public async Task Another_companys_department_does_not_count()
    {
        var mine = NewOrg();
        var other = NewOrg();
        await using var db = _fx.NewDb();
        db.Departments.Add(new Department { OrgId = other, Name = "legal", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        Assert.NotNull(await DepartmentOps.CheckKnownAsync(db, mine, "legal"));
    }

    [Fact]
    public async Task A_department_already_in_use_by_older_data_is_accepted()
    {
        var org = NewOrg();
        await using (var db = _fx.NewDb())
        {
            // Someone already works in "support", but nobody ever added it to the list.
            db.Users.Add(new User { Email = $"a@{org}.test", PasswordHash = "x", OrgId = org, Role = "member", Department = "support" });
            await db.SaveChangesAsync();
        }

        await using var read = _fx.NewDb();
        Assert.Null(await DepartmentOps.CheckKnownAsync(read, org, "support"));
        Assert.True(await DepartmentOps.ExistsAsync(read, org, "support"));   // and it is now on the list
    }
}