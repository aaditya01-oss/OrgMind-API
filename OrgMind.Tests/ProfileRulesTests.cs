using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

public class ProfileRulesTests
{
    private static string? Check(string? name = null, string? title = null, string? phone = null, string? bio = null)
        => ProfileRules.Validate(new ProfileRequest(name, title, phone, bio), out _);

    [Fact]
    public void A_normal_profile_is_accepted()
        => Assert.Null(Check("Aaditya Ghimire", "Engineer", "+977 98-1234567", "Hello there."));

    [Fact]
    public void Empty_text_becomes_null()
    {
        ProfileRules.Validate(new ProfileRequest("   ", "", null, "\n \n"), out var clean);
        Assert.Null(clean.DisplayName);
        Assert.Null(clean.JobTitle);
        Assert.Null(clean.Bio);
    }

    [Fact]
    public void Too_long_values_are_refused()
    {
        Assert.NotNull(Check(name: new string('a', ProfileRules.MaxName + 1)));
        Assert.NotNull(Check(title: new string('a', ProfileRules.MaxTitle + 1)));
        Assert.NotNull(Check(bio: new string('a', ProfileRules.MaxBio + 1)));
    }

    [Fact]
    public void A_bio_of_exactly_the_limit_is_accepted()
        => Assert.Null(Check(bio: new string('a', ProfileRules.MaxBio)));

    [Theory]
    [InlineData("abc-defg")]
    [InlineData("12")]
    [InlineData("<script>1234</script>")]
    public void Bad_phone_numbers_are_refused(string phone)
        => Assert.NotNull(Check(phone: phone));

    [Fact]
    public void Line_breaks_stay_in_the_bio_but_not_in_the_name()
    {
        ProfileRules.Validate(new ProfileRequest("A\nB", null, null, "one\r\ntwo"), out var clean);
        Assert.Equal("A B", clean.DisplayName);
        Assert.Equal("one\ntwo", clean.Bio);
    }

    [Fact]
    public void Control_characters_are_removed()
    {
        ProfileRules.Validate(new ProfileRequest("Ab\u0000c\u0007", null, null, null), out var clean);
        Assert.Equal("Abc", clean.DisplayName);
    }
}