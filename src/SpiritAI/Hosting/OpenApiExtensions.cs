using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SpiritAI.Hosting;

/// <summary>
/// The OpenAPI document this host describes itself with.
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>Adds the document, shaped the way the generated client needs it.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSpiritOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddOpenApi(options => options.AddSchemaTransformer(NumbersAreNumbersAsync));
    }

    /// <summary>
    /// Takes <c>string</c> back out of every numeric type union.
    /// </summary>
    /// <remarks>
    /// Web defaults let a number be read from a string, so the generator describes every
    /// <c>int</c> as <c>["integer", "string"]</c> and the browser gets <c>number | string</c> on
    /// every count, id and quantity in the API. The host never writes one as a string, and this
    /// only edits the document — what the host accepts is untouched.
    /// </remarks>
    /// <param name="schema">One schema in the document.</param>
    /// <param name="context">Which type it came from.</param>
    /// <param name="cancellationToken">Cancels the transform.</param>
    /// <returns>A completed task.</returns>
    private static Task NumbersAreNumbersAsync(
        IOpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema is OpenApiSchema concrete
            && concrete.Type is { } type
            && type.HasFlag(JsonSchemaType.String)
            && (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)))
        {
            concrete.Type = type & ~JsonSchemaType.String;
            concrete.Pattern = null;
        }

        return Task.CompletedTask;
    }
}
