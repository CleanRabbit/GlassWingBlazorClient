using System.Text.Json;

namespace GlassWingClient.Services;

public record AuthResponse(string Token, string PlayerId, string Username);

public record RatResponse(
    string Id,
    string Name,
    string OwnerId,
    TrainingFitness? Fitness,
    JsonElement? HealthState,
    string[]? TricksLearned);

public record TrainingFitness(StatFitness? Sprint, StatFitness? Agility, StatFitness? Endurance);
public record StatFitness(double Score, int TrainingCount);

public record HomeResponse(string Id, string OwnerId, string Name, CageResponse[] Cages);

public record CageResponse(
    string Id,
    string Name,
    int FoodLevel,
    int WaterLevel,
    string? Food,
    string? Regime,
    RatSummary[] Rats);

public record RatSummary(string Id, string Name);

public record TutorialEventResponse(JsonElement? Event, decimal NewCurrencyBalance);
