using Microsoft.EntityFrameworkCore;

namespace TinyUrl.Core;

public class UrlRepository
{
    private readonly AppDbContext _context;

    public UrlRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ShortUrl> CreateAsync(ShortUrl shortUrl)
    {
        _context.ShortUrls.Add(shortUrl);
        await _context.SaveChangesAsync();
        return shortUrl;
    }

    public async Task<ShortUrl?> GetBySlugAsync(string slug)
    {
        return await _context.ShortUrls.FirstOrDefaultAsync(s => s.Slug == slug);
    }

    public async Task<int?> GetClickCountAsync(string slug)
    {
        var shortUrl = await _context.ShortUrls.FirstOrDefaultAsync(s => s.Slug == slug);
        return shortUrl?.ClickCount;
    }
}
