namespace TinyUrl.Core;

public class StatsUseCase
{
    private readonly UrlRepository _repository;
    private readonly IClickCounter _clickCounter;

    public StatsUseCase(UrlRepository repository, IClickCounter clickCounter)
    {
        _repository = repository;
        _clickCounter = clickCounter;
    }

    public async Task<int?> GetClickCountAsync(string slug)
    {
        var dbCount = await _repository.GetClickCountAsync(slug);
        if (dbCount is null)
            return null;

        return dbCount.Value + _clickCounter.GetUnflushedCount(slug);
    }
}
