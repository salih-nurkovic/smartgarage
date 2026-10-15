using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartGarage.Application.Dtos.Auth;

namespace SmartGarage.IntegrationTests.Support;

public static class ApiClientExtensions
{
    /// <summary>The API writes enums as names, so the tests read them the same way.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The password every seeded account has, as the instructions prescribe.</summary>
    public const string SeedPassword = "test";

    /// <summary>A password that satisfies the live registration policy.</summary>
    public const string StrongPassword = "Lozinka!123";

    public static async Task<AuthResponse> LoginAsync(
        this HttpClient client,
        string userName,
        string password = SeedPassword)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { UserName = userName, Password = password });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    /// <summary>Registers a customer with a name no other test has used.</summary>
    public static async Task<(AuthResponse Auth, string UserName)> RegisterNewCustomerAsync(
        this HttpClient client)
    {
        var userName = $"test{Guid.NewGuid():N}"[..20];

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            FirstName = "Test",
            LastName = "Korisnik",
            PhoneNumber = "+387 61 000 000",
            Password = StrongPassword,
            ConfirmPassword = StrongPassword,
        });

        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<AuthResponse>())!, userName);
    }

    public static HttpRequestMessage WithBearer(this HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public static Task<HttpResponseMessage> GetAsync(
        this HttpClient client,
        string url,
        string accessToken) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url).WithBearer(accessToken));

    public static Task<HttpResponseMessage> DeleteAsync(
        this HttpClient client,
        string url,
        string accessToken) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url).WithBearer(accessToken));

    /// <summary>Uploads bytes as a multipart form file, the way both clients do.</summary>
    public static Task<HttpResponseMessage> PostFileAsync(
        this HttpClient client,
        string url,
        byte[] bytes,
        string fileName,
        string contentType,
        string accessToken)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent { { file, "file", fileName } };

        return client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, url) { Content = form }.WithBearer(accessToken));
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static Task<HttpResponseMessage> SendJsonAsync<T>(
        this HttpClient client,
        HttpMethod method,
        string url,
        T body,
        string accessToken) =>
        client.SendAsync(
            new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) }
                .WithBearer(accessToken));
}
