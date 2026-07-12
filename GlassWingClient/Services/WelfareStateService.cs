namespace GlassWingClient.Services;

public class WelfareStateService
{
    readonly GlassWingApiClient api;

    public WelfareStatus? Status { get; private set; }
    public event Action? OnChange;

    public bool IsTotalLocked  => Status?.BlockScope == "TotalGameLock";
    public bool IsEventsLocked => Status?.ActiveBlock == "PostTutorialAdoption";

    public WelfareStateService(GlassWingApiClient api, WelfareBlockSignal signal)
    {
        this.api = api;
        signal.Detected += OnWelfareBlockDetected;
    }

    async void OnWelfareBlockDetected() => await RefreshAsync();

    public async Task RefreshAsync()
    {
        var result = await api.GetWelfareStatusAsync();
        if (result is not null)
        {
            Status = result;
            OnChange?.Invoke();
        }
    }

    // Applies a WelfareStatus already carried on another response (e.g. HomeResponse.Welfare)
    // without an extra round trip to /api/welfare/status.
    public void ApplyStatus(WelfareStatus status)
    {
        Status = status;
        OnChange?.Invoke();
    }
}
