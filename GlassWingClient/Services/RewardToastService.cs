namespace GlassWingClient.Services;

public enum RewardToastKind { Achievement, Title, Cosmetic, Challenge, SeasonalEvent, DailyReward }

public record RewardToastItem(
    RewardToastKind Kind,
    string Headline,
    string Detail,
    int? CurrencyAwarded = null,
    string? TitleDisplayText = null,
    string? CosmeticName = null);

// Singleton — registered alongside PlayerStateService in Program.cs. Queues unlock
// notifications for RewardToastHost.razor to render, and centrally dedupes achievement
// unlocks that can arrive via either the Home-load path or the Achievements-page path
// (both can reference the same pending id) using a session-local id set, cleared on logout.
public class RewardToastService
{
    readonly List<RewardToastItem> _items = [];
    readonly HashSet<string> _shownAchievementIds = [];

    public IReadOnlyList<RewardToastItem> Active => _items;
    public event Action? OnChange;

    public void Enqueue(RewardToastItem item)
    {
        _items.Add(item);
        OnChange?.Invoke();
    }

    public void Dismiss(RewardToastItem item)
    {
        _items.Remove(item);
        OnChange?.Invoke();
    }

    // Returns false (no-op) if this achievement id was already toasted this session.
    public bool TryEnqueueAchievement(string id, RewardToastItem item)
    {
        if (!_shownAchievementIds.Add(id)) return false;
        Enqueue(item);
        return true;
    }

    public void ClearShownAchievements() => _shownAchievementIds.Clear();
}
