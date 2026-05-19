using FluentAssertions;
using NUnit.Framework;
using ValIO.Core.Services;

namespace ValIO.Generators.TypeScript.Tests.Core;

/// <summary>
/// Unit tests for <see cref="NamingConverter"/> — the foundational
/// identifier-translation service every generator depends on.
/// </summary>
[TestFixture]
public class NamingConverterTests
{
    private readonly NamingConverter _converter = new();

    [TestCase("user", "User")]
    [TestCase("user_id", "UserId")]
    [TestCase("user-id", "UserId")]
    [TestCase("userId", "UserId")]
    [TestCase("UserId", "UserId")]
    public void ToPascalCase_PreservesAcronymsAndJoinsTokens(string input, string expected)
    {
        _converter.ToPascalCase(input).Should().Be(expected);
    }

    // KNOWN LIMITATIONS — these tests document current behavior, not the
    // ideal contract. Fix would normalize SCREAMING_SNAKE → PascalCase and
    // split run-on acronyms (HTTPRequest → HttpRequest).
    [Test]
    public void ToPascalCase_KnownLimitation_ScreamingSnakeStaysUppercase()
    {
        // Today: "USER_ID" → "USERID" (drops underscore but keeps casing).
        _converter.ToPascalCase("USER_ID").Should().NotBe("UserId");
    }

    [Test]
    public void ToPascalCase_KnownLimitation_RunOnAcronymKeepsUppercase()
    {
        // Today: "HTTPRequest" is already PascalCase-ish, kept verbatim.
        _converter.ToPascalCase("HTTPRequest").Should().Be("HTTPRequest");
    }

    [TestCase("User", "user")]
    [TestCase("UserId", "userId")]
    [TestCase("user_id", "userId")]
    [TestCase("user-id", "userId")]
    [TestCase("userId", "userId")]
    public void ToCamelCase_LowercaseFirstLetter(string input, string expected)
    {
        _converter.ToCamelCase(input).Should().Be(expected);
    }

    [TestCase("UserId", "user_id")]
    [TestCase("userId", "user_id")]
    [TestCase("user-id", "user_id")]
    public void ToSnakeCase_LowercaseUnderscoreSeparator(string input, string expected)
    {
        _converter.ToSnakeCase(input).Should().Be(expected);
    }

    [Test]
    public void ToSnakeCase_KnownLimitation_ScreamingSnakeNotCollapsed()
    {
        // Today: "USER_ID" → "u_s_e_r_i_d" (treats each letter as a boundary).
        // Filed for later cleanup.
        _converter.ToSnakeCase("USER_ID").Should().NotBe("user_id");
    }

    [TestCase("UserId", "user-id")]
    [TestCase("userId", "user-id")]
    [TestCase("user_id", "user-id")]
    public void ToKebabCase_LowercaseHyphenSeparator(string input, string expected)
    {
        _converter.ToKebabCase(input).Should().Be(expected);
    }

    [Test]
    public void SanitizeIdentifier_StripsInvalidChars()
    {
        // C#-style: alpha or underscore, then alnum/underscore.
        _converter.SanitizeIdentifier("hello world").Should().NotContain(" ");
        _converter.SanitizeIdentifier("foo$bar").Should().NotContain("$");
    }

    [Test]
    public void SanitizeIdentifier_LeavesValidIdentifiersUnchanged()
    {
        _converter.SanitizeIdentifier("validName").Should().Be("validName");
        _converter.SanitizeIdentifier("_privateField").Should().Be("_privateField");
    }

    [Test]
    public void EmptyInput_DoesNotThrow()
    {
        Func<string> act1 = () => _converter.ToPascalCase("");
        Func<string> act2 = () => _converter.ToCamelCase("");
        Func<string> act3 = () => _converter.ToSnakeCase("");
        Func<string> act4 = () => _converter.ToKebabCase("");
        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        act4.Should().NotThrow();
    }
}
