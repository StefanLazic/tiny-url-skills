namespace TinyUrl.Core;

public class StatsUseCase
{
    private readonly UrlRepository _repository;

    public StatsUseCase(UrlRepository repository)
    {
        _repository = repository;
    }

    public async Task<int?> GetClickCountAsync(string slug)
    {
        return await _repository.GetClickCountAsync(slug);
    }
}
