using System.Net.Http.Json;
using System.Text.Json;

using SpiritAI.PublicChat;
using SpiritAI.Tests.Threads;

using Xunit;

namespace SpiritAI.Tests.PublicChat;

/// <summary>One widget, sending the key it keeps in <c>localStorage</c>, or one sending none at all.</summary>
internal sealed class VisitorCaller(HttpClient client, string? visitorKey)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The key this widget's calls are filed under, or <see langword="null"/> when it sends none.</summary>
    public string? Key => visitorKey is null ? null : VisitorPrincipal.KeyOf(visitorKey);

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url, body: null);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);

    public Task<HttpResponseMessage> PatchAsync(string url, object body) => SendAsync(HttpMethod.Patch, url, body);

    /// <summary>Reads a route that must succeed, as the record it answers with.</summary>
    public async Task<T> ReadAsync<T>(string url)
    {
        var response = await GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<T>();
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);

        if (visitorKey is not null)
        {
            request.Headers.TryAddWithoutValidation(VisitorPrincipal.Header, visitorKey);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
