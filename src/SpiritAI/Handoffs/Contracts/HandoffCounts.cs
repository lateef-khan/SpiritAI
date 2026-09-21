namespace SpiritAI.Handoffs.Contracts;

/// <summary>
/// How many rows of one view there are, split by who holds them. The sidebar shows the open
/// view's <see cref="AwaitingReply"/> and <see cref="Unassigned"/>; the tab strip shows the
/// first three.
/// </summary>
/// <param name="Mine">The rows the caller holds.</param>
/// <param name="Unassigned">The rows nobody has taken.</param>
/// <param name="All">Every row of the view.</param>
/// <param name="AwaitingReply">Of <paramref name="Mine"/>, the chats whose visitor is waiting on the caller. See <c>ReplyDue</c>.</param>
public sealed record HandoffCounts(int Mine, int Unassigned, int All, int AwaitingReply);
