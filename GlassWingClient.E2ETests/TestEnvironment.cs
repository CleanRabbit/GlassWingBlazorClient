namespace GlassWingClient.E2ETests;

// Every test in this suite drives the "Testing" launch profile pair (client on 5011, API on 5223,
// API pointed at the `glasswing_test` Mongo database) — never the interactive dev pair on
// 5001/5123 — so a run never pollutes, and is never polluted by, manually-curated dev state.
// See run-e2e-tests.ps1 (repo root) for the drop-database/start/bootstrap/stop lifecycle a real
// test run needs around it; this constant is the one place that lifecycle and the tests agree on
// which environment they're both talking about.
public static class TestEnvironment
{
    public const string BaseUrl = "http://localhost:5011";
}
