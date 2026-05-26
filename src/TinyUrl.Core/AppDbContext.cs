using Microsoft.EntityFrameworkCore;

namespace TinyUrl.Core;

public class AppDbContext : DbContext
{
    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShortUrl>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Slug).HasMaxLength(12);
            entity.Property(e => e.OriginalUrl).IsRequired();
            entity.Property(e => e.ClickCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).IsRequired();
        });
    }
}
