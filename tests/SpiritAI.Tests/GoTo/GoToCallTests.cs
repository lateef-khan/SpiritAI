using System.Text.Json;

using SpiritAI.GoTo;
using SpiritAI.Handoffs.Callback;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>
/// Call events as GoTo posted them on 2026-09-25, with the numbers and names swapped for test ones.
/// </summary>
public sealed class GoToCallTests
{
    [Fact]
    public void AnInboundRingReadsTheCallerAndTheRingingLine()
    {
        var call = Read("call_ringing");

        Assert.Equal(
            new GoToCall("8a484291-cc5e-31f1-afd2-c9f0eea271d2", "ACTIVE", Outbound: false, "+12015550123", call.Lines),
            call);
        Assert.Equal([new GoToCallLine("d3d10a08-2269-4872-a904-261c68494270", "8625", "RINGING")], call.Lines);
        Assert.Equal(call.Lines, CallRingAlert.LinesToTell(call));
    }

    [Fact]
    public void TheFirstEventOfACallHasNoLineYet()
    {
        var call = Read("call_starting");

        Assert.Equal("STARTING", call.State);
        Assert.Equal("+12015550123", call.OutsideNumber);
        Assert.Empty(call.Lines);
    }

    [Fact]
    public void ATransferTellsOnlyTheLineThatRings()
    {
        var call = Read("call_transfer_ringing");

        Assert.Equal(2, call.Lines.Count);
        Assert.Equal(["8645"], CallRingAlert.LinesToTell(call).Select(l => l.Extension));
    }

    [Fact]
    public void AnOutboundCallReadsTheCalleeAndTellsTheCallersLine()
    {
        var call = Read("call_outbound");

        Assert.True(call.Outbound);
        Assert.Equal("+12015550102", call.OutsideNumber);
        Assert.Equal([new GoToCallLine("ffc88d70-ab57-4b7a-84e4-00242d1334ec", "8654", "CONNECTED")], CallRingAlert.LinesToTell(call));
    }

    [Fact]
    public void AnEventWithoutACallIsNotRead()
        => Assert.Null(GoToCall.Read(JsonDocument.Parse("""{"type":"call-state"}""").RootElement));

    internal static GoToCall Read(string payload)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GoTo", "Payloads", payload + ".json"));

        return GoToCall.Read(JsonDocument.Parse(text).RootElement)!;
    }
}
