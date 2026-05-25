using TinyUrl.Core;

namespace TinyUrl.Core.Tests;

public class SlugGeneratorTests
{
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    [Fact]
    public void Generate_WithNoCustomSlug_ReturnsSlugOfDefaultLength()
    {
        var generator = new SlugGenerator();

        var slug = generator.Generate();

        Assert.Equal(7, slug.Length);
    }

    [Fact]
    public void Generate_WithNoCustomSlug_ReturnsOnlyBase62Characters()
    {
        var generator = new SlugGenerator();

        var slug = generator.Generate();

        Assert.All(slug.ToCharArray(), c => Assert.Contains(c, Base62Chars));
    }

    [Fact]
    public void Generate_WithValidCustomSlug_ReturnsCustomSlugUnchanged()
    {
        var generator = new SlugGenerator();

        var slug = generator.Generate("my-custom-slug");

        Assert.Equal("my-custom-slug", slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WithEmptyOrWhitespaceCustomSlug_ThrowsArgumentException(string customSlug)
    {
        var generator = new SlugGenerator();

        Assert.Throws<ArgumentException>(() => generator.Generate(customSlug));
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("has@symbol")]
    [InlineData("has#hash")]
    [InlineData("has?query")]
    public void Generate_WithNonUrlSafeCustomSlug_ThrowsArgumentException(string customSlug)
    {
        var generator = new SlugGenerator();

        Assert.Throws<ArgumentException>(() => generator.Generate(customSlug));
    }
}
