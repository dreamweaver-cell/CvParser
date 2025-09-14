using Microsoft.JSInterop;

public class JSRunner
{
    private readonly IJSRuntime _js;
    private bool _isReady = false;
    private readonly Queue<Func<Task>> _pendingCalls = new();

    public JSRunner(IJSRuntime js)
    {
        _js = js;
    }

    public void MarkAsReady()
    {
        _isReady = true;

        // Kör allt som köats innan
        while (_pendingCalls.Count > 0)
        {
            var call = _pendingCalls.Dequeue();
            _ = call(); // kör async utan await (vi vill inte blocka här)
        }
    }

    public Task InvokeVoidAsync(string identifier, params object[] args)
    {
        if (_isReady)
        {
            return _js.InvokeVoidAsync(identifier, args).AsTask();
        }

        // Lägg i kön om vi inte är redo än
        var tcs = new TaskCompletionSource();
        _pendingCalls.Enqueue(async () =>
        {
            await _js.InvokeVoidAsync(identifier, args);
            tcs.SetResult();
        });
        return tcs.Task;
    }
}
