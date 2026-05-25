using System.Security.Cryptography;

namespace TinyUrl.Core;

public class SlugGenerator
{
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int DefaultLength = 7;

    public string Generate(string? customSlug = null)
    {
        if (customSlug is not null)
        {
            ValidateCustomSlug(customSlug);
            return customSlug;
        }

        return GenerateRandom(DefaultLength);
    }

    private static void ValidateCustomSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Custom slug cannot be empty or whitespace.", nameof(slug));
        }

        if (!IsUrlSafe(slug))
        {
            throw new ArgumentException("Custom slug contains invalid characters. Only alphanumeric characters, hyphens, underscores, and periods are allowed.", nameof(slug));
        }
    }

    private static bool IsUrlSafe(string slug)
    {
        foreach (var c in slug)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
            {
                return false;
            }
        }
        return true;
    }

    private static string GenerateRandom(int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = Base62Chars[RandomNumberGenerator.GetInt32(Base62Chars.Length)];
        }
        return new string(chars);
    }
}
