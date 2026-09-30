using System.Text.Json.Serialization;

namespace SpiritAI.Hub;

/// <summary>Whether a Person has a working sign-in to a Hub app (hub spec, section 4.5).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LinkState>))]
public enum LinkState
{
    /// <summary>No Linked user has ever been created.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>The user was created but the create has not finished (hub spec, section 5.3).</summary>
    [JsonStringEnumMemberName("unfinished")]
    Unfinished,

    /// <summary>The Person can sign in.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,
}
