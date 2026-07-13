namespace GlassWingClient.Services;

// Client-side mirror of two backend formulas that used to be computed server-side purely for
// display (Task 29 §1): WeightTargetCalculator.ComputeTargetWeightGrams and
// HuskyProgression.ComputeProgress (GlassWing.Application/Features/RatsAndBreeding). Both are
// pure functions of already-shipped rat fields plus a couple of GameSettingsResponse constants
// (BiologicalScale, HuskyOnsetAgeMonths) — this class is now the canonical client-side copy;
// GlassWing.Application.Tests/RatsAndBreeding/HuskyProgressionTests.cs is the spec these were
// checked against when the backend-only copies were removed.
public static class RatFormulas
{
    // Mirrors WeightTargetCalculator.ComputeTargetWeightGrams — same 450/300 base weight by
    // sex, 0.6 + sz*0.8 size multiplier, 1 - ln*0.15 lean reduction.
    public static double ComputeTargetWeightGrams(string sex, int bodySize, int leanTendency)
    {
        var baseWeight     = sex == "Male" ? 450.0 : 300.0;
        var sz             = bodySize / 100.0;
        var ln             = leanTendency / 100.0;
        var sizeMultiplier = 0.6 + sz * 0.8;
        var leanReduction  = 1.0 - ln * 0.15;
        return baseWeight * sizeMultiplier * leanReduction;
    }

    // Mirrors HuskyProgression.ComputeProgress — same linear-ramp shape: 0% at onset, 100%
    // after an equal span past onset. Returns null for non-carriers.
    public static double? ComputeHuskyProgress(
        bool isHuskyCarrier, DateTime dateOfBirth, DateTime utcNow,
        double biologicalScale, double huskyOnsetAgeMonths)
    {
        if (!isHuskyCarrier) return null;

        var realAgeDays      = (utcNow - dateOfBirth).TotalDays;
        var biologicalMonths = realAgeDays * biologicalScale / 30.0;
        var monthsPast       = Math.Max(0, biologicalMonths - huskyOnsetAgeMonths);
        var progress         = Math.Min(monthsPast / huskyOnsetAgeMonths, 1.0);

        return progress * 100.0;
    }
}
