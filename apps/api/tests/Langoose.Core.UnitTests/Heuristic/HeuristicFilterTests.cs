using FluentAssertions;
using Langoose.Core.Configuration;
using Langoose.Core.Heuristic;
using Microsoft.Extensions.Options;
using Xunit;

namespace Langoose.Core.UnitTests.Heuristic;

public sealed class HeuristicFilterTests
{
    // Mirrors appsettings.json defaults. Settings models no longer carry
    // their own defaults, so each construction site is explicit; this
    // helper keeps test-side overrides tight without leaking the rule.
    private static HeuristicFilterSettings Settings(
        int minLength = 2,
        int maxLength = 300,
        string[]? blocklist = null) => new()
    {
        MinLength = minLength,
        MaxLength = maxLength,
        PosBlocklist = blocklist ?? ["name", "abbrev", "symbol", "intj"]
    };

    private static HeuristicFilter Build(HeuristicFilterSettings? settings = null) =>
        new(Options.Create(settings ?? Settings()));

    [Theory]
    [InlineData("book", "noun")]
    [InlineData("look up", "phrase")]
    [InlineData("don't", "verb")]
    [InlineData("co-op", "noun")]
    [InlineData("Café", "noun")]
    public void Evaluate_PlainHeadwords_Accepts(string text, string pos)
    {
        var verdict = Build().Evaluate(text, pos);

        verdict.Accepted.Should().BeTrue();
        verdict.Reason.Should().BeNull();
    }

    [Theory]
    [InlineData("a")]
    public void Evaluate_TooShort_Rejects(string text)
    {
        var verdict = Build().Evaluate(text, "noun");

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Contain("length");
    }

    [Fact]
    public void Evaluate_TooLong_Rejects()
    {
        var verdict = Build(Settings(maxLength: 5)).Evaluate("longerthan5", "noun");

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Contain("length");
    }

    [Theory]
    [InlineData("book2")]
    [InlineData("3rd")]
    [InlineData("hello!")]
    [InlineData("a/b")]
    [InlineData("foo_bar")]
    public void Evaluate_DisallowedCharacters_Rejects(string text)
    {
        var verdict = Build().Evaluate(text, "noun");

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Contain("characters");
    }

    [Theory]
    [InlineData("London", "name")]
    [InlineData("etc", "abbrev")]
    [InlineData("hi", "intj")]
    public void Evaluate_BlocklistedPos_Rejects(string text, string pos)
    {
        var verdict = Build().Evaluate(text, pos);

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Contain("blocklist");
    }

    [Fact]
    public void Evaluate_BlocklistIsCaseInsensitive_Rejects()
    {
        var verdict = Build(Settings(blocklist: ["Name"])).Evaluate("Paris", "name");

        verdict.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_LengthFailsBeforeCharacterCheck()
    {
        var verdict = Build(Settings(minLength: 10, maxLength: 20)).Evaluate("a/b", "noun");

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Contain("length");
    }
}
