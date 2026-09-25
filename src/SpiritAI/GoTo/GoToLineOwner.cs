namespace SpiritAI.GoTo;

/// <summary>One phone line and the GoTo user it belongs to.</summary>
/// <param name="LineId">The line's id, the <c>lineId</c> of a call event.</param>
/// <param name="Extension">The extension, such as <c>8625</c>.</param>
/// <param name="UserKey">The owner's user key.</param>
public sealed record GoToLineOwner(string LineId, string Extension, string UserKey);
