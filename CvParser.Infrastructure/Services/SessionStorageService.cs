namespace CvParser.Infrastructure.Services
{
    public class SessionStorageService : ISessionStorageService
    {
        private readonly Dictionary<string, object?> _sessionStorage = new();

        public T? GetItem<T>(string key)
        {
            if (_sessionStorage.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        public Task SetItem<T>(string key, T value)
        {
            _sessionStorage[key] = value;
            return Task.CompletedTask;
        }
    }
}
