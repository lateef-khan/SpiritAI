namespace SpiritAI.GoTo;

/// <summary>One staff phone line in a call.</summary>
/// <param name="LineId">The line's id, which <see cref="GoToStaffDirectory"/> finds its owner by.</param>
/// <param name="Extension">The extension, such as <c>8625</c>.</param>
/// <param name="Status">Such as <c>RINGING</c>, <c>CONNECTED</c>, or <c>DISCONNECTING</c>.</param>
public sealed record GoToCallLine(string LineId, string Extension, string Status)
{
    /// <summary>Whether the phone on this line is ringing now.</summary>
    public bool Ringing => Status == "RINGING";
}
