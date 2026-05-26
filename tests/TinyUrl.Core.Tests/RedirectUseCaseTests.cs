using Microsoft.EntityFrameworkCore;
using TinyUrl.Core;

namespace TinyUrl.Core.Tests;

public class RedirectUseCaseTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UrlRepository _repository;
    private readonly RedirectUseCase _useCase;

    public RedirectUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AppDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new UrlRepository(_context);
        _useCase = new RedirectUseCase(_repository);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task RedirectAsync_WithValidNonExpiredSlug_ReturnsOriginalUrl()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "abc1234",
            OriginalUrl = "https://example.com/destination",
            ClickCount = 0,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        var result = await _useCase.RedirectAsync("abc1234");

        Assert.NotNull(result);
        Assert.Equal("https://example.com/destination", result.OriginalUrl);
        Assert.False(result.IsGone);
    }

    [Fact]
    public async Task RedirectAsync_WithExpiredSlug_ReturnsGone()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "expired1",
            OriginalUrl = "https://example.com/old-page",
            ClickCount = 0,
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };
        await _repository.CreateAsync(shortUrl);

        var result = await _useCase.RedirectAsync("expired1");

        Assert.True(result.IsGone);
        Assert.Null(result.OriginalUrl);
    }

    [Fact]
    public async Task RedirectAsync_WithUnknownSlug_ReturnsNotFound()
    {
        var result = await _useCase.RedirectAsync("unknown1");

        Assert.True(result.IsNotFound);
        Assert.Null(result.OriginalUrl);
    }

    [Fact]
    public async Task RedirectAsync_WithValidSlug_IncrementsClickCount()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "click12",
            OriginalUrl = "https://example.com/track",
            ClickCount = 0,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        await _useCase.RedirectAsync("click12");
        await _useCase.RedirectAsync("click12");

        var stored = await _context.ShortUrls.FirstOrDefaultAsync(s => s.Slug == "click12");
        Assert.NotNull(stored);
        Assert.Equal(2, stored.ClickCount);
    }

    [Fact]
    public async Task RedirectAsync_WithExpiredSlug_DoesNotIncrementClickCount()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "noclick1",
            OriginalUrl = "https://example.com/expired",
            ClickCount = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        await _repository.CreateAsync(shortUrl);

        await _useCase.RedirectAsync("noclick1");

        var stored = await _context.ShortUrls.FirstOrDefaultAsync(s => s.Slug == "noclick1");
        Assert.NotNull(stored);
        Assert.Equal(0, stored.ClickCount);
    }
}
