using System.Text.Json;

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
    // Top-level on RatResponse (RatsModule.cs), not nested under HealthState — this was missing
    // entirely, so the real value was silently dropped on deserialization.
    double WeightGrams = 0,
    bool IsNursing = false,
    bool IsProtective = false,
    DateTime DateOfBirth = default,
    string? ActiveCosmeticId = null,
    TrickTrainingStateDto? CurrentTrickTraining = null,
    BondingInfo? Bonding = null,
    PlaySessionInfo? PlaySession = null,
    OtcProtectionInfo[]? OtcProtections = null,
    OtcMedicationCooldownInfo[]? OtcMedicationCooldowns = null,
    // Set once vitality first becomes Critical; cleared on recovery. Wasn't previously exposed
    // client-side — Task 32's Admin/RatDetail.razor is the first consumer that needs to show it
    // (making it visible exactly why Clear Lock deliberately never touches this field).
    DateTime? CriticalSince = null,
    // Secret rat easter egg (Task 23) — present only on the rename response that freshly
    // claims a transformation; absent (not null) from the JSON on every other response, so its
    // mere presence can't be used to fingerprint claimed rats via schema inspection.
    string? SecretMessage = null,
    // Personality traits (Tasks 24-26) — flat collection, e.g. ["Fussy", "Bold", "Playful"].
    string[]? Traits = null,
    // Ancestry (Task 13) — empty ({father: null, mother: null}) for starter/agency-adopted rats.
    Ancestry? Ancestry = null);

// GET /api/rats — lightweight roster projection (Task 30 §4). Scoped to what Rats.razor's
// table renders (name/sex/current abilities, IsRetired/IsPregnant) plus IsNursing, needed by
// this same endpoint's other callers (Events.razor's event-entry eligibility filter,
// RatDetail.razor's mate picker, Adoption.razor's surrender list). No phenotype, no event
// scorecard — fetch GetRatAsync(id) for full detail.
public record RatRosterResponse(
    string Id,
    string Name,
    string Sex,
    string LifeStage,
    bool IsRetired,
    bool IsPregnant,
    bool IsNursing,
    double SprintAbility,
    double AgilityAbility,
    double EnduranceAbility);

// --- Ancestry (Task 13) ---

public record Ancestry(AncestorNode? Father, AncestorNode? Mother);

// Lightweight (IsEnriched == false) until the ancestor is sold/retired/surrendered, at which
// point every field below is a permanent, self-contained snapshot. Recursive, bounded to 4
// generations total from the viewed rat.
public record AncestorNode(
    string RatId,
    string Name,
    string Sex,
    bool IsEnriched,
    DateTime? DateOfBirth,
    DateTime? RetiredAt,
    string? RetirementReason,
    double? SprintAbility,
    double? AgilityAbility,
    double? EnduranceAbility,
    int? SprintPotential,
    int? AgilityPotential,
    int? EndurancePotential,
    CoatSnapshot? Coat,
    AncestorNode? Father,
    AncestorNode? Mother);

// Display-relevant coat fields only, snapshotted at enrichment time. Field is Silvering (not
// SilveringIntensity) — matches the live CoatPhenotype.Silvering name exactly, confirmed
// against the real backend Ancestry.cs rather than the design doc's own open question about it.
public record CoatSnapshot(
    string Colour,
    string Pattern,
    string? HoodQuality,
    string Silvering,
    bool HasBlaze,
    bool IsRoan,
    bool IsDownunder);

// GET /api/rats/{id}/public — any authenticated player may view any rat's public projection.
// Deliberately a separate, honestly-typed record (not RatResponse with fields nulled out) so
// RatDetail.razor can tell "hidden because not owned" apart from "genuinely empty."
public record PublicRatResponse(
    string Id,
    string Name,
    string OwnerId,
    DateTime DateOfBirth,
    DateTime CreatedAt,
    string? FatherId,
    string? MotherId,
    int Generation,
    string Sex,
    string LifeStage,
    RatPhenotype Phenotype,
    double SprintAbility,
    double AgilityAbility,
    double EnduranceAbility,
    int SprintPotential,
    int AgilityPotential,
    int EndurancePotential,
    string[] Traits,
    string[] TricksLearned,
    DateTime RetiresAt,
    bool IsRetired,
    DateTime? RetiredAt,
    string? RetirementReason,
    Ancestry? Ancestry,
    string? ActiveCosmeticId);

// --- Tricks (Task 19) ---

public record TrickCatalogueResponse(TrickCategoryGroup[] Categories);
public record TrickCategoryGroup(string Category, TrickDefinition[] Tricks);
public record TrickDefinition(
    string Id, string Name, int Tier, double BaseScore, int AptitudeThreshold,
    TrickRatStatus[] Rats);

// Status: Learned | InTraining | SocialLearning | Locked | NotStarted
public record TrickRatStatus(string RatId, string RatName, string Status, double Progress, int Aptitude);

public record TrickTrainingStateDto(string TrickId, double Progress, DateTime StartedAt);
public record BondingInfo(double CurrentLevel, double Capacity);

// --- Play Sessions (Task 20) ---

public record PlaySessionInfo(
    string? CurrentTrickId, double? TrickProgress, double DailyProgressRemaining,
    DateTime? LastPlaySessionAt, bool SolitudeActive);

public record PlaySessionAchievementInfo(string Id, string Name, int? Currency, string? TitleId, string? CosmeticId);

public record PlaySessionResponse(
    string TrickId, double ProgressBefore, double ProgressAfter, double ProgressGained,
    bool CappedByDailyLimit, bool TrickLearned, double BondingBefore, double BondingAfter,
    PlaySessionAchievementInfo[] NewAchievements);

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
    double BodyLengthCm,
    ActiveIllness[]? ActiveIllnesses);

// Diagnosis-gated (Task 21): IllnessId/Name/Category are null until IsDiagnosed flips true.
// OtcCureProgress/OtcCriticalBufferHours (Task 22) are independent of vet state and populated
// regardless of diagnosis.
public record ActiveIllness(
    bool IsDiagnosed,
    string? IllnessId,
    string? Name,
    string? Category,
    DateTime StartedAt,
    bool TreatmentApplied,
    string? TreatmentType = null,
    int? DosesAdministered = null,
    int? DoseCount = null,
    bool? CanDoseToday = null,
    double OtcCureProgress = 0,
    double OtcCriticalBufferHours = 0,
    double? TreatmentRecoveryHours = null);

// --- Vet Care (Task 21) ---

public record VetVisitResponse(VetDiagnosisEntry[] Diagnoses, decimal NewBalance);
public record VetDiagnosisEntry(
    string IllnessId, string Name, string Category, DateTime StartedAt,
    string TreatmentType, int TreatmentCost, int? DoseCount, string? Recommendation,
    double? TreatmentRecoveryHours);

// --- OTC Medication (Task 22) ---

public record OtcProtectionInfo(string Category, DateTime ExpiresAt, double Factor);
public record OtcMedicationCooldownInfo(string MedicationId, DateTime AvailableAt);

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
    bool IsDownunderHomozygous,
    // Task 27
    string? Pearl = null,
    bool HasWhiteFeet = false,
    bool IsHuskyCarrier = false,
    bool IsBandedHusky = false);

public record MorphologyProfile(string? Sex, int BodySize);

// --- Home ---

public record HomeResponse(
    string Id,
    string OwnerId,
    string Name,
    CageResponse?[] Cages,
    HomeCarryCaseInfo[]? CarryCases = null,
    HomeStorageDrawerInfo[]? StorageDrawers = null,
    HomeFoodStorageBinInfo[]? FoodStorageBins = null,
    int? CageSlots = null,
    string? MaxCageTier = null,
    int? CagesOccupied = null,
    int? TotalAccessorySlots = null,
    int? AccessoriesOccupied = null,
    int[]? EmptyAccessorySlotIndices = null,
    AutoFillNotification[]? AutoFills = null,
    LifeStageNotification[]? LifeStageNotifications = null,
    NewAchievementNotice[]? NewAchievements = null,
    AchievementsHomeSummary? Achievements = null,
    DailyRewardInfo? DailyReward = null,
    ChallengesSummaryInfo? Challenges = null,
    SeasonalEventSummaryInfo? SeasonalEvent = null,
    string[]? VetTreatmentNotifications = null,
    HomeMedicationDeviceInfo[]? MedicationDevices = null,
    string[]? MischiefNotifications = null,
    HomeWeatherAccessoryInfo[]? WeatherAccessories = null,
    WeatherInfo? Weather = null,
    WelfareStatus? Welfare = null);

// ── Estate Agency (Task 37) ──────────────────────────────────────────────────

public record HomeTierInfo(
    string Id, string Name, string Tier, int CageSlots, string MaxCageTier,
    int HomeAccessorySlots, int InGamePrice, bool IsCurrent, bool Fits);

public record HomeUpgradeResponse(string HomeTypeId, string HomeTypeName, int NewBalance);

// --- Vet/OTC home extras ---

public record HomeMedicationDeviceInfo(string Id, string TypeId, int AnchorIndex, double Cleanliness, double Condition);

// --- Weather (Task 16) ---

public record HomeWeatherAccessoryInfo(string Id, string TypeId, int AnchorIndex, double Cleanliness, double Condition);

public record WeatherConditionsInfo(bool TooWarm, bool TooCold, bool TooHumid, bool TooDry);
public record WeatherAccessoriesSummary(bool HasAirConditioning, bool HasRadiator, bool HasDehumidifier, bool HasHumidifier);

public record WeatherInfo(
    double? TemperatureCelsius,
    double? RelativeHumidityPercent,
    DateTime? UpdatedAt,
    bool IsEnabled,
    WeatherConditionsInfo Conditions,
    WeatherAccessoriesSummary Accessories);

// ── Achievements (Task 18a) ────────────────────────────────────────────────────

// Progress/CriteriaType/CriteriaThreshold are raw catalogue/state fields — Threshold/display-
// progress/Owned-style booleans are no longer pre-computed server-side (Task 29-30 §2). Progress
// is a plain running count, except for AchievementCatalogueClient.AllEventTypesId ("all-event-types")
// where it's a 3-bit mask the client PopCounts (see ProgressAchievements.razor). NewBalance lets
// the reward-toast flow patch currency directly instead of a follow-up GetPlayerProfileAsync().
public record AchievementsResponse(AchievementCategoryGroup[] Categories, AchievementsSummary Summary, decimal NewBalance = 0);
public record AchievementCategoryGroup(string Category, AchievementEntry[] Achievements);
public record AchievementEntry(
    string Id, string Name, string Description,
    DateTime? CompletedAt, int Progress,
    string CriteriaType, int? CriteriaThreshold,
    AchievementRewardInfo Reward);
public record AchievementRewardInfo(int? Currency, string? TitleId, string? CosmeticId);
public record AchievementsSummary(int Total, int Completed, string[] PendingUnlocks);
public record NewAchievementNotice(string Id, string Name, AchievementRewardInfo Reward);
public record AchievementsHomeSummary(bool HasPendingUnlocks);

// ── Titles (Task 18b) ───────────────────────────────────────────────────────────

// UnlockedTitleIds is the player's raw owned-title-id set — TitleEntry no longer carries a
// pre-computed Unlocked bool; callers derive it via UnlockedTitleIds.Contains(t.Id) (Task 29-30 §2).
public record TitlesResponse(string? ActiveTitleId, TitleEntry[] Titles, string[] UnlockedTitleIds);
public record TitleEntry(string Id, string DisplayText, string Description, string UnlockSource);

// ── Daily Rewards (Task 18c) ────────────────────────────────────────────────────

// Type: "Currency" | "AdoptionFeeWaiver" | "EventEntryWaiver" | "Cosmetic". CosmeticId is
// only populated on Cosmetic-type entries from the Calendar/Claim endpoints — the Home
// summary's nested reward never includes it, and a missing JSON field just defaults to null.
public record DailyRewardEntry(string Type, int? Amount, string? CosmeticId = null);
public record DailyRewardInfo(bool Available, int Day, DateTime? NextAvailableAt, DailyRewardEntry Reward);
public record ClaimDailyRewardResponse(int Day, DailyRewardEntry Reward, int NextDay, DailyRewardEntry NextReward, bool CalendarWrapped);
public record DailyRewardCalendarResponse(int CalendarLength, int CurrentDay, bool ClaimedToday, DailyRewardCalendarEntry[] Entries);
public record DailyRewardCalendarEntry(int Day, DailyRewardEntry Reward, bool Claimed);

// ── Challenges (Task 18d) ───────────────────────────────────────────────────────

public record ChallengeWeekResponse(
    int WeekNumber, DateTime WeekStart, DateTime WeekEnd,
    ChallengeEntry[] Challenges, string[] PendingCompletions, ChallengeWeekSummary Summary,
    decimal NewBalance = 0);

public record ChallengeEntry(
    string Id, string Name, string Description, string Difficulty, string Category,
    int Progress, int Threshold, DateTime? CompletedAt, ChallengeRewardInfo Reward);

public record ChallengeRewardInfo(int Currency);
public record ChallengeWeekSummary(int Completed, int Total, int TotalRewardAvailable);

public record ChallengesSummaryInfo(DateTime WeekEnd, int Completed, int Total, bool HasCompletions);

// ── Seasonal Events (Task 18e) ───────────────────────────────────────────────────

// SeasonalChallengeEntry mirrors ChallengeEntry (Task 18d) minus Category — the real
// backend response (SeasonalEventModule.cs) never included one, unlike the design doc's
// "reused verbatim" assumption.
public record SeasonalChallengeEntry(
    string Id, string Name, string Description, string Difficulty,
    int Progress, int Threshold, DateTime? CompletedAt, ChallengeRewardInfo Reward);

public record SeasonalCompletionRewardInfo(int Currency, string[] CosmeticIds);

public record ActiveSeasonalEvent(
    string Id, string Name, string Theme, DateTime StartDate, DateTime EndDate, int DaysRemaining,
    SeasonalCompletionRewardInfo CompletionReward, string? TitleId, string? TitleDisplayText,
    SeasonalChallengeEntry[] Challenges, bool AllChallengesCompleted, string[] PendingCompletions);

public record UpcomingSeasonalEvent(string Id, string Name, DateTime StartDate, DateTime EndDate);
public record SeasonalEventResponse(ActiveSeasonalEvent? Active, UpcomingSeasonalEvent[] Upcoming, decimal NewBalance = 0);

public record SeasonalEventSummaryInfo(
    bool Active, string Name, DateTime EndsAt, int DaysRemaining,
    int ChallengesCompleted, int ChallengesTotal, bool HasPendingCompletions);

// ── Cosmetics (Task 18f) ─────────────────────────────────────────────────────────

// EquippedOn is always [] from the real API (lazy evaluation) — read ActiveCosmeticId off
// the relevant CageResponse/RatResponse instead, per the backend's own recommendation.
// CosmeticEntry no longer carries a pre-computed Owned bool — callers derive it via
// CosmeticsResponse.UnlockedCosmeticIds.Contains(c.Id) (Task 29-30 §2).
public record CosmeticEntry(
    string Id, string Name, string Description, string Rarity, string Availability,
    int? ShopPrice, string? GrantSource, string[] EquippedOn);

public record CosmeticsResponse(CosmeticEntry[] CageDecorations, CosmeticEntry[] RatAccessories, string[] UnlockedCosmeticIds);
public record BuyCosmeticResponse(int Currency, CosmeticEntry Cosmetic);

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
    RatSummary[] Rats,
    string? ActiveCosmeticId = null,
    double Cleanliness = 100);

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

public record InstalledBowlInfo(string Id, string Name, int CapacityRatDays, double Cleanliness = 100, double Condition = 100);
public record InstalledBottleInfo(string Id, string Name, int CapacityRatDays, double Cleanliness = 100, double Condition = 100);
public record InstalledAccessoryInfo(string Id, string Name, string? Description, double Cleanliness = 100, double Condition = 100);

public record RenameHomeResponse(string Name);

// Task 29 §5 — POST /api/home/cages/{cageId}/train. ExcludedRatIds maps ratId -> exclusion
// reason ("Infant"/"Nursing"/"Protective"/"Retired"/"Weening"), mirroring the server's own
// GetTrainingExclusionReason so the client can show why a rat sat out without duplicating
// the eligibility logic itself.
public record StartTrainingSessionResponse(
    DateTime SessionUntil, string[] RatIds, TrainingBonus Efficacy, Dictionary<string, string> ExcludedRatIds);

public record RatSummary(
    string Id, string Name, string LifeStage = "Adult", bool IsNursing = false, bool IsProtective = false,
    PlaySessionInfo? PlaySession = null);

// --- Game ---

public record GameSettingsResponse(
    double BiologicalScale,
    double FoodConsumptionScale,
    double HuskyOnsetAgeMonths,
    double WaterConsumptionScale,
    double TrainingCooldownScale,
    double IllnessProgressionScale,
    decimal? MarketplaceListingFee = null,
    double? MarketplaceTransactionFeePercent = null,
    double? RatLifespanDays = null,
    double? CriticalHealthRetirementThresholdDays = null,
    double? RetirementWarningEarlyDays = null,
    double? RetirementWarningLateDays = null,
    int? TrickMaxRoutineSize = null,
    int? SocialLearningAptitudeThreshold = null,
    int? MaxPlaySessionSeconds = null,
    double? MaxActiveProgressPerRatPerDay = null,
    int? VetDiagnosisFee = null,
    int? AdoptionFee = null,
    int? MaxAdoptionSurrenders = null,
    int? AdoptionPoolMaxRandomCount = null,
    double? WeatherTooWarmCelsius = null,
    double? WeatherTooColdCelsius = null,
    double? WeatherTooHumidPercent = null,
    double? WeatherTooDryPercent = null,
    int? LeaderboardSeasonDurationDays = null,
    int? LeaderboardAverageMinEntries = null,
    LeaderboardSeasonInfo? CurrentLeaderboardSeason = null,
    Dictionary<string, string[]>? LeaderboardMetricsByEventType = null);

public record LeaderboardSeasonInfo(int SeasonNumber, DateTime Start, DateTime End);

// --- Home extras ---

public record HomeCarryCaseInfo(string Id, string TypeId, string? RatId, int AnchorIndex = 0, double Cleanliness = 100, double Condition = 100);
public record HomeStorageDrawerInfo(string Id, string TypeId, int Capacity, int SlotsAvailable, HomeStorageDrawerItem[] Items, int AnchorIndex = 0, double Cleanliness = 100, double Condition = 100);
public record HomeStorageDrawerItem(string Id, string TypeId, string Kind, double Cleanliness = 100, double Condition = 100, DateTime? CleaningEndsAt = null, bool IsCleaning = false);
public record HomeFoodStorageBinInfo(string Id, string TypeId, double StoredRatDays, int CapacityRatDays, int AnchorIndex = 0, double Cleanliness = 100, double Condition = 100);
public record AutoFillNotification(string CageId, string CageName, bool FoodRefilled, bool WaterRefilled);

// --- Events ---

// --- Leaderboards (Task 17) ---

public record LeaderboardResponse(
    string EventType, string Metric, string Window,
    DateTime WindowStart, DateTime WindowEnd, int? SeasonNumber,
    LeaderboardEntryResponse[] Entries, int TotalEntries, DateTime CachedAt);

public record LeaderboardEntryResponse(
    int Rank, string RatId, string RatName, string OwnerPlayerId, string OwnerUsername,
    string? OwnerTitle, double Score, int EntryCount);

public record TutorialEventResponse(TutorialEventResult? Event, decimal NewCurrencyBalance);
public record TutorialEventResult(string EventName, string EventType, DateTime CompletedAt, TutorialEventEntry[] Entries, int CurrencyAwarded);
public record TutorialEventEntry(string ParticipantName, bool IsPlayer, int Score, int Placement);

public record LobbyResponse(string Id, string EventDefinitionId, string EventName, string EventType, DateTime ScheduledRunAt, string Status, int EntrantsCount, int PlayerSlots, int EntryFee);
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
    ShopStorageDrawersType[]? StorageDrawers = null,
    ShopMedicationType[]? Medications = null,
    ShopWeatherAccessoryType[]? WeatherAccessories = null);

public record ShopWeatherAccessoryType(string Id, string Name, string Description, string Suppresses, int InGamePrice);
public record WeatherAccessoryPurchaseResponse(string AccessoryId, string TypeId, int AnchorIndex, decimal NewBalance);

// Delivery: "HomeAccessoryDevice" | "DirectDose". The /api/shop endpoint serializes the
// domain MedicationType record directly (no DTO mapping), so property names match verbatim.
public record ShopMedicationType(
    string Id, string Name, string Description, string DeliveryType, string TargetCategory,
    int InGamePrice, double UseCooldownHours, double PreventionWindowHours, double PreventionFactor,
    double CriticalBufferPerUseHours, double MaxCriticalBufferHours, double CureProgressPerUse,
    double SideEffectStressHoursPerUse);

public record MedicationDevicePurchaseResponse(string DeviceId, string TypeId, int AnchorIndex, decimal NewBalance);

public record ShopCageType(string Id, string Brand, string ModelName, string? Tier, int WidthCm, int DepthCm, int HeightCm, int MaxCapacity, int MaxFoodBowlSlots, int MaxWaterBottleSlots, int Price);
public record ShopAccessoryType(string Id, string Name, string? Description, int BaseEnrichment, string? MinimumTier, int Price);
public record ShopFoodBowlType(string Id, string Name, int CapacityRatDays, string? MinimumTier, int Price);
public record ShopWaterBottleType(string Id, string Name, int CapacityRatDays, string? MinimumTier, int Price);
public record ShopFoodStorageBinType(string Id, string Name, int CapacityRatDays, int Price);
public record ShopFoodType(string Id, string Name, int QualityTier, int InGamePrice, double? HealthBonus, string? TargetIllnessCategory = null);
public record ShopCarryCaseType(string Id, string Name, int Price);
public record ShopStorageDrawersType(string Id, string Name, int SlotsPerUnit, int Price);

public record CagePurchaseResponse(
    string CageId, string CageName, string CageTypeId, decimal NewBalance,
    decimal Refund = 0, string[]? DiscardedItemNotices = null);
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
// Mirrors the real backend ListingResponse/RatSnapshotResponse (PlayerMarketplaceModule.cs) —
// a prior version of this model was written against a guessed shape (flat RatName/Price/AgeMonths,
// no Snapshot) that never actually matched what the API returns.

public record MarketplaceListingResponse(
    string Id,
    string SellerId,
    string SellerUsername,
    string RatId,
    RatSnapshotResponse Snapshot,
    int AskingPrice,
    int ListingFeeCharged,
    double TransactionFeePercent,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? SoldAt,
    string? BuyerId,
    // Populated only on create/buy (currency changes as a side effect of those calls) — lets
    // the client patch its currency display directly instead of a follow-up
    // GetPlayerProfileAsync() (Task 30 §5).
    decimal? NewBalance = null);

public record RatSnapshotResponse(
    string Name,
    string GeneticDna,
    RatPhenotype Phenotype,
    DateTime DateOfBirth,
    int Generation,
    double SprintScore,
    double AgilityScore,
    double EnduranceScore);

// --- Adoption (Task 14) ---

public record PagedResponse<T>(T[] Items, long TotalCount, int Page, int PageSize);

// Flat stat/potential fields, not a nested Stats object — matches the real PooledRatResponse
// (AdoptionModule.cs), not the design doc's guessed AdoptionPoolEntryResponse/AdoptionPoolStats.
public record PooledRatResponse(
    string Id, string Name, string Source, DateTime DateOfBirth, string Sex, string LifeStage,
    RatPhenotype Phenotype, double SprintAbility, double AgilityAbility, double EnduranceAbility,
    int SprintPotential, int AgilityPotential, int EndurancePotential,
    double WeightGrams, string? ActiveCosmeticId);

public record AdoptResponse(RatResponse Rat, string CarryCaseId, decimal NewBalance);
public record SurrenderResponse(int RemainingSurrenders);

// --- Welfare (Task 15 groundwork — fee-waiver flags read by the Adoption page's modal) ---

public record PostTutorialAdoptionStatus(bool Active, bool FemaleOnly, bool FeeWaived);
public record MinimumRatCountStatus(bool Active, bool FeeWaived);
public record LoneCageStatus(bool Active, string? Type, LoneCageRef? CageA, LoneCageRef? CageB);
public record LoneCageRef(string CageId, string Sex);

public record WelfareStatus(
    string? ActiveBlock,
    string? BlockScope,
    PostTutorialAdoptionStatus PostTutorialAdoption,
    MinimumRatCountStatus MinimumRatCount,
    LoneCageStatus LoneCage);

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
    string? ActiveTitleText = null,
    int SurrenderCount = 0);

// --- Admin (Task 32) ---

public record AdminPlayerSummaryResponse(string Id, string Username, string? Email, int Currency, string Role);

public record AdminRatSummaryResponse(
    string Id, string Name, string Sex, string LifeStage, bool IsRetired, bool IsPregnant,
    double SprintAbility, double AgilityAbility, double EnduranceAbility);

public record AdminPlayerDetailResponse(
    string Id, string Username, string? Email, int Currency, string Role, DateTime CreatedAt,
    AdminRatSummaryResponse[] Rats);

public record AdjustCurrencyResponseDto(int NewBalance);

public record AdminAuditLogEntryDto(
    string Id, string AdminPlayerId, string TargetPlayerId, string Action, string Reason,
    string? Detail, DateTime Timestamp);

// DefaultValue/CurrentValue are JsonElement rather than a concrete CLR type — GameSettings spans
// several unrelated types (double/int/DateTime) in one flat field list, so this is read
// generically for display and passed back through Admin/Settings.razor's own type-appropriate
// edit control per row.
public record AdminGameSettingFieldDto(
    string Name, JsonElement DefaultValue, JsonElement CurrentValue, bool IsOverridden, bool IsAnchorField);
