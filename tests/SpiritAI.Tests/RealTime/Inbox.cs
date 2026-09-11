using System.Threading.Channels;

using Microsoft.AspNetCore.SignalR.Client;

using Xunit;

namespace SpiritAI.Tests.RealTime;

/// <summary>
/// Everything one socket received under one event name, in order, typed the way the browser
/// reads it.
/// </summary>
internal sealed class Inbox<T>
{
    /// <summary>How long a test waits for a push before giving up on it.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>How long a test waits to be sure a push is not coming.</summary>
    public static readonly TimeSpan Silence = TimeSpan.FromMilliseconds(500);

    private readonly Channel<T> _received = Channel.CreateUnbounded<T>();

    public Inbox(HubConnection connection, string eventName)
    {
        connection.On<T>(eventName, item => _received.Writer.TryWrite(item));
    }

    /// <summary>The next push, or a timeout after <see cref="Patience"/>.</summary>
    public Task<T> NextAsync()
        => _received.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(Patience);

    /// <summary>Waits <see cref="Silence"/> and says whether nothing came in that time.</summary>
    public async Task<bool> StaysEmptyAsync()
    {
        try
        {
            await _received.Reader.WaitToReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(Silence);
            return false;
        }
        catch (TimeoutException)
        {
            return true;
        }
    }
}
