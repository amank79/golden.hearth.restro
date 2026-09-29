using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RestaurantPos.Tests;

/// <summary>JSON helpers matching the API's settings (camelCase, enums as text).</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> Read<T>(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException($"{(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        return (await response.Content.ReadFromJsonAsync<T>(Options))!;
    }

    public static Task<HttpResponseMessage> Post<T>(this HttpClient c, string url, T body) => c.PostAsJsonAsync(url, body, Options);

    public static Task<HttpResponseMessage> Put<T>(this HttpClient c, string url, T body) => c.PutAsJsonAsync(url, body, Options);

    public static async Task<T> Get<T>(this HttpClient c, string url) => await (await c.GetAsync(url)).Read<T>();

    /// <summary>Field names that have errors in a 400 ValidationProblem response.</summary>
    public static async Task<string[]> ErrorFields(this HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("errors", out var errors)
            ? errors.EnumerateObject().Select(p => p.Name).ToArray()
            : [];
    }
}
