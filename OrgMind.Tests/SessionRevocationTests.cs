using Microsoft.EntityFrameworkCore;
using OrgMind.API.Models;
using OrgMind.API.Services;

namespace OrgMind.Tests;

public class SessionRevocationTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    public void The_version_is_read_from_the_token(string? claim, int expected)
        => Assert.Equal(expected, SessionRevocation.ReadVersion(claim));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, false)]   // the password changed after this token was made
    [InlineData(null, 0, false)]  // the person no longer exists
    public void A_token_is_good_only_while_its_version_is_current(int? current, int inToken, bool expected)
        => Assert.Equal(expected, SessionRevocation.IsStillValid(current, inToken));
}

[Collection("real-db")]
[Trait("Category", "RealDb")]
public class RealSessionTests
{
    private readonly RealDbFixture _fx;
    public RealSessionTests(RealDbFixture fx) => _fx = fx;

    [Fact]
    public async Task A_new_person_starts_at_zero_and_the_counter_really_saves()
    {
        var org = "org-" + Guid.NewGuid().ToString("N")[..10];
        await using (var db = _fx.NewDb())
        {
            db.Users.Add(new User { Email = $"a@{org}.test", PasswordHash = "x", OrgId = org, Role = "member" });
            await db.SaveChangesAsync();
        }

        await using (var db = _fx.NewDb())
        {
            var u = await db.Users.SingleAsync(x => x.OrgId == org);
            Assert.Equal(0, u.TokenVersion);
            u.TokenVersion++;
            await db.SaveChangesAsync();
        }

        await using var fresh = _fx.NewDb();
        Assert.Equal(1, (await fresh.Users.SingleAsync(x => x.OrgId == org)).TokenVersion);
    }
}