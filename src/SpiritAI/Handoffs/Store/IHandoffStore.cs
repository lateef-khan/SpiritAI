using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Store;

/// <summary>
/// Every move a handoff makes, <c>waiting</c> → <c>human</c> → <c>done</c>, over <c>spirit.handoff</c>.
/// </summary>
public interface IHandoffStore
{
    /// <summary>
    /// Asks for a person on a chat. A chat that already has an open handoff gets that one back;
    /// two asks racing on one chat both get the row the first one made.
    /// </summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="askedBy">Which side asked.</param>
    /// <param name="reason">What the person is for, when the asker said.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>The open row.</returns>
    Task<Handoff> AskAsync(
        string conversationId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken);

    /// <summary>The chat's open handoff, waiting or with a person.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or <see langword="null"/> when the bot has the chat.</returns>
    Task<Handoff?> OpenAsync(string conversationId, CancellationToken cancellationToken);

    /// <summary>The chat's open handoff if it has one, else the one closed most recently.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or <see langword="null"/> when nobody was ever asked for.</returns>
    Task<Handoff?> LatestAsync(string conversationId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a waiting chat for one member of staff. Of any number of claims racing on one chat,
    /// exactly one wins.
    /// </summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="staffKey">The claimant's caller key.</param>
    /// <param name="staffName">The name the visitor will see.</param>
    /// <param name="cancellationToken">Cancels the claim.</param>
    /// <returns>How it went, with the row as it stands.</returns>
    Task<HandoffClaim> ClaimAsync(
        string conversationId, string staffKey, string staffName, CancellationToken cancellationToken);

    /// <summary>Closes the chat's open handoff, from waiting or from human. The row stays.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>Whether there was an open handoff to close.</returns>
    Task<bool> DoneAsync(string conversationId, CancellationToken cancellationToken);

    /// <summary>Records where a reply goes when the visitor is not there to read it.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="email">The visitor's address.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the chat had an open handoff to put it on.</returns>
    Task<bool> SetEmailAsync(string conversationId, string email, CancellationToken cancellationToken);
}
