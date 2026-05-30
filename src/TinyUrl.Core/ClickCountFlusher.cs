using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TinyUrl.Core;

public class ClickCountFlusher : BackgroundService
{
    private readonly IClickCounter _clickCounter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _flushInterval;

    public ClickCountFlusher(IClickCounter clickCounter, IServiceScopeFactory scopeFactory)
        : this(clickCounter, scopeFactory, null)
    {
    }

    public ClickCountFlusher(IClickCounter clickCounter, IServiceScopeFactory scopeFactory, TimeSpan? flushInterval)
    {
        _clickCounter = clickCounter;
        _scopeFactory = scopeFactory;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_flushInterval, stoppingToken);
            await FlushAsync();
        }
    }

    public async Task FlushAsync()
    {
        var drained = _clickCounter.DrainAll();

        if (drained.Count == 0)
            return;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var (slug, delta) in drained)
        {
            await context.ShortUrls
                .Where(s => s.Slug == slug)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ClickCount, p => p.ClickCount + delta));
        }
    }
}
