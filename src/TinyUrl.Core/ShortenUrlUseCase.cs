namespace TinyUrl.Core;

public class ShortenUrlResult
{
    public string ShortUrl { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class ShortenUrlUseCase
{
    private readonly UrlRepository _repository;
    private readonly SlugGenerator _slugGenerator;

    public ShortenUrlUseCase(UrlRepository repository, SlugGenerator slugGenerator)
    {
        _repository = repository;
        _slugGenerator = slugGenerator;
    }

    public async Task<ShortenUrlResult> ShortenAsync(string originalUrl, string baseUrl, string? customSlug = null, DateTime? expiresAt = null)
    {
        ValidateOriginalUrl(originalUrl);

        var slug = _slugGenerator.Generate(customSlug);

        if (customSlug is not null)
        {
            var existing = await _repository.GetBySlugAsync(slug);
            if (existing is not null)
            {
                throw new InvalidOperationException($"Slug '{slug}' is already in use.");
            }
        }

        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            OriginalUrl = originalUrl,
            ClickCount = 0,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(shortUrl);

        return new ShortenUrlResult
        {
            ShortUrl = $"{baseUrl.TrimEnd('/')}/{slug}",
            Slug = slug
        };
    }

    private static void ValidateOriginalUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            throw new ArgumentException("Original URL cannot be empty.", nameof(originalUrl));
        }

        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Original URL is not a valid absolute URL.", nameof(originalUrl));
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            throw new ArgumentException("Original URL must use http or https scheme.", nameof(originalUrl));
        }
    }
}
