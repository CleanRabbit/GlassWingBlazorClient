using System.Text.Json;

namespace GlassWingClient.Services;

public record AuthResponse(string Token, string PlayerId, string Username);

public record RatResponse(
    string Id,
    string Name,
    string OwnerId,
    TrainingFitness? Fitness,
    HealthState? HealthState,
    RatPhenotype? Phenotype,
    string[]? TricksLearned);

public record TrainingFitness(StatFitness? Sprint, StatFitness? Agility, StatFitness? Endurance);
public record StatFitness(double Score, int TrainingCount);

// --- Health ---

public record HealthState(
    string? Vitality,
    double WeightGrams,
    double BodyLengthCm,
    ActiveIllness[]? ActiveIllnesses);

public record ActiveIllness(
    string IllnessId,
    DateTime StartedAt,
    bool TreatmentApplied,
    DateTime? TreatedAt);

// --- Phenotype (appearance) ---

public record RatPhenotype(CoatPhenotype? Coat, MorphologyProfile? Morphology);

public record CoatPhenotype(
    string? BaseColor,
    string? PointColor,
    string? Pattern,
    string? HoodQuality,
    string? Type,
    string? EyeColor,
    string? EarType,
    bool HasBlaze,
    bool IsRoan,
    string? Silvering,
    bool IsDownunder,
    bool IsDownunderHomozygous);

public record MorphologyProfile(string? Sex, int BodySize);

// --- Home ---

public record HomeResponse(
    string Id,
    string OwnerId,
    string Name,
    CageResponse[] Cages,
    HomeFoodStorageBinInfo[]? FoodStorageBins = null,
    int? CageSlots = null,
    int? CagesOccupied = null,
    AutoFillNotification[]? AutoFills = null);

public record CageResponse(
    string Id,
    string Name,
    CageTypeInfo? Type,
    CageFoodInfo? Food,
    CageRegimeInfo? Regime,
    TrainingBonus? TrainingEfficacy,
    int FoodLevel,
    int WaterLevel,
    InstalledBowlInfo[]? FoodBowls,
    InstalledBottleInfo[]? WaterBottles,
    InstalledAccessoryInfo[]? Accessories,
    RatSummary[] Rats);

public record CageTypeInfo(
    string Id,
    string Brand,
    string ModelName,
    string? Tier,
    int WidthCm,
    int DepthCm,
    int HeightCm,
    int MaxCapacity,
    int MaxFoodBowlSlots,
    int MaxWaterBottleSlots);

public record CageFoodInfo(string Id, string Name);
public record CageRegimeInfo(string Id, string Name);
public record TrainingBonus(double Sprint, double Agility, double Endurance);

public record InstalledBowlInfo(string Id, string Name, int CapacityRatDays);
public record InstalledBottleInfo(string Id, string Name, int CapacityRatDays);
public record InstalledAccessoryInfo(string Id, string Name, string? Description);

public record RatSummary(string Id, string Name);

// --- Game ---

public record GameSettingsResponse(
    double BiologicalScale,
    double FoodConsumptionScale,
    double WaterConsumptionScale,
    double TrainingCooldownScale,
    double IllnessProgressionScale);

// --- Home extras ---

public record HomeFoodStorageBinInfo(string Id, string Name, string? FoodTypeId, string? FoodTypeName, int CapacityRatDays, int StoredRatDays);
public record AutoFillNotification(string CageId, string CageName, string Type);

// --- Events ---

public record TutorialEventResponse(JsonElement? Event, decimal NewCurrencyBalance);

// --- Shop ---

public record ShopCatalogueResponse(
    ShopCageType[] Cages,
    ShopAccessoryType[] Accessories,
    ShopFoodBowlType[] FoodBowls,
    ShopWaterBottleType[] WaterBottles,
    ShopFoodStorageBinType[] FoodStorageBins,
    ShopFoodType[] Foods);

public record ShopCageType(string Id, string Brand, string ModelName, string? Tier, int WidthCm, int DepthCm, int HeightCm, int MaxCapacity, int MaxFoodBowlSlots, int MaxWaterBottleSlots, int Price);
public record ShopAccessoryType(string Id, string Name, string? Description, int BaseEnrichment, string? MinimumTier, int Price);
public record ShopFoodBowlType(string Id, string Name, int CapacityRatDays, string? MinimumTier, int Price);
public record ShopWaterBottleType(string Id, string Name, int CapacityRatDays, string? MinimumTier, int Price);
public record ShopFoodStorageBinType(string Id, string Name, int CapacityRatDays, int Price);
public record ShopFoodType(string Id, string Name, int QualityTier, int PricePerRatDay, double? HealthBonus, TrainingBonus? TrainingBonus);

public record CagePurchaseResponse(string CageId, string CageName, string CageTypeId, decimal NewBalance);
public record InventoryPurchaseResponse(string InventoryItemId, string TypeId, string Name, decimal NewBalance);
public record FoodStorageBinPurchaseResponse(string BinId, int CapacityRatDays, decimal NewBalance);
public record FoodPurchaseResponse(string BinId, string FoodTypeId, int StoredRatDays, decimal NewBalance);

// --- Inventory ---

public record InventoryResponse(InventoryItem[] Items);
public record InventoryItem(string Id, string ItemType, string TypeId, string Name, string? Description, int Quantity);
public record PlaceInventoryItemResponse(bool Success, string? Message);

// --- Player ---

public record PlayerProfileResponse(string PlayerId, string Username, decimal Currency);
