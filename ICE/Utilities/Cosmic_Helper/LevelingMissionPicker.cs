using System.Collections.Generic;
using ECommons.GameHelpers;
using ICE.Utilities.GatheringHelper;

namespace ICE.Utilities.Cosmic_Helper
{
    /// <summary>
    /// Leveling-mode mission selection: out of the missions the game is currently offering for the
    /// selected job, pick the one with the best class EXP per minute (from run-history timings).
    /// </summary>
    public static class LevelingMissionPicker
    {
        // Run time (minutes) assumed for an unmeasured mission, giving it an optimistic EXP/min so a
        // high-exp unmeasured mission still gets probed but a low-exp one can't preempt a proven one.
        private const double OptimisticMinutesFloor = 1.0;

        // Collectable craft category codes (WKSMissionText.RowId) whose objective is "collectability
        // will be scored" -- can't be turned in without quality, so leveling keeps the quality solver.
        // The importer only flags gatherer collectables, not these. Progress-only codes: 99/101/140/145/236/300/309.
        private static readonly HashSet<uint> CollectableCraftCategories = new() { 100, 102, 146, 147, 148, 235, 237, 238 };

        // EXP % by player level band (in-game Class Exp table): 10-49 -> _1, 50-89 -> _2, 90+ -> _3.
        public static uint ExpForLevel(CosmicHelper.CosmicInfo m, int jobLevel) =>
            jobLevel >= 90 ? m.ExpModifier_3 :
            jobLevel >= 50 ? m.ExpModifier_2 :
                             m.ExpModifier_1;

        // Leveling tier: 10 / 50 / 90.
        public static uint TierForLevel(int jobLevel) =>
            jobLevel >= 90 ? 90u : jobLevel >= 50 ? 50u : 10u;

        // Best known run time in seconds, or 0 if never measured.
        public static double RunSeconds(uint missionId)
        {
            if (!C.MissionConfig.TryGetValue(missionId, out var cfg))
                return 0;
            if (cfg.AverageBronzeTime > 0) return cfg.AverageBronzeTime;
            if (cfg.AverageTime > 0) return cfg.AverageTime;
            return 0;
        }

        // Non-fish missions are always reachable; fish missions need shipped coords or a custom hole.
        public static bool HasTravelData(uint missionId)
        {
            if (!CosmicHelper.SheetMissionDict.TryGetValue(missionId, out var m))
                return false;
            if (!m.Attributes.HasFlag(MissionAttributes.Fish))
                return true;
            if (C.Personal_FishLocation.Any(f => f.ZoneId == m.TerritoryId && f.MapCoords == m.MapPosition))
                return true;
            return GatheringUtil.MoonFishingLocations.TryGetValue(m.TerritoryId, out var zoneHoles)
                && zoneHoles.TryGetValue(m.MapPosition, out var spots)
                && spots != null && spots.Count > 0;
        }

        // Leveling-eligible subset of `available`: matches the job, at or below the player's tier,
        // grants non-zero exp at the player's level, supported, reachable.
        public static List<uint> EligibleFor(uint job, int jobLevel, IReadOnlyCollection<uint> available)
        {
            var tier = TierForLevel(jobLevel);
            var result = new List<uint>();
            foreach (var id in available)
            {
                if (!CosmicHelper.SheetMissionDict.TryGetValue(id, out var m)) continue;
                if (!m.Jobs.Contains(job)) continue;
                if (m.Level > tier) continue;
                if (ExpForLevel(m, jobLevel) == 0) continue;
                if (UnsupportedMissions.Ids.Contains(id)) continue;
                if (!HasTravelData(id)) continue;
                result.Add(id);
            }
            return result;
        }

        // Whether this mission's crafts must be solved for quality. Covers gatherer/recipe collectables
        // (the attribute) plus collectable crafts the importer misses (by category code).
        public static bool RequiresQuality(uint missionId)
        {
            if (!CosmicHelper.SheetMissionDict.TryGetValue(missionId, out var m))
                return false;
            if (m.Attributes.HasFlag(MissionAttributes.Collectables))
                return true;
            if (!m.Attributes.HasFlag(MissionAttributes.Craft))
                return false;
            var toDoSheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.WKSMissionToDo>();
            return toDoSheet != null
                && toDoSheet.TryGetRow(m.ToDoId, out var row)
                && CollectableCraftCategories.Contains(row.WKSMissionText.RowId);
        }

        // Best candidate by EXP/min. Measured missions use their real rate; unmeasured ones an optimistic
        // rate so they get probed once, then converge. Returns 0 when there are no candidates.
        public static uint PickBest(IReadOnlyCollection<uint> candidates, int jobLevel)
        {
            uint best = 0;
            double bestRate = -1;
            foreach (var id in candidates)
            {
                if (!CosmicHelper.SheetMissionDict.TryGetValue(id, out var m)) continue;
                var exp = ExpForLevel(m, jobLevel);
                var secs = RunSeconds(id);
                var rate = secs > 0 ? exp / (secs / 60.0) : exp / OptimisticMinutesFloor;
                if (rate > bestRate) { bestRate = rate; best = id; }
            }
            return best;
        }
    }
}
