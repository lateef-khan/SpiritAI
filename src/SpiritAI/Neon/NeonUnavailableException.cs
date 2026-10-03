namespace SpiritAI.Neon;

/// <summary>A Neon call failed, or the API key, project or branch is not set (<see cref="NotSetUp"/>).</summary>
public sealed class NeonUnavailableException : Exception
{
    public NeonUnavailableException(Exception? inner = null)
        : base("Neon did not answer.", inner)
    {
    }

    private NeonUnavailableException(string message)
        : base(message)
    {
    }

    public static NeonUnavailableException NotSetUp() => new("Neon is not set up yet.");
}
