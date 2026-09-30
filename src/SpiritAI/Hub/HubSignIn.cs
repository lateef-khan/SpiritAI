namespace SpiritAI.Hub;

/// <summary>A one-time link into another app, made for the caller alone.</summary>
/// <param name="Url">Where the browser is sent. Desk's and CRM's own sign-in pages consume it once.</param>
public sealed record HubSignIn(string Url);
