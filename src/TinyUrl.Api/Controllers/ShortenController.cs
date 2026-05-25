using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyUrl.Api.Data;
using TinyUrl.Api.Models;
using TinyUrl.Api.Services;

namespace TinyUrl.Api.Controllers;

[ApiController]
public class ShortenController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISlugGenerator _slugGenerator;

    public ShortenController(AppDbContext db, ISlugGenerator slugGenerator)
    {
        _db = db;
        _slugGenerator = slugGenerator;
    }

    [HttpPost("/api/shorten")]
    public async Task<IActionResult> Create([FromBody] CreateShortUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalUrl))
        {
            return BadRequest(new { error = "OriginalUrl is required." });
        }

        if (!Uri.TryCreate(request.OriginalUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return BadRequest(new { error = "OriginalUrl must be a valid absolute HTTP or HTTPS URL." });
        }

        var slug = request.CustomSlug;

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var exists = await _db.ShortUrls.AnyAsync(s => s.Slug == slug);
            if (exists)
            {
                return Conflict(new { error = "Custom Slug is already in use." });
            }
        }
        else
        {
            slug = _slugGenerator.Generate();
        }

        var shortUrl = new ShortUrl
        {
            Slug = slug,
            OriginalUrl = request.OriginalUrl,
            ClickCount = 0,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _db.ShortUrls.Add(shortUrl);
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = new CreateShortUrlResponse
        {
            ShortUrl = $"{baseUrl}/{shortUrl.Slug}",
            Slug = shortUrl.Slug,
            OriginalUrl = shortUrl.OriginalUrl,
            ExpiresAt = shortUrl.ExpiresAt,
            CreatedAt = shortUrl.CreatedAt
        };

        return Created(response.ShortUrl, response);
    }
}
