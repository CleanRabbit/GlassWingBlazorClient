namespace GlassWingClient.Services;

// Decouples WelfareBlockDetectionHandler (an HttpClient pipeline handler) from
// WelfareStateService — the handler raises this, WelfareStateService listens, and neither
// depends on the other directly. Avoids a circular DI dependency: the handler is attached
// to GlassWingApiClient's own HttpClient, and WelfareStateService depends on
// GlassWingApiClient, so the handler can't depend on WelfareStateService directly.
public class WelfareBlockSignal
{
    public event Action? Detected;
    public void Raise() => Detected?.Invoke();
}
