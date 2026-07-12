namespace GlassWingClient.Services;

// Singleton — same pattern as PlayerStateService. Drives the nav "Progress" badge dot and
// each hub tile's pending-count badge. HasPendingChallenges/HasPendingSeasonalCompletion/
// DailyRewardAvailable stay false until their HomeResponse fields land (Task 18d/18e/18c
// client work) — ApplyHomeSnapshot only reads what HomeResponse exposes today.
public class ProgressStateService
{
    public bool HasPendingAchievements { get; private set; }
    public bool HasPendingChallenges { get; private set; }
    public bool HasPendingSeasonalCompletion { get; private set; }
    public bool DailyRewardAvailable { get; private set; }

    public bool HasAnyPending => HasPendingAchievements || HasPendingChallenges
                                  || HasPendingSeasonalCompletion || DailyRewardAvailable;

    public event Action? OnChange;

    public void ApplyHomeSnapshot(HomeResponse home)
    {
        HasPendingAchievements = home.Achievements?.HasPendingUnlocks ?? false;
        OnChange?.Invoke();
    }

    public void ClearAchievements()
    {
        HasPendingAchievements = false;
        OnChange?.Invoke();
    }
}
