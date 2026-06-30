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
    HomeCarryCaseInfo[]? CarryCases = null,
    HomeStorageDrawerInfo[]? StorageDrawers = null,
    HomeFoodStorageBinInfo[]? FoodStorageBins = null,
    int? CageSlots = null,
    int? CagesOccupied = null,
    int? TotalAccessorySlots = null,
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

public record HomeCarryCaseInfo(string Id, string TypeId, string? RatId, int AnchorIndex = 0);
public record HomeStorageDrawerInfo(string Id, string TypeId, int Capacity, int SlotsAvailable, HomeStorageDrawerItem[] Items, int AnchorIndex = 0);
public record HomeStorageDrawerItem(string Id, string TypeId, string Kind);
public record HomeFoodStorageBinInfo(string Id, string TypeId, double StoredRatDays, int CapacityRatDays, int AnchorIndex = 0);
public record AutoFillNotification(string CageId, string CageName, bool FoodRefilled, bool WaterRefilled);

// --- Events ---

public record TutorialEventResponse(TutorialEventResult? Event, decimal NewCurrencyBalance);
public record TutorialEventResult(string EventName, string EventType, DateTime CompletedAt, TutorialEventEntry[] Entries, int CurrencyAwarded);
public record TutorialEventEntry(string ParticipantName, bool IsPlayer, int Score, int Placement);

public record LobbyResponse(string Id, string EventDefinitionId, string EventName, string EventType, DateTime ScheduledRunAt, string Status, int EntrantsCount, int PlayerSlots);
public record LobbyResultEntryResponse(string? PlayerId, string EntrantLabel, bool IsNpc, int Score, int Placement, int CurrencyAwarded);

public record PlayerEventsResponse(PlayerActiveEntry[] ActiveEntries, PlayerRecentResult[] RecentResults);
public record PlayerActiveEntry(string LobbyId, string EventDefinitionId, string EventName, string EventType, DateTime ScheduledRunAt, int EntrantsCount, int PlayerSlots, string RatId);
public record PlayerRecentResult(string LobbyId, string EventDefinitionId, string EventName, string EventType, DateTime CompletedAt, string RatId, int Placement, int EntrantsCount, int CurrencyAwarded, int Score);

// --- Shop ---

public record ShopCatalogueResponse(
    ShopCageType[] Cages,
    ShopAccessoryType[] Accessories,
    ShopFoodBowlType[] FoodBowls,
    ShopWaterBottleType[] WaterBottles,
    ShopFoodStorageBinType[] FoodStorageBins,
    ShopFoodType[] Foods,
    ShopCarryCaseType[]? CarryCases = null,
    ShopStorageDrawersType[]? StorageDrawers = null);

public record ShopCageType(string Id, string Brand, string ModelName, string? Tier, int WidthCm, int DepthCm, int HeightCm, int MaxCapacity, int MaxFoodBowlSlots, int MaxWaterBottleSlots, int Price);
public record ShopAccessoryType(string Id, string Name, string? Description, int BaseEnrichment, string? MinimumTier, int Price);
public record ShopFoodBowlType(string Id, string Name, int CapacityRatDays, string? MinimumTier, int Price);
public record ShopWaterBottleType(string Id, string Name, int CapacityRatDays, string? MinimumTier, int Price);
public record ShopFoodStorageBinType(string Id, string Name, int CapacityRatDays, int Price);
public record ShopFoodType(string Id, string Name, int QualityTier, int PricePerRatDay, double? HealthBonus, TrainingBonus? TrainingBonus);
public record ShopCarryCaseType(string Id, string Name, int Price);
public record ShopStorageDrawersType(string Id, string Name, int SlotsPerUnit, int Price);

public record CagePurchaseResponse(string CageId, string CageName, string CageTypeId, decimal NewBalance);
public record InventoryPurchaseResponse(string InventoryItemId, string TypeId, string Name, decimal NewBalance);
public record FoodStorageBinPurchaseResponse(string BinId, int CapacityRatDays, decimal NewBalance, int AnchorIndex = 0);
public record CarryCasePurchaseResponse(string CarryCaseId, int AnchorIndex, decimal NewBalance);
public record StorageDrawersPurchaseResponse(string DrawersId, int AnchorIndex, int Capacity, decimal NewBalance);
public record FoodPurchaseResponse(string BinId, string FoodTypeId, int StoredRatDays, decimal NewBalance);

// --- Inventory ---

public record InventoryResponse(InventoryItem[] Items);
public record InventoryItem(string Id, string ItemType, string TypeId, string Name, string? Description, int Quantity);
public record PlaceInventoryItemResponse(bool Success, string? Message);

// --- Player ---

public record PlayerProfileResponse(string PlayerId, string Username, decimal Currency);
