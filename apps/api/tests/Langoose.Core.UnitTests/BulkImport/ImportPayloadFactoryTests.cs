using System.Text.Json;
using FluentAssertions;
using Langoose.Core.BulkImport;
using Langoose.Corpus.Data.Models;
using Langoose.Corpus.Data.Readers;
using Xunit;

namespace Langoose.Core.UnitTests.BulkImport;

public sealed class ImportPayloadFactoryTests
{
    private readonly ImportPayloadFactory _factory = new();

    [Fact]
    public void Build_SingleRow_EmitsHeaderAndSenseTranslations()
    {
        var bundle = BundleFromJsonl(
            rank: 5,
            language: "en",
            word: "book",
            pos: "noun",
            "{\"word\":\"book\",\"lang_code\":\"en\",\"pos\":\"noun\",\"senses\":[{\"glosses\":[\"a bound set of pages\"],\"translations\":[{\"code\":\"ru\",\"lang\":\"Russian\",\"word\":\"книга\"}]}]}");

        var json = _factory.Build(bundle);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("entry").GetProperty("text").GetString().Should().Be("book");
        doc.RootElement.GetProperty("entry").GetProperty("language").GetString().Should().Be("en");
        doc.RootElement.GetProperty("entry").GetProperty("pos").GetString().Should().Be("noun");

        var senses = doc.RootElement.GetProperty("senses");
        senses.GetArrayLength().Should().Be(1);
        senses[0].GetProperty("sense_index").GetInt32().Should().Be(0);
        senses[0].GetProperty("gloss").GetString().Should().Be("a bound set of pages");

        var translations = senses[0].GetProperty("translations");
        translations.GetArrayLength().Should().Be(1);
        translations[0].GetProperty("language").GetString().Should().Be("ru");
        translations[0].GetProperty("text").GetString().Should().Be("книга");
    }

    [Fact]
    public void Build_EtymologyBundle_ConcatenatesSensesWithContinuousIndex()
    {
        var bundle = BundleFromJsonl(
            rank: 9,
            language: "en",
            word: "lead",
            pos: "noun",
            "{\"word\":\"lead\",\"lang_code\":\"en\",\"pos\":\"noun\",\"senses\":[{\"glosses\":[\"a heavy metallic element\"],\"translations\":[{\"code\":\"ru\",\"lang\":\"Russian\",\"word\":\"свинец\"}]}]}",
            "{\"word\":\"lead\",\"lang_code\":\"en\",\"pos\":\"noun\",\"senses\":[{\"glosses\":[\"a strap or rope used to restrain an animal\"],\"translations\":[{\"code\":\"ru\",\"lang\":\"Russian\",\"word\":\"поводок\"}]}]}");

        var json = _factory.Build(bundle);
        using var doc = JsonDocument.Parse(json);

        var senses = doc.RootElement.GetProperty("senses");
        senses.GetArrayLength().Should().Be(2);
        senses[0].GetProperty("sense_index").GetInt32().Should().Be(0);
        senses[1].GetProperty("sense_index").GetInt32().Should().Be(1);
        senses[0].GetProperty("gloss").GetString().Should().Contain("metallic");
        senses[1].GetProperty("gloss").GetString().Should().Contain("animal");
    }

    [Fact]
    public void Build_PreservesAllRawSourceRowsInRawArray()
    {
        var rawA = "{\"word\":\"lead\",\"lang_code\":\"en\",\"pos\":\"noun\",\"etymology_number\":1,\"senses\":[]}";
        var rawB = "{\"word\":\"lead\",\"lang_code\":\"en\",\"pos\":\"noun\",\"etymology_number\":2,\"senses\":[]}";

        var bundle = BundleFromJsonl(9, "en", "lead", "noun", rawA, rawB);

        var json = _factory.Build(bundle);
        using var doc = JsonDocument.Parse(json);

        var raw = doc.RootElement.GetProperty("raw");
        raw.GetArrayLength().Should().Be(2);
        raw[0].GetProperty("etymology_number").GetInt32().Should().Be(1);
        raw[1].GetProperty("etymology_number").GetInt32().Should().Be(2);
    }

    [Fact]
    public void Build_DropsTranslationsWithoutLanguageOrText()
    {
        var bundle = BundleFromJsonl(
            rank: 5,
            language: "en",
            word: "book",
            pos: "noun",
            "{\"word\":\"book\",\"lang_code\":\"en\",\"pos\":\"noun\",\"senses\":[{\"glosses\":[\"a bound set of pages\"],\"translations\":[{\"code\":\"ru\",\"word\":\"книга\"},{\"code\":null,\"word\":\"x\"},{\"code\":\"de\",\"word\":\"\"}]}]}");

        var json = _factory.Build(bundle);
        using var doc = JsonDocument.Parse(json);

        var translations = doc.RootElement.GetProperty("senses")[0].GetProperty("translations");
        translations.GetArrayLength().Should().Be(1);
        translations[0].GetProperty("language").GetString().Should().Be("ru");
    }

    private static WiktionaryBundle BundleFromJsonl(
        int rank,
        string language,
        string word,
        string pos,
        params string[] dataJsonRows) =>
        new(
            rank,
            language,
            word,
            pos,
            dataJsonRows
                .Select(json => new WiktionaryEntryRow(language, word, pos, "test-1", json))
                .ToArray());
}
