namespace SpiritAI.Hub;

/// <summary>A create refused because the app already has a user with that email (hub spec, section 5.1).</summary>
public sealed class EmailAlreadyUsedException(string appName) : Exception($"email already used in {appName}");
