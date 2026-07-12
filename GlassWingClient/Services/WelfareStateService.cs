namespace GlassWingClient.Services;

public class WelfareStateService(GlassWingApiClient api)
{
    public WelfareStatus? Status { get; private set; }
    public event Action? OnChange;

    public bool IsTotalLocked  => Status?.BlockScope == "TotalGameLock";
    public bool IsEventsLocked => Status?.ActiveBlock == "PostTutorialAdoption";

    public async Task RefreshAsync()
    {
        var result = await api.GetWelfareStatusAsync();
        if (result is not null)
        {
            Status = result;
            OnChange?.Invoke();
        }
    }
}
