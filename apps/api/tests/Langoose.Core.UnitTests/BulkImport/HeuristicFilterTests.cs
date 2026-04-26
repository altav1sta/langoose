using FluentAssertions;
using Langoose.Core.Configuration;
using Langoose.Core.BulkImport;
using Xunit;

namespace Langoose.Core.UnitTests.BulkImport;

public sealed class HeuristicFilterTests
{
    private static HeuristicFilter Build(HeuristicFilterSettings? settings = null) =>
        new(settings ?? new HeuristicFilterSettings());

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
        var settings = new HeuristicFilterSettings { MinLength = 2, MaxLength = 5 };

        var verdict = Build(settings).Evaluate("longerthan5", "noun");

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
        var settings = new HeuristicFilterSettings { PosBlocklist = ["Name"] };

        var verdict = Build(settings).Evaluate("Paris", "name");

        verdict.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_LengthFailsBeforeCharacterCheck()
    {
        var settings = new HeuristicFilterSettings { MinLength = 10, MaxLength = 20 };

        var verdict = Build(settings).Evaluate("a/b", "noun");

        verdict.Accepted.Should().BeFalse();
        verdict.Reason.Should().Contain("length");
    }
}
