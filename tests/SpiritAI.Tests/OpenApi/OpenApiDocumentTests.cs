using System.Text.Json;

using AgentCore.Application.Calls.Memory;
using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Hosting;
using SpiritAI.Lookup;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.OpenApi;

/// <summary>
/// The contract the browser's API client is generated from.
/// </summary>
/// <remarks>
/// <para>
/// The document is built here rather than by <c>Microsoft.Extensions.ApiDescription.Server</c> at
/// build time. That tool runs the real <c>Program</c>, and this host's startup reads
/// <c>config/spirit.yaml</c>, demands <c>OPENAI_API_KEY</c> and <c>Auth:Neon:BaseUrl</c>, and opens
/// the DAB MCP server over Tailscale. A build machine has none of those, so the document would be
/// unbuildable in exactly the place it is needed most.
/// </para>
/// <para>
/// Only the routes the browser generates a client for are mapped. AgentCore's chat endpoint is
/// deliberately absent: it answers with an SSE stream that <c>transport.ts</c> reads by hand, and a
/// generated client for it would be wrong rather than merely unused.
/// </para>
/// </remarks>
public sealed class OpenApiDocumentTests
{
    /// <summary>Where the document is written, relative to the repository root.</summary>
    private const string DocumentPath = "src/web/openapi/v1.json";

    /// <summary>The operations the browser expects to find a function for.</summary>
    private static readonly string[] Expected =
    [
        "listThreads",
        "createThread",
        "getThread",
        "getThreadMessages",
        "updateThread",
        "deleteThread",
        "getUnit",
        "getOrder",
    ];

    /// <summary>
    /// Writes the document the generator reads, so a route change shows up as a diff.
    /// </summary>
    /// <remarks>
    /// CI runs the suite and then <c>git diff --exit-code</c>. A route whose shape changed without
    /// its client being regenerated fails there, which is the whole reason the file is checked in
    /// rather than built into <c>obj</c>.
    /// </remarks>
    [Fact]
    public async Task TheDocumentOnDiskMatchesTheRoutes()
    {
        var written = await BuildAsync();
        var path = Path.Combine(RepositoryRoot(), DocumentPath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, written, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(written));
    }

    /// <summary>
    /// Every described route answers with a named type, so no generated body comes back as unknown.
    /// </summary>
    /// <remarks>
    /// This is what stops a handler drifting back to a bare <c>IResult</c> with no
    /// <c>Produces&lt;T&gt;</c> beside it. The failure it catches is silent otherwise: the build
    /// stays green, the client generates, and every field on the response is gone.
    /// </remarks>
    [Fact]
    public async Task EveryDescribedRouteNamesTheTypeItAnswersWith()
    {
        using var document = JsonDocument.Parse(await BuildAsync());

        var operations = Operations(document).ToDictionary(o => o.OperationId, o => o.Body);

        Assert.Equal([.. Expected.Order()], [.. operations.Keys.Order()]);

        foreach (var (operationId, body) in operations)
        {
            if (body is null)
            {
                continue;
            }

            Assert.True(
                body.Value.TryGetProperty("$ref", out _),
                $"{operationId} answers with an inline schema rather than a named type. "
                    + "Give its route a Produces<T>() naming the record it returns.");
        }
    }

    /// <summary>The title route is left out, and a client must not be generated for it.</summary>
    [Fact]
    public async Task TheTitleStreamIsNotInTheDocument()
    {
        using var document = JsonDocument.Parse(await BuildAsync());

        Assert.DoesNotContain("/title", document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name));
    }

    /// <summary>Reads each operation's id and the schema of its success body, if it has one.</summary>
    /// <param name="document">The whole OpenAPI document.</param>
    /// <returns>One entry per described operation.</returns>
    private static IEnumerable<(string OperationId, JsonElement? Body)> Operations(JsonDocument document)
    {
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var operation = method.Value;

                yield return (
                    operation.GetProperty("operationId").GetString()!,
                    SuccessBody(operation));
            }
        }
    }

    /// <summary>The schema of a 200 or 201 body, or null when the route answers with none.</summary>
    /// <param name="operation">One described operation.</param>
    /// <returns>The schema element, or <see langword="null"/> for a 204.</returns>
    private static JsonElement? SuccessBody(JsonElement operation)
    {
        var responses = operation.GetProperty("responses");

        foreach (var code in (string[])["200", "201"])
        {
            if (responses.TryGetProperty(code, out var response)
                && response.TryGetProperty("content", out var content)
                && content.TryGetProperty("application/json", out var json))
            {
                return json.GetProperty("schema");
            }
        }

        return null;
    }

    /// <summary>Builds the document from the routes themselves.</summary>
    /// <returns>The document, as the generator will read it.</returns>
    private static async Task<string> BuildAsync()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSpiritOpenApi();
                    services.AddRouting();

                    // Present so the route builder reads these as injected services rather than as
                    // request bodies. Nothing calls them: no route is ever invoked here.
                    services.AddSingleton<ICallStore>(new InMemoryCallStore());
                    services.AddSingleton<ICallTitler>(new SilentTitler());
                    services.AddSingleton(new UnitLookup(
                        (_, _, _) => ValueTask.FromResult(default(System.Text.Json.JsonElement))));
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapThreads();
                        endpoints.MapLookup();
                        endpoints.MapOpenApi();
                    });
                }))
            .StartAsync(TestContext.Current.CancellationToken);

        return await host.GetTestClient()
            .GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Walks up from the test binaries to the directory holding the solution.</summary>
    /// <returns>The repository root.</returns>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SpiritAI.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("SpiritAI.slnx is above no parent of the test binaries.");
    }

    /// <summary>A titler that is registered and never asked for anything.</summary>
    private sealed class SilentTitler : ICallTitler
    {
        public IAsyncEnumerable<string> GenerateAsync(string callId, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<string>();

        public IAsyncEnumerable<string> GenerateFromAsync(
            string callId,
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<string>();
    }
}
