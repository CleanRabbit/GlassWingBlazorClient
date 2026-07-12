namespace GlassWingClient.Services;

// Singleton — same pattern as PlayerStateService. Drives the nav "Progress" badge dot and
// each hub tile's pending-count badge. HasPendingSeasonalCompletion stays false until its
// HomeResponse field lands on the client (Task 18e client work, though the backend
// HomeResponse already exposes SeasonalEvent) — ApplyHomeSnapshot only reads what the
// client-side HomeResponse model exposes today.
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
}
