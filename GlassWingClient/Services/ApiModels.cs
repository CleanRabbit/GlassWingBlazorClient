namespace GlassWingClient.Services;

public record AuthResponse(string Token, string PlayerId, string Username);

public record RatResponse(
    string Id,
    string Name,
    string OwnerId,
    HealthState? HealthState,
    RatPhenotype? Phenotype,
    string[]? TricksLearned,
    bool IsRetired = false,
    DateTime? RetiredAt = null,
    string? RetirementReason = null,
    DateTime RetiresAt = default,
    string Sex = "Female",
    string LifeStage = "Adult",
    int LitterCount = 0,
    bool IsPregnant = false,
    DateTime? DueAt = null,
    string? MotherId = null,
    string? FatherId = null,
    double SprintAbility = 1,
    double AgilityAbility = 1,
    double EnduranceAbility = 1,
    int SprintPotential = 100,
    int AgilityPotential = 100,
    int EndurancePotential = 100,
    bool IsNursing = false,
    bool IsProtective = false,
    DateTime DateOfBirth = default);

public record LifeStageNotification(string RatId, string RatName, string PreviousStage, string NewStage, string Message);

public record PregnancyResponse(
    string FatherId,
    int GestationRatDays,
    DateTime DueAt,
    double ConceptionWellnessScore);

// Shared 409 conflict body shape — { error, reason } — used by retire, breed, and
// future train/sex-separation endpoints.
public record ApiErrorResponse(string? Error, string? Reason);

public record ApiProblemDetails(string? Title, string? Detail);

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
    AutoFillNotification[]? AutoFills = null,
    LifeStageNotification[]? LifeStageNotifications = null,
    NewAchievementNotice[]? NewAchievements = null,
    AchievementsHomeSummary? Achievements = null);

// ── Achievements (Task 18a) ────────────────────────────────────────────────────

public record AchievementsResponse(AchievementCategoryGroup[] Categories, AchievementsSummary Summary);
public record AchievementCategoryGroup(string Category, AchievementEntry[] Achievements);
public record AchievementEntry(
    string Id, string Name, string Description,
    DateTime? CompletedAt, int Progress, int Threshold,
    AchievementRewardInfo Reward);
public record AchievementRewardInfo(int? Currency, string? TitleId, string? CosmeticId);
public record AchievementsSummary(int Total, int Completed, string[] PendingUnlocks);
public record NewAchievementNotice(string Id, string Name, AchievementRewardInfo Reward);
public record AchievementsHomeSummary(bool HasPendingUnlocks);

// ── Titles (Task 18b) ───────────────────────────────────────────────────────────

public record TitlesResponse(string? ActiveTitleId, TitleEntry[] Titles);
public record TitleEntry(string Id, string DisplayText, string Description, string UnlockSource, bool Unlocked);

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
public record TrainingRegimeResponse(string Id, string Name, string Description, TrainingBonus Training);

public record InstalledBowlInfo(string Id, string Name, int CapacityRatDays);
public record InstalledBottleInfo(string Id, string Name, int CapacityRatDays);
public record InstalledAccessoryInfo(string Id, string Name, string? Description);

public record RatSummary(string Id, string Name, string LifeStage = "Adult", bool IsNursing = false, bool IsProtective = false);

// --- Game ---

public record GameSettingsResponse(
    double BiologicalScale,
    double FoodConsumptionScale,
    double WaterConsumptionScale,
    double TrainingCooldownScale,
    double IllnessProgressionScale,
    decimal? MarketplaceListingFee = null,
    double? MarketplaceTransactionFeePercent = null,
    double? RatLifespanDays = null,
    double? CriticalHealthRetirementThresholdDays = null,
    double? RetirementWarningEarlyDays = null,
    double? RetirementWarningLateDays = null);

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
public record ShopFoodType(string Id, string Name, int QualityTier, int InGamePrice, double? HealthBonus);
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

// --- Marketplace ---

public record MarketplaceListingResponse(
    string ListingId,
    string RatId,
    string RatName,
    int AgeMonths,
    string SellerId,
    string SellerUsername,
    decimal Price,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    MarketplaceListingStats? Stats,
    CoatPhenotype? Appearance);

public record MarketplaceListingStats(double? Sprint, double? Agility, double? Endurance);
public record CreateListingResponse(string ListingId, DateTime ExpiresAt, decimal NewBalance);
public record BuyListingResponse(string CarryCaseId, decimal NewBalance);

// --- Player ---

public record PlayerProfileResponse(
    string PlayerId,
    string Username,
    decimal Currency,
    string? HomeName = null,
    string? Country = null,
    string? State = null,
    bool WeatherEnabled = false,
    string? ActiveTitleId = null,
    string? ActiveTitleText = null);
