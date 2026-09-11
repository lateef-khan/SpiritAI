using System.Security.Claims;

namespace SpiritAI.RealTime;

/// <summary>
/// What a socket said about itself when it opened, as the hub hands it to each
/// <see cref="IRealTimeAdmission"/>.
/// </summary>
/// <param name="User">
/// The signed-in caller, or <see langword="null"/> when the request carried no credentials. A
/// request whose token failed never reaches an admission: the hub drops it first.
/// </param>
/// <param name="Query">The query string of the connection request.</param>
public sealed record RealTimeRequest(ClaimsPrincipal? User, IQueryCollection Query);
