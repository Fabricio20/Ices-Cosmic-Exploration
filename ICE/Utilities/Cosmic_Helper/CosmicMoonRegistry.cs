using ICE.ConfigFiles;
using ICE.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static ICE.ConfigFiles.Config;

namespace ICE.Utilities.Cosmic_Helper;

/// <summary>
/// Per-moon settings that are not on WKSMissionUnit (hand-tuned in the plugin).
/// </summary>
public sealed class CosmicMoonDefinition
{
    /// <summary>TerritoryType row ID — same value Player.Territory uses in-zone (e.g. 1319 = Auxesia).</summary>
    public required uint TerritoryId { get; init; }
    public required string DisplayName { get; init; }
    public required string IconResource { get; init; }
    public required ItemFilter PlanetFilter { get; init; }
    public required uint PlanetCreditItemId { get; init; }
    /// <summary>Hub stellar return / navmesh anchor for this moon.</summary>
    public required Vector3 HubCenter { get; init; }
    /// <summary>Oizys and Auxesia have the cosmodrome / drone vendor; Sinus and Phaenna do not.</summary>
    public bool HasCosmodrome { get; init; }
    public uint? DronebitCreditId { get; init; }
    public uint? DronebitBoxId { get; init; }
    /// <summary>Max relic research stage on this hub (agenda playlist goals).</summary>
    public int MaxRelicStage { get; init; }
    /// <summary>Expedition log research icon under Resources/ResearchIcons.</summary>
    public string? ResearchIconResource { get; init; }
}

/// <summary>
/// One place to define every cosmic hub (Sinus through Auxesia — final moon for this expansion).
/// </summary>
/// <remarks>
/// Auxesia was the last moon added, but this registry is the uniform contract for <b>all</b> hubs:
/// UI filters, agenda warnings, overlay planets, debug tables, credits, hub centers, and cosmodrome checks
/// should go through here instead of hardcoding 1237 / 1291 / 1310 / 1319.
/// <para>
/// Mission territory still comes from <see cref="CosmicTerritoryResolver"/> (game PlaceName), not from row-id bands.
/// Hand-authored data (NPC positions, gathering YAML, fish holes, MissionScores) stays per moon but uses
/// <see cref="TerritoryId"/> and <see cref="DisplayName"/> from this type.
/// </para>
/// </remarks>
public static class CosmicMoonRegistry
{
    public static readonly CosmicMoonDefinition Sinus = new()
    {
        TerritoryId = 1237,
        DisplayName = "Sinus Ardorum",
        IconResource = "ICE.Resources.Sinus_Ardorum.png",
        PlanetFilter = ItemFilter.Sinus,
        PlanetCreditItemId = 45691,
        HubCenter = new(2.84f, 1.55f, -0.06f),
        MaxRelicStage = 9,
        ResearchIconResource = "ICE.Resources.ResearchIcons.novice.png",
    };

    public static readonly CosmicMoonDefinition Phaenna = new()
    {
        TerritoryId = 1291,
        DisplayName = "Phaenna",
        IconResource = "ICE.Resources.Phaenna.png",
        PlanetFilter = ItemFilter.Phaenna,
        PlanetCreditItemId = 48146,
        HubCenter = new(339.90f, 52.60f, -412.10f),
        MaxRelicStage = 14,
        ResearchIconResource = "ICE.Resources.ResearchIcons.intermediate.png",
    };

    public static readonly CosmicMoonDefinition Oizys = new()
    {
        TerritoryId = 1310,
        DisplayName = "Oizys",
        IconResource = "ICE.Resources.Oizys.png",
        PlanetFilter = ItemFilter.Oizys,
        PlanetCreditItemId = 48147,
        HubCenter = new(-180.02f, 0.50f, 129.25f),
        HasCosmodrome = true,
        DronebitCreditId = 49170,
        DronebitBoxId = 50414,
        MaxRelicStage = 17,
        ResearchIconResource = "ICE.Resources.ResearchIcons.advance.png",
    };

    public static readonly CosmicMoonDefinition Auxesia = new()
    {
        TerritoryId = 1319,
        DisplayName = "Auxesia",
        IconResource = "ICE.Resources.Auxesia.png",
        PlanetFilter = ItemFilter.Auxesia,
        PlanetCreditItemId = 48148,
        HubCenter = new(291.00f, 205.78f, 376.02f),
        HasCosmodrome = true,
        DronebitCreditId = 49171,
        DronebitBoxId = 50415,
        MaxRelicStage = 20,
        ResearchIconResource = "ICE.Resources.ResearchIcons.expert.png",
    };

    public static readonly CosmicMoonDefinition[] All = [Sinus, Phaenna, Oizys, Auxesia];

    public static readonly IReadOnlyList<uint> TerritoryIds = All.Select(m => m.TerritoryId).ToArray();

    public static readonly IReadOnlyDictionary<uint, CosmicMoonDefinition> ByTerritoryId =
        All.ToDictionary(m => m.TerritoryId);

    // These dictionaries back the existing CosmicHelper.PlanetCreditInfo / HubCenter / DronebitInfo fields
    public static readonly Dictionary<uint, uint> PlanetCredits =
        All.ToDictionary(m => m.TerritoryId, m => m.PlanetCreditItemId);

    public static readonly Dictionary<uint, Vector3> HubCenters =
        All.ToDictionary(m => m.TerritoryId, m => m.HubCenter);

    // Only moons with a cosmodrome populate this — do not index blindly on Sinus/Phaenna
    public static readonly Dictionary<uint, CosmicHelper.Dronebit> Dronebits = All
        .Where(m => m.DronebitCreditId is uint creditId && m.DronebitBoxId is uint boxId)
        .ToDictionary(
            m => m.TerritoryId,
            m => new CosmicHelper.Dronebit { creditId = m.DronebitCreditId!.Value, boxId = m.DronebitBoxId!.Value });

    public static bool TryGetMoon(uint territoryId, out CosmicMoonDefinition moon) =>
        ByTerritoryId.TryGetValue(territoryId, out moon!);

    public static string GetDisplayName(uint territoryId) =>
        TryGetMoon(territoryId, out var moon) ? moon.DisplayName : $"Zone_{territoryId}";

    public static bool IsKnownCosmicTerritory(uint territoryId) =>
        ByTerritoryId.ContainsKey(territoryId);

    /// <summary>Moons that have the cosmodrome / drone vendor (Oizys, Auxesia).</summary>
    public static IEnumerable<CosmicMoonDefinition> WithCosmodrome =>
        All.Where(m => m.HasCosmodrome);

    public static string GetIconResource(uint territoryId) =>
        TryGetMoon(territoryId, out var moon) ? moon.IconResource : Sinus.IconResource;

    public static bool ItemFilterIncludesTerritory(ItemFilter filter, uint territoryId) =>
        TryGetMoon(territoryId, out var moon) && filter.HasFlag(moon.PlanetFilter);

    /// <summary>
    /// Standard-mode missions (rank &lt; 6) enabled for a job on a given hub — same rules for every moon.
    /// </summary>
    public static int CountEnabledStandardMissions(uint territoryId, uint jobId)
    {
        var count = 0;
        foreach (var (missionId, info) in CosmicHelper.SheetMissionDict)
        {
            if (info.TerritoryId != territoryId || !info.Jobs.Contains(jobId) || info.Rank >= 6)
                continue;

            if (C.MissionConfig.TryGetValue(missionId, out var cfg) && cfg.Enabled)
                count++;
        }

        return count;
    }

    /// <summary>Enabled missions on a hub (any rank) — used for startup logging.</summary>
    public static int CountEnabledMissions(uint territoryId)
    {
        var count = 0;
        foreach (var (missionId, info) in CosmicHelper.SheetMissionDict)
        {
            if (info.TerritoryId != territoryId)
                continue;

            if (C.MissionConfig.TryGetValue(missionId, out var cfg) && cfg.Enabled)
                count++;
        }

        return count;
    }

    public static CosmicMoonDefinition? GetMoonForTerritory(uint territoryId) =>
        TryGetMoon(territoryId, out var moon) ? moon : null;

    /// <summary>Target relic stage for max-relic playlist goals — one lookup instead of hardcoded 9/14/17/20.</summary>
    public static int GetMaxRelicGoal(PlaylistOptions option) => option switch
    {
        PlaylistOptions.SinusMax => Sinus.MaxRelicStage,
        PlaylistOptions.PhaennaMax => Phaenna.MaxRelicStage,
        PlaylistOptions.OizysMax => Oizys.MaxRelicStage,
        PlaylistOptions.AuxesiaMax => Auxesia.MaxRelicStage,
        _ => 0,
    };

    /// <summary>True when QuickLevelList has at least one mission on this hub.</summary>
    public static bool HasLevelingContent(CosmicMoonDefinition moon)
    {
        foreach (var missionId in CosmicHelper.QuickLevelList)
        {
            if (CosmicHelper.SheetMissionDict.TryGetValue(missionId, out var info) && info.TerritoryId == moon.TerritoryId)
                return true;
        }

        return false;
    }
}

