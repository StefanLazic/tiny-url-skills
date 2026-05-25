using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TinyUrl.Api.Data;
using TinyUrl.Api.Models;

namespace TinyUrl.Tests;

public class CreateShortUrlTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CreateShortUrlTests(WebApplicationFactory<Program> factory)
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite(_connection));
            });
        });

        // Ensure database is created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task Create_WithValidUrl_ReturnsCreatedWithSlug()
    {
        var request = new { OriginalUrl = "https://example.com/long-page" };

        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateShortUrlResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("https://example.com/long-page", body.OriginalUrl);
        Assert.False(string.IsNullOrWhiteSpace(body.Slug));
        Assert.Contains(body.Slug, body.ShortUrl);
    }

    [Fact]
    public async Task Create_WithCustomSlug_UsesProvidedSlug()
    {
        var request = new { OriginalUrl = "https://example.com/page", CustomSlug = "my-custom" };

        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateShortUrlResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("my-custom", body.Slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid-scheme.com")]
    public async Task Create_WithInvalidUrl_ReturnsBadRequest(string invalidUrl)
    {
        var request = new { OriginalUrl = invalidUrl };

        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateCustomSlug_ReturnsConflict()
    {
        var request1 = new { OriginalUrl = "https://example.com/first", CustomSlug = "taken" };
        var request2 = new { OriginalUrl = "https://example.com/second", CustomSlug = "taken" };

        await _client.PostAsJsonAsync("/api/shorten", request1);
        var response = await _client.PostAsJsonAsync("/api/shorten", request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithExpiresAt_StoresExpiration()
    {
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var request = new { OriginalUrl = "https://example.com/expires", ExpiresAt = expiresAt };

        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateShortUrlResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotNull(body.ExpiresAt);
    }

    [Fact]
    public async Task Create_WithoutExpiresAt_ReturnsNullExpiration()
    {
        var request = new { OriginalUrl = "https://example.com/no-expiry" };

        var response = await _client.PostAsJsonAsync("/api/shorten", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateShortUrlResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Null(body.ExpiresAt);
    }

    [Fact]
    public async Task Create_SameUrlTwice_GeneratesDifferentSlugs()
    {
        var request = new { OriginalUrl = "https://example.com/same" };

        var response1 = await _client.PostAsJsonAsync("/api/shorten", request);
        var response2 = await _client.PostAsJsonAsync("/api/shorten", request);

        var body1 = await response1.Content.ReadFromJsonAsync<CreateShortUrlResponse>(JsonOptions);
        var body2 = await response2.Content.ReadFromJsonAsync<CreateShortUrlResponse>(JsonOptions);
        Assert.NotNull(body1);
        Assert.NotNull(body2);
        Assert.NotEqual(body1.Slug, body2.Slug);
    }
}
