using AgentCore.Application.State;

using SpiritAI.Knowledge;

using Xunit;

namespace SpiritAI.Tests.Knowledge;

/// <summary>
/// The rule that decides which words a knowledge answer MUST contain.
/// </summary>
/// <remarks>
/// <para>
/// These moved here with the analyzer itself when AgentCore stopped shipping it. They matter more
/// here than they did there: a term wrongly made mandatory drops a good card silently, so the
/// boundary cases below are the whole point of the file.
/// </para>
/// <para>
/// A term this returns becomes mandatory inside the analyzer's own prefetch leg, and that leg is
/// fused with an unfiltered one — so a term lifts the cards that carry it and never drops the cards
/// that do not. Getting the set wrong in the other direction is what costs answers: making an
/// ordinary English word mandatory would wreck every search, which is why a letter-only token is
/// only ever accepted from the knowledge base's own product list.
/// </para>
/// </remarks>
public sealed class IdentifierCodeAnalyzerTests
{
    private static readonly IReadOnlySet<string> Products =
        new HashSet<string>(StringComparer.Ordinal) { "lcr", "lcb", "srvo", "crw800h2o", "f63", "ct900" };

    /// <summary>The analyzer as it behaves before any vocabulary refresh has run.</summary>
    private static readonly IdentifierCodeAnalyzer Shapes = new();

    [Fact]
    public void Name_IsTheConfigurationValue() => Assert.Equal("identifier-codes", Shapes.Name);

    [Theory]
    [InlineData("the screen says e33", "e33")]
    [InlineData("THE SCREEN SAYS E33", "e33")]
    [InlineData("error ol1 on startup", "ol1")]
    [InlineData("code ce10 keeps coming back", "ce10")]
    // A product line is the same shape as a fault code, and is required the same way.
    [InlineData("ct900 belt slipping", "ct900")]
    public void RequiredTerms_FindsTheIdentifier(string query, string expected)
        => Assert.Equal([expected], Shapes.RequiredTerms(query));

    [Fact]
    public void RequiredTerms_TwoIdentifiers_ReturnsBothInOrder()
        => Assert.Equal(["e33", "e27"], Shapes.RequiredTerms("the screen says e33 e27"));

    [Fact]
    public void RequiredTerms_RepeatedIdentifier_IsReturnedOnce()
        => Assert.Equal(["e33"], Shapes.RequiredTerms("e33 again, still e33"));

    [Theory]
    [InlineData("how do i clean the deck")]
    [InlineData("")]
    [InlineData("treadmill")]
    // Digits with no letters anywhere.
    [InlineData("part 90210")]
    // A Spirit serial number is 16 digits. Requiring it verbatim would drop every card that talks
    // about the unit in words, which is all of them.
    [InlineData("serial 1234567890123456")]
    // A model number is the first 6 digits of the serial, and carries no letters either.
    [InlineData("model 900112")]
    public void RequiredTerms_NoIdentifier_IsEmpty(string query)
        => Assert.Empty(Shapes.RequiredTerms(query));

    [Fact]
    public void RequiredTerms_LettersAroundDigits_IsAnIdentifier()
    {
        // Real products are shaped this way — ctsbs900, xt485ent, e95s — so the shape must be
        // accepted, and a token that merely resembles one is accepted with it. That costs nothing:
        // a mandatory term only lifts cards inside the prefetch leg, and never drops any.
        Assert.Equal(["ct9000x"], Shapes.RequiredTerms("model ct9000x"));
    }

    [Fact]
    public void RequiredTerms_Null_Throws()
        => Assert.Throws<ArgumentNullException>(() => Shapes.RequiredTerms(null!));

    [Fact]
    public void FindsALetterOnlyProductName()
        => Assert.Equal(["lcr"], Analyzer().RequiredTerms("my 2023 LCR is not picking up my HR signal"));

    [Theory]
    [InlineData("f63")]
    [InlineData("ct900")]
    [InlineData("e33")]
    [InlineData("ol1")]
    public void StillFindsLettersThenDigits(string token)
        => Assert.Equal([token], Analyzer().RequiredTerms($"what about the {token}"));

    [Theory]
    [InlineData("40t")]
    [InlineData("70t")]
    [InlineData("80t")]
    public void FindsDigitsThenLetters(string token)
        => Assert.Equal([token], Analyzer().RequiredTerms($"what about the {token}"));

    [Theory]
    [InlineData("e95s")]
    [InlineData("sb1200")]
    [InlineData("ct900ent")]
    [InlineData("ctsbs900")]
    [InlineData("xt485ent")]
    public void FindsLettersAroundDigits(string token)
        => Assert.Equal([token], Analyzer().RequiredTerms($"what about the {token}"));

    [Theory]
    [InlineData("belt")]
    [InlineData("motor")]
    [InlineData("noise")]
    [InlineData("the")]
    [InlineData("bike")]
    [InlineData("console")]
    public void NeverMakesAnOrdinaryWordMandatory(string word)
        => Assert.Empty(Analyzer().RequiredTerms($"there is a problem with the {word}"));

    [Fact]
    public void FindsNoLetterOnlyNameWhenTheProductListIsEmpty()
    {
        // The vocabulary refresh has not run, or it failed. Behaviour falls back to shape alone,
        // which is what shipped before this list existed.
        var analyzer = new IdentifierCodeAnalyzer(() => new HashSet<string>(StringComparer.Ordinal));

        Assert.Empty(analyzer.RequiredTerms("my 2023 LCR is not picking up my HR signal"));
    }

    [Fact]
    public void StillFindsAShapeMatchWhenTheProductListIsEmpty()
    {
        var analyzer = new IdentifierCodeAnalyzer(() => new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal(["f63"], analyzer.RequiredTerms("my 2023 F63"));
    }

    [Fact]
    public void FindsNothingForAHyphenatedProductName()
    {
        // A query is split on non-alphanumerics, so xth-rails arrives as "xth" and "rails" and
        // matches neither. One product, and pinned so it is not mistaken for a new bug.
        var analyzer = new IdentifierCodeAnalyzer(
            () => new HashSet<string>(StringComparer.Ordinal) { "xth-rails" });

        Assert.Empty(analyzer.RequiredTerms("a problem with the xth-rails"));
    }

    [Fact]
    public void ReturnsEachTermOnce()
        => Assert.Equal(["lcr"], Analyzer().RequiredTerms("the LCR, and the other lcr"));

    [Theory]
    [InlineData("lcr-2023", "lcr")]
    [InlineData("f63-2019", "f63")]
    [InlineData("tt8-2016-ac", "tt8")]
    [InlineData("ct900", "ct900")]
    [InlineData("xth-rails", "xth-rails")]
    public void DerivesAProductNameFromASlug(string slug, string expected)
        => Assert.Contains(expected, IdentifierCodeAnalyzer.ProductsIn([slug]));

    [Fact]
    public void ReadsTheProductsOutOfAFilledVocabularyCache()
    {
        VocabularyCache cache = new();
        cache.Replace(IdentifierCodeAnalyzer.ModelSlot, ["lcr-2023", "lcr-2026", "f63-2019", "srvo"], 2000);

        Assert.Equal(
            ["f63", "lcr", "srvo"],
            IdentifierCodeAnalyzer.ProductsIn(cache).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ReadsNoProductWhenTheCacheHasNoModelSlot()
    {
        // The slot name here and the one spirit.yaml declares are the same string or this reads
        // empty forever, and nothing anywhere says so.
        VocabularyCache cache = new();
        cache.Replace("product_line", ["treadmill", "bike"], 2000);

        Assert.Empty(IdentifierCodeAnalyzer.ProductsIn(cache));
    }

    [Fact]
    public void ReadsNoProductWhenTheHostRegisteredNoCache()
        => Assert.Empty(IdentifierCodeAnalyzer.ProductsIn((VocabularyCache?)null));

    private static IdentifierCodeAnalyzer Analyzer() => new(() => Products);
}
