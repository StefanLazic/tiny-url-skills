using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TinyUrl.Core;

namespace TinyUrl.Api.Tests;

public class ApiTests : IClassFixture<ApiTests.CustomWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public void Dispose()
    {
    }

    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection;

        public CustomWebApplicationFactory()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite(_connection));

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _connection.Dispose();
        }
    }

    [Fact]
    public async Task PostShorten_ValidUrl_ReturnsShortUrl()
    {
        var response = await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "https://example.com/some/long/path"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Slug);
        Assert.Contains(body.Slug, body.ShortUrl);
    }

    [Fact]
    public async Task PostShorten_CustomSlug_ReturnsCustomSlug()
    {
        var response = await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "https://example.com",
            customSlug = "myslug"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShortenResponse>();
        Assert.NotNull(body);
        Assert.Equal("myslug", body.Slug);
    }

    [Fact]
    public async Task PostShorten_WithExpiresAt_Succeeds()
    {
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var response = await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "https://example.com",
            expiresAt = expiresAt
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostShorten_InvalidUrl_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "not-a-url"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostShorten_ConflictingCustomSlug_Returns400()
    {
        await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "https://example.com",
            customSlug = "taken"
        });

        var response = await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "https://other.com",
            customSlug = "taken"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSlug_ValidSlug_RedirectsToOriginalUrl()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "https://example.com/destination"
        });
        var body = await createResponse.Content.ReadFromJsonAsync<ShortenResponse>();

        var response = await _client.GetAsync($"/{body!.Slug}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://example.com/destination", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GetSlug_UnknownSlug_Returns404()
    {
        var response = await _client.GetAsync("/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSlug_ExpiredUrl_Returns410()
    {
        var expiresAt = DateTime.UtcNow.AddSeconds(-1);
        var createResponse = await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "https://example.com",
            expiresAt = expiresAt
        });
        var body = await createResponse.Content.ReadFromJsonAsync<ShortenResponse>();

        var response = await _client.GetAsync($"/{body!.Slug}");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_ValidSlug_ReturnsClickCount()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/shorten", new
        {
            originalUrl = "https://example.com"
        });
        var body = await createResponse.Content.ReadFromJsonAsync<ShortenResponse>();

        // Visit the URL to increment click count
        await _client.GetAsync($"/{body!.Slug}");

        var statsResponse = await _client.GetAsync($"/api/{body.Slug}/stats");

        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        var stats = await statsResponse.Content.ReadFromJsonAsync<StatsResponse>();
        Assert.NotNull(stats);
        Assert.Equal(body.Slug, stats.Slug);
        Assert.Equal(1, stats.ClickCount);
    }

    [Fact]
    public async Task GetStats_UnknownSlug_Returns404()
    {
        var response = await _client.GetAsync("/api/unknown123/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
