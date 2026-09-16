using SpiritAI.RealTime;

namespace SpiritAI.Tests.RealTime;

/// <summary>
/// An <see cref="IRealTimeAdmission"/> that reads <c>?as=</c>: <c>alpha</c> joins <c>room:a</c> and
/// may signal <c>room:b</c>; <c>beta</c> joins <c>room:b</c> and may signal nothing; anything else
/// is nobody this feature knows.
/// </summary>
internal sealed class FakeAdmission : IRealTimeAdmission
{
    public const string AlphaKey = "user:alpha";

    public const string BetaKey = "visitor:beta";

    public const string RoomA = "room:a";

    public const string RoomB = "room:b";

    private int _asked;

    /// <summary>How many sockets the hub asked about. A bad token must never reach here.</summary>
    public int Asked => Volatile.Read(ref _asked);

    public ValueTask<RealTimeCaller?> AdmitAsync(RealTimeRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _asked);

        RealTimeCaller? caller = request.Query["as"].ToString() switch
        {
            "alpha" => new RealTimeCaller(AlphaKey, "Alpha", "alpha", [RoomA], group => group == RoomB),
            "beta" => new RealTimeCaller(BetaKey, null, "beta", [RoomB], _ => false),
            _ => null,
        };

        return ValueTask.FromResult(caller);
    }
}
