using AgentCore.Application.Ports;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpiritAI.Database;

namespace SpiritAI.PublicChat;

/// <summary>
/// Deletes the AI's copy of a widget chat once it has not changed for
/// <see cref="PublicChatOptions.TranscriptDays"/>. Chatwoot keeps the chat itself, and the copy is
/// made again from Chatwoot if the visitor comes back. A widget copy is a conversation with no
/// principal: signed-in threads always have one.
/// </summary>
public sealed class WidgetTranscriptSweeper(
    IServiceScopeFactory scopes,
    IOptions<PublicChatOptions> options,
    TimeProvider clock,
    ILogger<WidgetTranscriptSweeper> logger) : BackgroundService
{
    /// <summary>The most copies one sweep deletes.</summary>
    public const int BatchSize = 500;

    /// <summary>How often the sweep runs.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>Deletes up to <see cref="BatchSize"/> stale widget copies, oldest first.</summary>
    /// <param name="cancellationToken">Stops the sweep between deletes.</param>
    /// <returns>How many copies were deleted.</returns>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SpiritDbContext>();
        var conversations = scope.ServiceProvider.GetRequiredService<IConversations>();

        var before = clock.GetUtcNow() - TimeSpan.FromDays(options.Value.TranscriptDays);

        // Reads AgentCore's own tables: IConversations lists by principal only, and these have none.
        var stale = await database.Database
            .SqlQuery<string>(
                $"""
                SELECT c.conversation_id AS "Value"
                FROM agentcore.conversation c
                WHERE c.updated_at < {before}
                  AND NOT EXISTS (SELECT 1 FROM agentcore.conversation_principal p WHERE p.conversation_id = c.conversation_id)
                ORDER BY c.updated_at
                LIMIT {BatchSize}
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var conversationId in stale)
        {
            await conversations.DeleteAsync(conversationId, cancellationToken).ConfigureAwait(false);
        }

        return stale.Count;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);

        try
        {
            do
            {
                await SweepAndLogAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    /// <summary>A failed sweep is logged and tried again on the next tick.</summary>
    private async Task SweepAndLogAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await SweepOnceAsync(cancellationToken).ConfigureAwait(false) is > 0 and var deleted)
            {
                logger.LogInformation("Swept {Count} stale widget transcript(s).", deleted);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The widget transcript sweep failed; trying again next tick.");
        }
    }
}
