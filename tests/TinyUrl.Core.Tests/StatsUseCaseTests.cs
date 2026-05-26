using Microsoft.EntityFrameworkCore;
using TinyUrl.Core;

namespace TinyUrl.Core.Tests;

public class StatsUseCaseTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UrlRepository _repository;
    private readonly StatsUseCase _useCase;

    public StatsUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AppDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new UrlRepository(_context);
        _useCase = new StatsUseCase(_repository);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task GetClickCountAsync_WithExistingSlug_ReturnsClickCount()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "stats01",
            OriginalUrl = "https://example.com/page",
            ClickCount = 5,
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        var result = await _useCase.GetClickCountAsync("stats01");

        Assert.NotNull(result);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task GetClickCountAsync_WithUnknownSlug_ReturnsNull()
    {
        var result = await _useCase.GetClickCountAsync("unknown1");

        Assert.Null(result);
    }
}
