using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

public class PasswordSafetyTests
{
    [Fact]
    public void A_temporary_password_has_the_right_length()
    {
        Assert.Equal(12, TemporaryPassword.Generate().Length);
        Assert.Equal(20, TemporaryPassword.Generate(20).Length);
    }

    [Fact]
    public void It_never_contains_look_alike_characters()
    {
        for (var i = 0; i < 500; i++)
        {
            var password = TemporaryPassword.Generate();
            Assert.DoesNotContain(password, c => "0Oo1lI".Contains(c));
            Assert.All(password, c => Assert.True(char.IsLetterOrDigit(c)));
        }
    }

    [Fact]
    public void Two_passwords_are_never_the_same()
    {
        var many = Enumerable.Range(0, 300).Select(_ => TemporaryPassword.Generate()).ToHashSet();
        Assert.Equal(300, many.Count);
    }

    [Theory]
    [InlineData("/me", true)]
    [InlineData("/ME", true)]
    [InlineData("/me/password", true)]
    [InlineData("/me/avatar", false)]
    [InlineData("/me/activity", false)]
    [InlineData("/me/password/extra", false)]
    [InlineData("/tickets", false)]
    [InlineData("/admin/users", false)]
    [InlineData("/chat/channels", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void While_a_reset_password_is_in_use_only_two_requests_are_allowed(string? path, bool expected)
        => Assert.Equal(expected, PasswordChangeGate.Allows(path));
}