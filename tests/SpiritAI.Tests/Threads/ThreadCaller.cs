using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>One signed-in browser, or one that is not signed in at all.</summary>
internal sealed class ThreadCaller(HttpClient client, string? token)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url, body: null);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);

    public Task<HttpResponseMessage> PatchAsync(string url, object body) => SendAsync(HttpMethod.Patch, url, body);

    public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, body: null);

    public async Task<string> CreateThreadAsync()
    {
        var response = await PostAsync(ThreadWorld.Threads);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<ThreadCreated>()).RemoteId;
    }

    public async Task<ThreadPage> ListAsync(string query = "")
    {
        var response = await GetAsync(ThreadWorld.Threads + query);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ThreadPage>();
    }

    public async Task<ThreadSummary> FetchAsync(string remoteId)
    {
        var response = await GetAsync($"{ThreadWorld.Threads}/{remoteId}");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ThreadSummary>();
    }

    public async Task<ThreadHistory> HistoryAsync(string remoteId, string query = "")
    {
        var response = await GetAsync($"{ThreadWorld.Threads}/{remoteId}/messages{query}");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ThreadHistory>();
    }

    public async Task<string> TitleAsync(string remoteId, object body)
    {
        var response = await PostAsync($"{ThreadWorld.Threads}/{remoteId}/title", body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    public async Task<string> RawAsync(string url)
    {
        var response = await GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
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
