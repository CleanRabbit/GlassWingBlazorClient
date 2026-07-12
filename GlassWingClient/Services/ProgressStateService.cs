namespace GlassWingClient.Services;

// Singleton — same pattern as PlayerStateService. Drives the nav "Progress" badge dot and
// each hub tile's pending-count badge.
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
        HasPendingChallenges = home.Challenges?.HasCompletions ?? false;
        HasPendingSeasonalCompletion = home.SeasonalEvent?.HasPendingCompletions ?? false;
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

    public void ClearChallenges()
    {
        HasPendingChallenges = false;
        OnChange?.Invoke();
    }

    public void ClearSeasonalCompletion()
    {
        HasPendingSeasonalCompletion = false;
        OnChange?.Invoke();
    }
}
