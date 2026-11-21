using System.Security.Cryptography;
using System.Text.Json;
using OrgMind.API.Services;

namespace OrgMind.Tests;

public class FieldEncryptionTests
{
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void A_value_survives_a_round_trip_and_is_not_readable_while_stored()
    {
        var key = NewKey();
        var stored = FieldEncryption.Encrypt("12-34-56-78901", key, "org|cid");

        Assert.StartsWith(FieldEncryption.Prefix, stored);
        Assert.DoesNotContain("12-34-56", stored);
        Assert.Equal("12-34-56-78901", FieldEncryption.Decrypt(stored, key, "org|cid"));
    }

    [Fact]
    public void The_same_value_is_stored_differently_each_time()
    {
        var key = NewKey();
        Assert.NotEqual(FieldEncryption.Encrypt("same", key, "c"), FieldEncryption.Encrypt("same", key, "c"));
    }

    [Fact]
    public void Non_english_text_round_trips()
    {
        var key = NewKey();
        var text = "\u0928\u0947\u092A\u093E\u0932 \u2013 caf\u00E9";
        Assert.Equal(text, FieldEncryption.Decrypt(FieldEncryption.Encrypt(text, key, "c"), key, "c"));
    }

    [Fact]
    public void A_value_cannot_be_read_with_another_key()
        => Assert.ThrowsAny<CryptographicException>(() =>
            FieldEncryption.Decrypt(FieldEncryption.Encrypt("secret", NewKey(), "c"), NewKey(), "c"));

    [Fact]
    public void A_value_cannot_be_moved_to_another_company_or_question()
    {
        var key = NewKey();
        var stored = FieldEncryption.Encrypt("secret", key, "orgA|cid");
        Assert.ThrowsAny<CryptographicException>(() => FieldEncryption.Decrypt(stored, key, "orgB|cid"));
        Assert.ThrowsAny<CryptographicException>(() => FieldEncryption.Decrypt(stored, key, "orgA|other"));
    }

    [Fact]
    public void A_changed_value_is_detected()
    {
        var key = NewKey();
        var stored = FieldEncryption.Encrypt("secret", key, "c");
        var bytes = Convert.FromBase64String(stored.Substring(FieldEncryption.Prefix.Length));
        bytes[^1] ^= 1;   // flip one bit
        var tampered = FieldEncryption.Prefix + Convert.ToBase64String(bytes);
        Assert.ThrowsAny<CryptographicException>(() => FieldEncryption.Decrypt(tampered, key, "c"));
    }

    [Fact]
    public void Old_unscrambled_values_are_returned_as_they_are()
        => Assert.Equal("plain old value", FieldEncryption.Decrypt("plain old value", NewKey(), "c"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64 !!")]
    [InlineData("c2hvcnQ=")]   // valid base64 but only 5 bytes
    public void A_bad_key_setting_stops_the_app(string? setting)
        => Assert.Throws<InvalidOperationException>(() => FieldEncryption.ParseKey(setting));

    [Fact]
    public void A_good_key_setting_is_accepted()
        => Assert.Equal(32, FieldEncryption.ParseKey(Convert.ToBase64String(NewKey())).Length);
}

public class AnswerProtectionTests
{
    private const string Fields =
        "[{\"key\":\"cid\",\"label\":\"Citizenship\",\"type\":\"text\",\"sensitive\":true}," +
        "{\"key\":\"name\",\"label\":\"Name\",\"type\":\"text\"}]";

    private static FieldProtector NewProtector() => new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Only_sensitive_answers_are_scrambled()
    {
        var p = NewProtector();
        var stored = AnswerProtection.Protect(Fields, "{\"cid\":\"12-34\",\"name\":\"Sam\"}", p, "org1");

        Assert.DoesNotContain("12-34", stored);
        Assert.Contains("Sam", stored);

        var read = AnswerProtection.ReadAll(stored, Fields, p, "org1");
        Assert.Equal("12-34", read["cid"]);
        Assert.Equal("Sam", read["name"]);
    }

    [Fact]
    public void A_form_with_no_sensitive_questions_is_stored_unchanged()
    {
        var plain = "{\"name\":\"Sam\"}";
        Assert.Equal(plain, AnswerProtection.Protect("[{\"key\":\"name\",\"type\":\"text\"}]", plain, NewProtector(), "org1"));
    }

    [Fact]
    public void An_empty_sensitive_answer_stays_empty()
    {
        var p = NewProtector();
        var stored = AnswerProtection.Protect(Fields, "{\"cid\":\"\",\"name\":\"Sam\"}", p, "org1");
        Assert.Equal("", AnswerProtection.ReadAll(stored, Fields, p, "org1")["cid"]);
    }

    [Fact]
    public void Answers_stored_before_encryption_existed_are_still_readable()
    {
        var read = AnswerProtection.ReadAll("{\"cid\":\"old-plain\",\"name\":\"Sam\"}", Fields, NewProtector(), "org1");
        Assert.Equal("old-plain", read["cid"]);
    }

    [Fact]
    public void Another_company_cannot_read_a_sensitive_answer()
    {
        var p = NewProtector();
        var stored = AnswerProtection.Protect(Fields, "{\"cid\":\"12-34\",\"name\":\"Sam\"}", p, "org1");
        Assert.ThrowsAny<CryptographicException>(() => AnswerProtection.ReadAll(stored, Fields, p, "org2"));
    }

    [Fact]
    public void A_question_made_non_sensitive_later_is_still_readable()
    {
        var p = NewProtector();
        var stored = AnswerProtection.Protect(Fields, "{\"cid\":\"12-34\",\"name\":\"Sam\"}", p, "org1");
        var relaxed = "[{\"key\":\"cid\",\"label\":\"Citizenship\",\"type\":\"text\"},{\"key\":\"name\",\"type\":\"text\"}]";
        Assert.Equal("12-34", AnswerProtection.ReadAll(stored, relaxed, p, "org1")["cid"]);
    }

    [Fact]
    public void Ordinary_text_that_looks_like_scrambled_text_does_not_break_reading()
    {
        var typed = JsonSerializer.Serialize(new Dictionary<string, string> { ["name"] = "enc:v1:hello" });
        Assert.Equal("enc:v1:hello", AnswerProtection.ReadAll(typed, Fields, NewProtector(), "org1")["name"]);
    }
}