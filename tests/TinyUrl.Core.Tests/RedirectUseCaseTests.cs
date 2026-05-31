using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TinyUrl.Core;

namespace TinyUrl.Core.Tests;

public class RedirectUseCaseTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UrlRepository _repository;
    private readonly InMemoryClickCounter _clickCounter;
    private readonly IMemoryCache _cache;
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
        _clickCounter = new InMemoryClickCounter();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var cacheSettings = Options.Create(new CacheSettings());
        _useCase = new RedirectUseCase(_repository, _clickCounter, _cache, cacheSettings);
    }

    public void Dispose()
    {
        _cache.Dispose();
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

        Assert.Equal(2, _clickCounter.GetUnflushedCount("click12"));

        // DB should not have been updated (no flush yet)
        var stored = await _context.ShortUrls.FirstOrDefaultAsync(s => s.Slug == "click12");
        Assert.NotNull(stored);
        Assert.Equal(0, stored.ClickCount);
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

    [Fact]
    public async Task RedirectAsync_SecondCall_ServesFromCacheWithoutHittingRepository()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "cached1",
            OriginalUrl = "https://example.com/cached",
            ClickCount = 0,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        // First call populates cache
        var result1 = await _useCase.RedirectAsync("cached1");
        Assert.Equal("https://example.com/cached", result1.OriginalUrl);

        // Remove from DB — second call should still succeed via cache
        var entity = await _context.ShortUrls.FirstAsync(s => s.Slug == "cached1");
        _context.ShortUrls.Remove(entity);
        await _context.SaveChangesAsync();

        // Second call serves from cache
        var result2 = await _useCase.RedirectAsync("cached1");
        Assert.Equal("https://example.com/cached", result2.OriginalUrl);
    }

    [Fact]
    public async Task RedirectAsync_CachedButExpired_ReturnsGone()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cacheSettings = Options.Create(new CacheSettings());
        var useCase = new RedirectUseCase(_repository, _clickCounter, _cache, cacheSettings, fakeTime);

        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "willexp1",
            OriginalUrl = "https://example.com/will-expire",
            ClickCount = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        // First call succeeds and caches
        var result1 = await useCase.RedirectAsync("willexp1");
        Assert.Equal("https://example.com/will-expire", result1.OriginalUrl);

        // Advance time past expiration
        fakeTime.Advance(TimeSpan.FromMinutes(15));

        // Second call returns Gone even though cached
        var result2 = await useCase.RedirectAsync("willexp1");
        Assert.True(result2.IsGone);
    }

    [Fact]
    public async Task RedirectAsync_NotFoundSlugIsNotCached_SubsequentInsertIsVisible()
    {
        // First call — slug doesn't exist
        var result1 = await _useCase.RedirectAsync("later1");
        Assert.True(result1.IsNotFound);

        // Insert the slug into DB
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "later1",
            OriginalUrl = "https://example.com/later",
            ClickCount = 0,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        // Second call should find it (miss was not cached)
        var result2 = await _useCase.RedirectAsync("later1");
        Assert.Equal("https://example.com/later", result2.OriginalUrl);
    }
}
