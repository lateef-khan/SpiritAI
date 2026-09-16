using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using SpiritAI.Tests.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Staff;

/// <summary>One signed-in browser, or one that is not signed in at all.</summary>
internal sealed class StaffCaller(HttpClient client, string? token)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url, body: null);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);

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

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
