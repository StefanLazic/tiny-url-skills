using System.Security.Cryptography;

namespace TinyUrl.Api.Services;

public class Base62SlugGenerator : ISlugGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int SlugLength = 7;

    public string Generate()
    {
        return string.Create(SlugLength, Alphabet, (span, alphabet) =>
        {
            Span<byte> randomBytes = stackalloc byte[SlugLength];
            RandomNumberGenerator.Fill(randomBytes);
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[randomBytes[i] % alphabet.Length];
            }
        });
    }
}
