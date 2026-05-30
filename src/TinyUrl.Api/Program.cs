using Microsoft.EntityFrameworkCore;
using TinyUrl.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=tinyurl.db"));
builder.Services.AddSingleton<IClickCounter, InMemoryClickCounter>();
builder.Services.AddScoped<UrlRepository>();
builder.Services.AddScoped<SlugGenerator>();
builder.Services.AddScoped<ShortenUrlUseCase>();
builder.Services.AddScoped<RedirectUseCase>();
builder.Services.AddScoped<StatsUseCase>();
builder.Services.AddHostedService<ClickCountFlusher>();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapPost("/api/shorten", async (ShortenRequest request, ShortenUrlUseCase useCase, HttpRequest httpRequest) =>
{
    var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host}";
    try
    {
        var result = await useCase.ShortenAsync(request.OriginalUrl, baseUrl, request.CustomSlug, request.ExpiresAt);
        return Results.Ok(new ShortenResponse(result.ShortUrl, result.Slug));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/{slug}", async (string slug, RedirectUseCase useCase) =>
{
    var result = await useCase.RedirectAsync(slug);
    return result.Status switch
    {
        RedirectStatus.Success => Results.Redirect(result.OriginalUrl!, permanent: false),
        RedirectStatus.Gone => Results.StatusCode(410),
        _ => Results.NotFound()
    };
});

app.MapGet("/api/{slug}/stats", async (string slug, StatsUseCase useCase) =>
{
    var clickCount = await useCase.GetClickCountAsync(slug);
    if (clickCount is null)
    {
        return Results.NotFound();
    }
    return Results.Ok(new StatsResponse(slug, clickCount.Value));
});

app.Run();

public record ShortenRequest(string OriginalUrl, string? CustomSlug = null, DateTime? ExpiresAt = null);
public record ShortenResponse(string ShortUrl, string Slug);
public record StatsResponse(string Slug, int ClickCount);

public partial class Program { }
