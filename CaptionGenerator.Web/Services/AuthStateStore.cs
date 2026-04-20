using System.Text.Json;
using CaptionGenerator.Web.Models.Auth;
using Microsoft.JSInterop;

namespace CaptionGenerator.Web.Services;

public class AuthStateStore
{
    private const string StorageKey = "captiongenerator.auth.session";
    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _isInitialized;

    public AuthSession? Session { get; private set; }
    public bool IsAuthenticated => Session is not null && Session.ExpiresAtUtc > DateTime.UtcNow;

    public event Action? Changed;

    public AuthStateStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializeLock.WaitAsync();

        try
        {
            if (_isInitialized)
            {
                return;
            }

            var payload = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);

            if (string.IsNullOrWhiteSpace(payload))
            {
                _isInitialized = true;
                return;
            }

            try
            {
                var session = JsonSerializer.Deserialize<AuthSession>(payload);
                if (session is null || session.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    await ClearAsync();
                    _isInitialized = true;
                    return;
                }

                Session = session;
                _isInitialized = true;
                NotifyChanged();
            }
            catch
            {
                await ClearAsync();
                _isInitialized = true;
            }
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task SetSessionAsync(AuthSession session)
    {
        Session = session;
        var payload = JsonSerializer.Serialize(session);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, payload);
        NotifyChanged();
    }

    public async Task ClearAsync()
    {
        Session = null;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        NotifyChanged();
    }

    public string? GetToken() => IsAuthenticated ? Session?.Token : null;

    private void NotifyChanged() => Changed?.Invoke();
}

