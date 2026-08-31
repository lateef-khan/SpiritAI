using SpiritAI.Knowledge;

using Xunit;

namespace SpiritAI.Tests.Knowledge;

/// <summary>
/// The rule that decides which words a knowledge answer MUST contain.
/// </summary>
/// <remarks>
/// These moved here with the analyzer itself when AgentCore stopped shipping it. They matter more
/// here than they did there: a term wrongly made mandatory drops a good card silently, so the
/// boundary cases below (<c>ct9000x</c>, a bare number) are the whole point of the file.
/// </remarks>
public sealed class IdentifierCodeAnalyzerTests
{
    private static readonly IdentifierCodeAnalyzer Analyzer = new();

    [Fact]
    public void Name_IsTheConfigurationValue() => Assert.Equal("identifier-codes", Analyzer.Name);

    [Theory]
    [InlineData("the screen says e33", "e33")]
    [InlineData("THE SCREEN SAYS E33", "e33")]
    [InlineData("error ol1 on startup", "ol1")]
    [InlineData("code ce10 keeps coming back", "ce10")]
    // A product line is the same shape as a fault code, and is required the same way.
    [InlineData("ct900 belt slipping", "ct900")]
    public void RequiredTerms_FindsTheIdentifier(string query, string expected)
        => Assert.Equal([expected], Analyzer.RequiredTerms(query));

    [Fact]
    public void RequiredTerms_TwoIdentifiers_ReturnsBothInOrder()
        => Assert.Equal(["e33", "e27"], Analyzer.RequiredTerms("the screen says e33 e27"));

    [Fact]
    public void RequiredTerms_RepeatedIdentifier_IsReturnedOnce()
        => Assert.Equal(["e33"], Analyzer.RequiredTerms("e33 again, still e33"));

    [Theory]
    [InlineData("how do i clean the deck")]
    [InlineData("")]
    [InlineData("treadmill")]
    // Five letters, then digits: too long to be a console code.
    [InlineData("model ct9000x")]
    // Digits with no leading letters.
    [InlineData("part 90210")]
    // A Spirit serial number is 16 digits. Requiring it verbatim would drop every card that talks
    // about the unit in words, which is all of them.
    [InlineData("serial 1234567890123456")]
    // A model number is the first 6 digits of the serial, and carries no letters either.
    [InlineData("model 900112")]
    public void RequiredTerms_NoIdentifier_IsEmpty(string query)
        => Assert.Empty(Analyzer.RequiredTerms(query));

    [Fact]
    public void RequiredTerms_Null_Throws()
        => Assert.Throws<ArgumentNullException>(() => Analyzer.RequiredTerms(null!));
}
