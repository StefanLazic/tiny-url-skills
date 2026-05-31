using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TinyUrl.Core;

public enum RedirectStatus
{
    Success,
    Gone,
    NotFound
}

public class RedirectResult
{
    public string? OriginalUrl { get; }
    public RedirectStatus Status { get; }

    public bool IsGone => Status == RedirectStatus.Gone;
    public bool IsNotFound => Status == RedirectStatus.NotFound;

    private RedirectResult(RedirectStatus status, string? originalUrl = null)
    {
        Status = status;
        OriginalUrl = originalUrl;
    }

    public static RedirectResult Success(string originalUrl) => new(RedirectStatus.Success, originalUrl);
    public static RedirectResult Gone() => new(RedirectStatus.Gone);
    public static RedirectResult NotFound() => new(RedirectStatus.NotFound);
}

public class RedirectUseCase
{
    private readonly UrlRepository _repository;
    private readonly IClickCounter _clickCounter;
    private readonly TimeProvider _timeProvider;
    private readonly IMemoryCache _cache;
    private readonly CacheSettings _cacheSettings;

    public RedirectUseCase(
        UrlRepository repository,
        IClickCounter clickCounter,
        IMemoryCache cache,
        IOptions<CacheSettings> cacheSettings,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _clickCounter = clickCounter;
        _cache = cache;
        _cacheSettings = cacheSettings.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RedirectResult> RedirectAsync(string slug)
    {
        var cacheKey = $"slug:{slug}";

        var shortUrl = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            var entity = await _repository.GetBySlugAsync(slug);
            if (entity is null)
            {
                entry.Dispose();
                return null;
            }

            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.TtlMinutes);
            entry.Size = 1;
            return entity;
        });

        if (shortUrl is null)
        {
            _cache.Remove(cacheKey);
            return RedirectResult.NotFound();
        }

        if (shortUrl.ExpiresAt.HasValue && shortUrl.ExpiresAt.Value <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            return RedirectResult.Gone();
        }

        _clickCounter.Increment(slug);

        return RedirectResult.Success(shortUrl.OriginalUrl);
    }
}
