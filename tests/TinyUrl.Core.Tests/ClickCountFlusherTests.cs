using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TinyUrl.Core;

namespace TinyUrl.Core.Tests;

public class ClickCountFlusherTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly UrlRepository _repository;
    private readonly InMemoryClickCounter _clickCounter;
    private readonly ClickCountFlusher _flusher;

    public ClickCountFlusherTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new UrlRepository(_context);
        _clickCounter = new InMemoryClickCounter();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(_connection));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _flusher = new ClickCountFlusher(_clickCounter, scopeFactory, TimeSpan.FromMilliseconds(100));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task FlushAsync_WithPendingCounts_UpdatesDatabaseAtomically()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "flush01",
            OriginalUrl = "https://example.com",
            ClickCount = 5,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        _clickCounter.Increment("flush01");
        _clickCounter.Increment("flush01");
        _clickCounter.Increment("flush01");

        await _flusher.FlushAsync();

        var updated = await _context.ShortUrls.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == "flush01");
        Assert.NotNull(updated);
        Assert.Equal(8, updated.ClickCount);
    }

    [Fact]
    public async Task FlushAsync_WithNoPendingCounts_DoesNothing()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "flush02",
            OriginalUrl = "https://example.com",
            ClickCount = 3,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        await _flusher.FlushAsync();

        var stored = await _context.ShortUrls.FirstOrDefaultAsync(s => s.Slug == "flush02");
        Assert.NotNull(stored);
        Assert.Equal(3, stored.ClickCount);
    }

    [Fact]
    public async Task FlushAsync_AfterDrain_MemoryIsReset()
    {
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = "flush03",
            OriginalUrl = "https://example.com",
            ClickCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.CreateAsync(shortUrl);

        _clickCounter.Increment("flush03");
        _clickCounter.Increment("flush03");

        await _flusher.FlushAsync();

        Assert.Equal(0, _clickCounter.GetUnflushedCount("flush03"));
    }
}
