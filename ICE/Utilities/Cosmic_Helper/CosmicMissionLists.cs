using System.Collections.Generic;
using System.Linq;

namespace ICE.Utilities.Cosmic_Helper;

/// <summary>
/// Unlock mission IDs, rebuilt from the sheet on startup.
/// The old giant static lists in CustomNotes.cs are gone — if a patch adds rows the heuristic misses,
/// drop the row ID into ManualUnlockAdditions below.
/// </summary>
public static class CosmicMissionLists
{
    public static HashSet<uint> UnlockMissionIds { get; private set; } = [];

    // Patch gap filler — verify in the mission UI before copying +330 offsets from Oizys.
    public static HashSet<uint> ManualUnlockAdditions { get; } = [];

    public static void BuildFromSheet()
    {
        UnlockMissionIds.Clear();

        foreach (var (missionId, info) in CosmicHelper.SheetMissionDict)
        {
            if (!CosmicMoonRegistry.IsKnownCosmicTerritory(info.TerritoryId))
                continue;

            if (info.IsProvisional || info.IsCritical)
                continue;

            // Unlock chain: rank 1–5 standard missions on any cosmic hub
            if (info.Rank is >= 1 and <= 5)
                UnlockMissionIds.Add(missionId);
        }

        foreach (var id in ManualUnlockAdditions)
            UnlockMissionIds.Add(id);
    }

    public static bool IsUnlockMission(uint missionId) => UnlockMissionIds.Contains(missionId);

    public static IEnumerable<uint> UnlockMissionList => UnlockMissionIds;

    public static bool HasUnlockContent(uint territoryId) =>
        UnlockMissionIds.Any(id =>
            CosmicHelper.SheetMissionDict.TryGetValue(id, out var info) && info.TerritoryId == territoryId);

    // Leveling runs off whatever missions the game offers, so a hub supports it as long as it has any
    // crafter/gatherer mission in the sheet.
    public static bool HasLevelingContent(uint territoryId) =>
        CosmicHelper.SheetMissionDict.Values.Any(info =>
            info.TerritoryId == territoryId &&
            info.Jobs.Any(j => CosmicHelper.CrafterJobList.Contains(j) || CosmicHelper.GatheringJobList.Contains(j)));
}
