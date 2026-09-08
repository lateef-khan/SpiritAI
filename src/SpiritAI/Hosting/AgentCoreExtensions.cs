using System.ComponentModel;

using AgentCore.AspNetCore.DependencyInjection;
using AgentCore.Hosting;

using SpiritAI.Knowledge;
using SpiritAI.Lookup;

namespace SpiritAI.Hosting;

/// <summary>
/// Everything this host says about AgentCore that <c>spirit.yaml</c> cannot say for itself.
/// </summary>
public static class AgentCoreExtensions
{
    /// <summary>The <c>binds:</c> name <c>spirit.yaml</c> gives the serial reader.</summary>
    public const string SerialBinding = "ParseSerial";

    /// <summary>Registers AgentCore, carrying this host's analyzers and bindings.</summary>
    /// <param name="builder">The host being built.</param>
    /// <returns>The same builder, so a host chains its calls.</returns>
    public static WebApplicationBuilder AddSpiritAgentCore(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddAgentCoreHost(Configure);
    }

    /// <summary>Writes this host's word over the defaults AgentCore filled in.</summary>
    /// <param name="options">The options the host is filling.</param>
    private static void Configure(AgentCoreOptions options)
        => options
            .UseKnowledgeQueryAnalyzers(new IdentifierCodeAnalyzer())
            .Bind(
                SerialBinding,
                ([Description("The number the person offered as a serial number, exactly as they wrote it.")] string serial)
                    => SerialNumber.Parse(serial));
}
