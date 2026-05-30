using Microsoft.EntityFrameworkCore;
using TinyUrl.Core;

namespace TinyUrl.Core.Tests;

public class UrlRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UrlRepository _repository;

    public UrlRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AppDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new UrlRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_StoresRecord_AndGetBySlugAsync_RetrievesIt()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "abc1234",
            OriginalUrl = "https://example.com/long-url",
            ClickCount = 0,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(shortUrl);

        var retrieved = await _repository.GetBySlugAsync("abc1234");

        Assert.NotNull(retrieved);
        Assert.Equal("abc1234", retrieved.Slug);
        Assert.Equal("https://example.com/long-url", retrieved.OriginalUrl);
        Assert.Equal(0, retrieved.ClickCount);
        Assert.Null(retrieved.ExpiresAt);
    }

    [Fact]
    public async Task GetBySlugAsync_WhenSlugDoesNotExist_ReturnsNull()
    {
        var result = await _repository.GetBySlugAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetClickCountAsync_ReturnsClickCount()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "stats01",
            OriginalUrl = "https://example.com",
            ClickCount = 5,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        var clickCount = await _repository.GetClickCountAsync("stats01");

        Assert.Equal(5, clickCount);
    }

    [Fact]
    public async Task GetClickCountAsync_WhenSlugDoesNotExist_ReturnsNull()
    {
        var result = await _repository.GetClickCountAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_WithExpiresAt_PersistsExpirationDate()
    {
        var expiresAt = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "expire1",
            OriginalUrl = "https://example.com",
            ClickCount = 0,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(shortUrl);

        var retrieved = await _repository.GetBySlugAsync("expire1");
        Assert.NotNull(retrieved);
        Assert.Equal(expiresAt, retrieved.ExpiresAt);
    }
}
