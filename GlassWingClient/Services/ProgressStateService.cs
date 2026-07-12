namespace GlassWingClient.Services;

// Singleton — same pattern as PlayerStateService. Drives the nav "Progress" badge dot and
// each hub tile's pending-count badge. HasPendingChallenges/HasPendingSeasonalCompletion
// stay false until their HomeResponse fields land on the client (Task 18d/18e client work,
// though the backend HomeResponse already exposes Challenges/SeasonalEvent) — ApplyHomeSnapshot
// only reads what the client-side HomeResponse model exposes today.
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
        DailyRewardAvailable = home.DailyReward?.Available ?? false;
        OnChange?.Invoke();
    }

    public void ClearAchievements()
    {
        HasPendingAchievements = false;
        OnChange?.Invoke();
    }

    public void ClearDailyReward()
    {
        DailyRewardAvailable = false;
        OnChange?.Invoke();
    }
}
