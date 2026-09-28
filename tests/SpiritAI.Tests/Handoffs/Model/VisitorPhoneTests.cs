using SpiritAI.Handoffs.Model;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Model;

/// <summary>
/// Reading a typed phone number. The valid numbers are libphonenumber's own example numbers for
/// US and GB (<c>PhoneNumberUtil.GetExampleNumber</c>).
/// </summary>
public sealed class VisitorPhoneTests
{
    [Theory]
    [InlineData("(201) 555-0123", "+12015550123")]
    [InlineData("201.555.0123", "+12015550123")]
    [InlineData("+1 201-555-0123", "+12015550123")]
    [InlineData("+44 121 234 5678", "+441212345678")]
    public void AValidNumberIsReadAsE164(string typed, string e164)
    {
        Assert.True(VisitorPhone.TryRead(typed, out var read));
        Assert.Equal(e164, read);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("call me")]
    public void SomethingElseIsRefused(string? typed)
    {
        Assert.False(VisitorPhone.TryRead(typed, out var read));
        Assert.Null(read);
    }

    [Theory]
    [InlineData("+12015550123", "+1 201-555-0123")]
    [InlineData("+441212345678", "+44 121 234 5678")]
    public void AStoredNumberIsShownInInternationalForm(string e164, string shown)
        => Assert.Equal(shown, VisitorPhone.Display(e164));
}
