using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpiritAI.Database;
using SpiritAI.RealTime;
using SpiritAI.RealTime.Presence;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.RealTime.Presence;

/// <summary>
/// The presence rows of section 6.5 of the handoff spec, against real PostgreSQL.
/// </summary>
/// <remarks>
/// The clock starts in the year 2000, so a sweep at that time can only ever delete rows made by
/// this class, and a count only ever sees rows made by this class inside its window.
/// </remarks>
public sealed class PresenceStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Start = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(90);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConnectTouchAndDisconnect()
    {
        var connectionId = NewConnectionId();
        await using var database = fixture.Open();
        var clock = new TestTimeProvider(Start);
        var store = Store(database, clock);

        try
        {
            await store.ConnectAsync(connectionId, "user:dana", "Dana R.", "staff", Cancel);

            var row = await database.Presence.AsNoTracking().SingleAsync(p => p.ConnectionId == connectionId, Cancel);
            Assert.Equal("user:dana", row.CallerKey);
            Assert.Equal("Dana R.", row.CallerName);
            Assert.Equal("staff", row.Kind);
            Assert.Equal(Start, row.ConnectedAt);
            Assert.Equal(Start, row.SeenAt);

            clock.Now += TimeSpan.FromSeconds(30);
            Assert.True(await store.TouchAsync(connectionId, Cancel));

            row = await database.Presence.AsNoTracking().SingleAsync(p => p.ConnectionId == connectionId, Cancel);
            Assert.Equal(Start, row.ConnectedAt);
            Assert.Equal(Start.AddSeconds(30), row.SeenAt);

            await store.DisconnectAsync(connectionId, Cancel);

            Assert.False(await database.Presence.AnyAsync(p => p.ConnectionId == connectionId, Cancel));
            Assert.False(await store.TouchAsync(connectionId, Cancel));
        }
        finally
        {
            await Delete(database, connectionId);
        }
    }

    [Fact]
    public async Task CountIsDistinctByKeyAndKindInsideTheWindow()
    {
        var (stale, tabOne, tabTwo, visitor) = (NewConnectionId(), NewConnectionId(), NewConnectionId(), NewConnectionId());
        await using var database = fixture.Open();
        var clock = new TestTimeProvider(Start);
        var store = Store(database, clock);

        try
        {
            await store.ConnectAsync(stale, "user:sam", "Sam", "staff", Cancel);
            clock.Now += Window + TimeSpan.FromSeconds(1);
            await store.ConnectAsync(tabOne, "user:dana", "Dana R.", "staff", Cancel);
            await store.ConnectAsync(tabTwo, "user:dana", "Dana R.", "staff", Cancel);
            await store.ConnectAsync(visitor, "visitor:v1", null, "visitor", Cancel);

            Assert.Equal(1, await store.CountOnlineAsync("staff", Cancel));
            Assert.Equal(1, await store.CountOnlineAsync("visitor", Cancel));

            Assert.True(await store.TouchAsync(stale, Cancel));

            Assert.Equal(2, await store.CountOnlineAsync("staff", Cancel));
        }
        finally
        {
            await Delete(database, stale, tabOne, tabTwo, visitor);
        }
    }

    [Fact]
    public async Task IsOnlineHoldsForTheWindowAfterTheLastTouch()
    {
        var connectionId = NewConnectionId();
        await using var database = fixture.Open();
        var clock = new TestTimeProvider(Start);
        var store = Store(database, clock);

        try
        {
            await store.ConnectAsync(connectionId, "visitor:v1", null, "visitor", Cancel);

            Assert.True(await store.IsOnlineAsync("visitor:v1", Cancel));
            Assert.False(await store.IsOnlineAsync("visitor:v2", Cancel));

            clock.Now += Window;
            Assert.True(await store.IsOnlineAsync("visitor:v1", Cancel));

            clock.Now += TimeSpan.FromSeconds(1);
            Assert.False(await store.IsOnlineAsync("visitor:v1", Cancel));
        }
        finally
        {
            await Delete(database, connectionId);
        }
    }

    [Fact]
    public async Task SweepDeletesOnlyStaleRowsAndNamesTheirKinds()
    {
        var (staleStaff, staleVisitor, fresh) = (NewConnectionId(), NewConnectionId(), NewConnectionId());
        await using var database = fixture.Open();
        var clock = new TestTimeProvider(Start);
        var store = Store(database, clock);

        try
        {
            await store.ConnectAsync(staleStaff, "user:sam", "Sam", "staff", Cancel);
            await store.ConnectAsync(staleVisitor, "visitor:v1", null, "visitor", Cancel);
            clock.Now += Window + TimeSpan.FromSeconds(1);
            await store.ConnectAsync(fresh, "user:dana", "Dana R.", "staff", Cancel);

            Assert.Equal(["staff", "visitor"], (await store.SweepAsync(Cancel)).Order());

            Assert.False(await database.Presence.AnyAsync(p => p.ConnectionId == staleStaff, Cancel));
            Assert.False(await database.Presence.AnyAsync(p => p.ConnectionId == staleVisitor, Cancel));
            Assert.True(await database.Presence.AnyAsync(p => p.ConnectionId == fresh, Cancel));
            Assert.Empty(await store.SweepAsync(Cancel));
        }
        finally
        {
            await Delete(database, staleStaff, staleVisitor, fresh);
        }
    }

    private static PresenceStore Store(SpiritDbContext database, TimeProvider clock)
        => new(database, clock, Options.Create(new RealTimeOptions { PresenceWindowSeconds = (int)Window.TotalSeconds }));

    private static string NewConnectionId() => "test-" + Guid.NewGuid().ToString("N");

    private static Task Delete(SpiritDbContext database, params string[] connectionIds)
        => database.Presence.Where(p => connectionIds.Contains(p.ConnectionId)).ExecuteDeleteAsync(Cancel);
}
