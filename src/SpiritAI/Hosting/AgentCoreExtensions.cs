using System.ComponentModel;

using AgentCore.Application.State;
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

    /// <summary>The <c>binds:</c> name <c>spirit.yaml</c> gives the parts reader.</summary>
    public const string PartsBinding = "LookupParts";

    /// <summary>The <c>binds:</c> name <c>spirit.yaml</c> gives the model reader.</summary>
    public const string ModelBinding = "LookupModel";

    /// <summary>The <c>binds:</c> name <c>spirit.yaml</c> gives the unit reader.</summary>
    public const string UnitBinding = "AskUnit";

    /// <summary>Registers AgentCore, carrying this host's analyzers and bindings.</summary>
    /// <param name="builder">The host being built.</param>
    /// <returns>The same builder, so a host chains its calls.</returns>
    public static WebApplicationBuilder AddSpiritAgentCore(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<PartsLookup>();

        builder.AddAgentCoreHost();

        builder.Services
            .AddOptions<AgentCoreOptions>()
            .Configure<IServiceProvider, IHostEnvironment>(Configure);

        return builder;
    }

    /// <summary>Writes this host's word over the defaults AgentCore filled in.</summary>
    /// <param name="options">The options the host is filling.</param>
    /// <param name="services">
    /// The container. A binding reads its lookup out of this when the model calls the tool, which
    /// is long after everything is built: asking for one here instead would close a circle, because
    /// <see cref="PartsLookup"/> reaches the tool registry, and the registry is what these options
    /// are being read to build.
    /// </param>
    /// <param name="environment">Locates the skills folder relative to the host, not the working directory.</param>
    private static void Configure(AgentCoreOptions options, IServiceProvider services, IHostEnvironment environment)
        => options
            .UseSkills(Path.Combine(environment.ContentRootPath, "skills"))
            .UseKnowledgeQueryAnalyzers(new IdentifierCodeAnalyzer(
                () => IdentifierCodeAnalyzer.ProductsIn(services.GetService<VocabularyCache>())))
            .Bind(
                SerialBinding,
                ([Description("The number the person offered as a serial number, exactly as they wrote it.")] string serial)
                    => SerialNumber.Parse(serial))
            .Bind(
                PartsBinding,
                (
                    [Description("The product name, such as F63 or CT900. Give this whenever the person named a machine.")] string? productName,
                    [Description("The year the machine was built, when the person has said it. This is what picks one model number out of the several a product name covers.")] int? year,
                    [Description("A sixteen digit serial number, when the person gave one. It carries its own model number, so it needs no year.")] string? serialNo,
                    [Description("An exact six digit model number, when one is already known.")] string? modelNo,
                    [Description("ONE word that narrows a long list, such as motor, belt or roller. A phrase matches nothing, so send one word and call again for the next.")] string? search,
                    CancellationToken cancellationToken)
                    => services.GetRequiredService<PartsLookup>()
                        .FindAsync(productName, year, serialNo, modelNo, search, cancellationToken))
            .Bind(
                ModelBinding,
                (
                    [Description("The product name, such as LCR or F63.")] string? productName,
                    [Description("The year the machine was built, when the person has said it.")] int? year,
                    CancellationToken cancellationToken)
                    => services.GetRequiredService<ModelIndex>()
                        .FindAsync(productName, year, cancellationToken))
            .Bind(
                UnitBinding,
                (
                    [Description("A 16 digit serial number, when the person gave one.")] string? serialNo,
                    [Description("A work order number, as in 845435-1.")] string? orderNumber,
                    [Description("The customer's name, email or phone, exactly as the person wrote it.")] string? customer,
                    CancellationToken cancellationToken)
                    => services.GetRequiredService<UnitDesk>()
                        .ReadAsync(serialNo, orderNumber, customer, cancellationToken));
}
