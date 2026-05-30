using TinyUrl.Core;

namespace TinyUrl.Core.Tests;

public class InMemoryClickCounterTests
{
    private readonly InMemoryClickCounter _counter = new();

    [Fact]
    public void Increment_SingleSlug_ReturnsOne()
    {
        _counter.Increment("abc123");

        Assert.Equal(1, _counter.GetUnflushedCount("abc123"));
    }

    [Fact]
    public void Increment_SameSlugMultipleTimes_AccumulatesCount()
    {
        for (int i = 0; i < 5; i++)
        {
            _counter.Increment("abc123");
        }

        Assert.Equal(5, _counter.GetUnflushedCount("abc123"));
    }

    [Fact]
    public void Increment_DifferentSlugs_TracksIndependently()
    {
        _counter.Increment("slugA");
        _counter.Increment("slugA");
        _counter.Increment("slugB");

        Assert.Equal(2, _counter.GetUnflushedCount("slugA"));
        Assert.Equal(1, _counter.GetUnflushedCount("slugB"));
    }

    [Fact]
    public void GetUnflushedCount_UnknownSlug_ReturnsZero()
    {
        Assert.Equal(0, _counter.GetUnflushedCount("unknown"));
    }

    [Fact]
    public void DrainAll_ReturnsAccumulatedCountsAndResets()
    {
        _counter.Increment("slugA");
        _counter.Increment("slugA");
        _counter.Increment("slugB");

        var drained = _counter.DrainAll();

        Assert.Equal(2, drained["slugA"]);
        Assert.Equal(1, drained["slugB"]);
        Assert.Equal(0, _counter.GetUnflushedCount("slugA"));
        Assert.Equal(0, _counter.GetUnflushedCount("slugB"));
    }

    [Fact]
    public async Task Increment_ConcurrentAccessSameSlug_NoLostUpdates()
    {
        const int taskCount = 100;
        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => Task.Run(() => _counter.Increment("concurrent")))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(taskCount, _counter.GetUnflushedCount("concurrent"));
    }
}
