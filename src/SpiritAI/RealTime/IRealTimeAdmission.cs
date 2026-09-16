namespace SpiritAI.RealTime;

/// <summary>
/// A feature's answer to "who is this socket?". The hub asks every registered admission in
/// registration order and the first to answer wins; when none does, the socket is dropped.
/// </summary>
public interface IRealTimeAdmission
{
    /// <summary>Decides whether this feature knows the caller.</summary>
    /// <param name="request">What the socket said about itself.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>
    /// The caller, or <see langword="null"/> when this feature does not admit them. Null and not a
    /// refusal, because another feature's admission may still know them.
    /// </returns>
    ValueTask<RealTimeCaller?> AdmitAsync(RealTimeRequest request, CancellationToken cancellationToken);
}
