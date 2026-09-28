using SpiritAI.Handoffs.Bot;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Bot;

/// <summary>The check of what a person typed for a call back (spec 9.2.1).</summary>
public sealed class CheckContactToolTests
{
    [Fact]
    public void AGoodPhoneAndEmailComeBackInTheirStoredForm()
    {
        var answer = CheckContactTool.Check("(201) 555-0123", "  Dana@Example.COM ");

        Assert.Equal("+12015550123", answer.Phone);
        Assert.Equal("dana@example.com", answer.Email);
        Assert.Empty(answer.Invalid);
    }

    [Fact]
    public void NoEmailIsNotAnError()
    {
        var answer = CheckContactTool.Check("(201) 555-0123", email: null);

        Assert.Null(answer.Email);
        Assert.Empty(answer.Invalid);
    }

    [Theory]
    [InlineData("12", "dana@example.com", "phone")]
    [InlineData("(201) 555-0123", "dana@localhost", "email")]
    [InlineData("(201) 555-0123", "Dana <dana@example.com>", "email")]
    [InlineData("(201) 555-0123", "not an email", "email")]
    public void AFieldThatIsNotValidIsNamed(string phone, string email, string invalid)
    {
        Assert.Equal([invalid], CheckContactTool.Check(phone, email).Invalid);
    }
}
