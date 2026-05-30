using System.Collections.Concurrent;

namespace TinyUrl.Core;

public class InMemoryClickCounter : IClickCounter
{
    private readonly ConcurrentDictionary<string, int> _counts = new();

    public void Increment(string slug)
    {
        _counts.AddOrUpdate(slug, 1, (_, current) => current + 1);
    }

    public int GetUnflushedCount(string slug)
    {
        return _counts.TryGetValue(slug, out var count) ? count : 0;
    }

    public Dictionary<string, int> DrainAll()
    {
        var drained = new Dictionary<string, int>();

        foreach (var key in _counts.Keys)
        {
            if (_counts.TryRemove(key, out var count))
            {
                drained[key] = count;
            }
        }

        return drained;
    }
}
