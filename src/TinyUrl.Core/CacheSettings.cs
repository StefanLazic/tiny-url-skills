namespace TinyUrl.Core;

public class CacheSettings
{
    public int TtlMinutes { get; set; } = 5;
    public int SizeLimit { get; set; } = 10000;
}
