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

    // Fixed: SCREAMING_SNAKE normalizes to PascalCase / snake_case correctly.
    [Test]
    public void ToPascalCase_ScreamingSnake_NormalisesToPascal()
    {
        _converter.ToPascalCase("USER_ID").Should().Be("UserId");
    }

    [Test]
    public void ToPascalCase_RunOnAcronym_SplitsAndTitleCases()
    {
        // "HTTPRequest" → ["HTTP", "Request"] → "HttpRequest"
        _converter.ToPascalCase("HTTPRequest").Should().Be("HttpRequest");
    }

    [Test]
    public void ToPascalCase_MixedAcronymThenWord_SplitsCorrectly()
    {
        _converter.ToPascalCase("XMLHttpRequest").Should().Be("XmlHttpRequest");
    }

    [Test]
    public void ToPascalCase_TrailingNumber_KeepsNumberAsSeparateWord()
    {
        _converter.ToPascalCase("api_v2").Should().Be("ApiV2");
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
    public void ToSnakeCase_ScreamingSnake_NormalisesToLowerSnake()
    {
        _converter.ToSnakeCase("USER_ID").Should().Be("user_id");
    }

    [Test]
    public void ToSnakeCase_AllCapsAcronymInsideIdentifier_SplitsCorrectly()
    {
        _converter.ToSnakeCase("getHTTPClient").Should().Be("get_http_client");
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
