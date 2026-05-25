namespace TinyUrl.Api.Models;

public class CreateShortUrlRequest
{
    public string OriginalUrl { get; set; } = string.Empty;
    public string? CustomSlug { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CreateShortUrlResponse
{
    public string ShortUrl { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
