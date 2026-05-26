namespace TinyUrl.Core;

public class RedirectResult
{
    public string? OriginalUrl { get; set; }
    public bool IsGone { get; set; }
    public bool IsNotFound { get; set; }
}

public class RedirectUseCase
{
    private readonly UrlRepository _repository;

    public RedirectUseCase(UrlRepository repository)
    {
        _repository = repository;
    }

    public async Task<RedirectResult> RedirectAsync(string slug)
    {
        var shortUrl = await _repository.GetBySlugAsync(slug);

        if (shortUrl is null)
        {
            return new RedirectResult { IsNotFound = true };
        }

        if (shortUrl.ExpiresAt.HasValue && shortUrl.ExpiresAt.Value <= DateTime.UtcNow)
        {
            return new RedirectResult { IsGone = true };
        }

        await _repository.IncrementClickCountAsync(slug);

        return new RedirectResult { OriginalUrl = shortUrl.OriginalUrl };
    }
}
