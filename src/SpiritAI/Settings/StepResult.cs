using System.Text.Json.Serialization;

namespace SpiritAI.Settings;

/// <summary>What one step of an Add or a Delete did.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StepResult>))]
public enum StepResult
{
    /// <summary>Nothing to do, so nothing was called.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>The app refused or did not answer; pressing again tries it again.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
