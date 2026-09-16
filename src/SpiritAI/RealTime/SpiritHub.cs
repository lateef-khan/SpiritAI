using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;

using SpiritAI.Auth;
using SpiritAI.RealTime.Presence;

namespace SpiritAI.RealTime;

/// <summary>
/// The one socket the host has. A caller is admitted by the first <see cref="IRealTimeAdmission"/>
/// that knows them, joins the groups it named, and is counted in presence under its kind. Anyone
/// nobody admits is dropped.
/// </summary>
/// <remarks>
/// The hub changes no state. Every state change is REST, and the pushes that follow one go out
/// through <see cref="IRealTimePublisher"/>; the hub only relays: a heartbeat to say the socket is
/// still here, and a signal from one socket to a group it is allowed to address. The socket is a
/// hint; REST is the truth.
/// </remarks>
public sealed class SpiritHub(
    IEnumerable<IRealTimeAdmission> admissions,
    IPresenceStore presence,
    ILogger<SpiritHub> logger) : Hub
{
    /// <summary>Where the hub is mapped.</summary>
    public const string Pattern = "/v1/realtime/hub";

    private const string CallerItem = "caller";

    private RealTimeCaller? Caller
        => Context.Items.TryGetValue(CallerItem, out var item) ? item as RealTimeCaller : null;

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var cancellationToken = Context.ConnectionAborted;
        var caller = await AdmitAsync(cancellationToken).ConfigureAwait(false);

        if (caller is null)
        {
            Context.Abort();
            return;
        }

        Context.Items[CallerItem] = caller;

        foreach (var group in caller.Groups)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group, cancellationToken).ConfigureAwait(false);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealTimeGroups.Presence, cancellationToken).ConfigureAwait(false);
        await presence.ConnectAsync(Context.ConnectionId, caller.Key, caller.Name, caller.Kind, cancellationToken).ConfigureAwait(false);
        await PublishPresenceAsync(caller.Kind, cancellationToken).ConfigureAwait(false);

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Caller is { } caller)
        {
            await presence.DisconnectAsync(Context.ConnectionId, CancellationToken.None).ConfigureAwait(false);
            await PublishPresenceAsync(caller.Kind, CancellationToken.None).ConfigureAwait(false);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Says the socket is still here. The client calls it every
    /// <see cref="RealTimeOptions.HeartbeatSeconds"/>; the server keeps no timer of its own.
    /// </summary>
    public Task Heartbeat()
        => Caller is null
            ? Task.CompletedTask
            : presence.TouchAsync(Context.ConnectionId, Context.ConnectionAborted);

    /// <summary>
    /// Says something to a group, when the admission allowed this caller to address it. The hub
    /// stamps the sender on; the payload passes through unread.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="name">What kind of signal.</param>
    /// <param name="payload">Whatever the sender attached.</param>
    public Task Signal(string group, string name, JsonElement payload)
    {
        if (Caller is not { } caller || string.IsNullOrEmpty(group) || string.IsNullOrEmpty(name))
        {
            return Task.CompletedTask;
        }

        if (!caller.MaySignal(group))
        {
            logger.LogDebug("Dropped a signal from {Kind} {Key} to {Group}: not allowed.", caller.Kind, caller.Key, group);
            return Task.CompletedTask;
        }

        var signal = new RealTimeSignal(new RealTimeSender(caller.Key, caller.Kind), group, name, payload);

        return Clients.Group(group).SendAsync(RealTimeEvents.Signal, signal, Context.ConnectionAborted);
    }

    /// <summary>
    /// Works out who is on the socket. The hub path is open to the API gate, so nothing has looked
    /// at the token yet: the hub asks the scheme itself, and only then the admissions.
    /// </summary>
    /// <returns>The caller, or <see langword="null"/> for one to drop.</returns>
    private async Task<RealTimeCaller?> AdmitAsync(CancellationToken cancellationToken)
    {
        var http = Context.GetHttpContext();

        if (http is null)
        {
            return null;
        }

        var auth = await http.AuthenticateAsync(NeonAuthenticationDefaults.Scheme).ConfigureAwait(false);

        if (!auth.Succeeded && !auth.None)
        {
            logger.LogInformation("Refused a socket: the token was not accepted.");
            return null;
        }

        var request = new RealTimeRequest(auth.Succeeded ? auth.Principal : null, http.Request.Query);

        foreach (var admission in admissions)
        {
            var caller = await admission.AdmitAsync(request, cancellationToken).ConfigureAwait(false);

            if (caller is not null)
            {
                return caller;
            }
        }

        logger.LogInformation("Refused a socket: no admission knew the caller.");
        return null;
    }

    /// <summary>Tells every admitted socket how many callers of one kind are online now.</summary>
    private async Task PublishPresenceAsync(string kind, CancellationToken cancellationToken)
    {
        var online = await presence.CountOnlineAsync(kind, cancellationToken).ConfigureAwait(false);

        await Clients.Group(RealTimeGroups.Presence)
            .SendAsync(RealTimeEvents.Presence, new RealTimePresence(kind, online), cancellationToken)
            .ConfigureAwait(false);
    }
}
