namespace CvParser.Infrastructure.Interfaces;

public interface ISessionStorageService
{
    public T? GetItem<T>(string key);
    public Task SetItem<T>(string key, T value);
}
