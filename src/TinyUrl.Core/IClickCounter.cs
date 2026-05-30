namespace TinyUrl.Core;

public interface IClickCounter
{
    void Increment(string slug);
    int GetUnflushedCount(string slug);
    Dictionary<string, int> DrainAll();
}
