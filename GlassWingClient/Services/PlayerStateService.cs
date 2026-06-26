namespace GlassWingClient.Services;

public class PlayerStateService
{
    public decimal? Currency { get; private set; }
    public event Action? OnChange;

    public void SetCurrency(decimal currency)
    {
        Currency = currency;
        OnChange?.Invoke();
    }
}
