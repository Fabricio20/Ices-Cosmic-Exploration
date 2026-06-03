using Lumina.Excel.Sheets;
using System.Collections.Generic;
using static ICE.Utilities.ExcelHelper;

namespace ICE.Utilities.Cosmic_Helper;

/// <summary>
/// Figures out which moon/planet a stellar mission belongs to.
/// </summary>
/// <remarks>
/// Previously we guessed territory from WKSMissionUnit row ID ranges (see legacy fallback below).
/// That broke whenever Square added a new moon and had to be updated by hand.
/// The same resolver runs for Sinus, Phaenna, Oizys, and Auxesia (final moon) so every hub stays in sync.
/// <para>
/// Game data path: each mission has a <c>PlaceName</c> on WKSMissionUnit. We map that to a
/// TerritoryType ID using place names from cosmic zones (see <see cref="CosmicMoonRegistry"/> and WKSTerritoryInfo).
/// </para>
/// Called once from <c>DictionaryCreation</c> before missions are loaded into <c>SheetMissionDict</c>.
/// </remarks>
public static class CosmicTerritoryResolver
{
    private static bool _initialized;

    // PlaceName sheet row ID -> TerritoryType row ID (e.g. 1237 = Sinus Ardorum)
    private static readonly Dictionary<uint, uint> PlaceNameToTerritory = new();
    private static readonly HashSet<uint> KnownTerritoryIds = new();

    // Log each edge case once so Dalamud logs stay readable during dictionary build
    private static readonly HashSet<uint> LoggedFallbackMissions = new();
    private static readonly HashSet<uint> LoggedMismatchMissions = new();
    private static readonly HashSet<uint> LoggedUnresolvedMissions = new();

    /// <summary>
    /// Builds the PlaceName lookup table. Safe to call multiple times; only runs the heavy work once.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        PlaceNameToTerritory.Clear();
        KnownTerritoryIds.Clear();
        LoggedFallbackMissions.Clear();
        LoggedMismatchMissions.Clear();
        LoggedUnresolvedMissions.Clear();

        // Our four known hubs first (always present even if a sheet is missing on an older client)
        foreach (var territoryId in CosmicMoonRegistry.TerritoryIds)
            RegisterCosmicTerritory(territoryId);

        // Pick up any extra cosmic territories the client knows about (future moons, etc.)
        var wksTerritorySheet = Svc.Data.GetExcelSheet<WKSTerritoryInfo>();
        if (wksTerritorySheet != null)
        {
            foreach (var row in wksTerritorySheet)
            {
                var territoryId = row.TerritoryType.RowId;
                if (territoryId != 0)
                    RegisterCosmicTerritory(territoryId);
            }
        }

        _initialized = true;
        IceLogging.Info(
            $"[CosmicTerritoryResolver] Registered {KnownTerritoryIds.Count} territories, {PlaceNameToTerritory.Count} place name mappings");
    }

    /// <summary>
    /// Returns TerritoryType row ID for this mission, or 0 if we cannot place it on any moon.
    /// </summary>
    public static uint Resolve(WKSMissionUnit mission)
    {
        Initialize();

        var missionId = mission.RowId;
        var fromPlaceName = ResolveFromPlaceName(mission.PlaceName.RowId);
        var fromLegacy = ResolveLegacyMissionRowId(missionId);

        // Prefer game data — survives new moons without changing row-id thresholds
        if (fromPlaceName != 0)
        {
            // If both methods disagree, trust PlaceName but leave a warning for us to verify after a patch
            if (fromLegacy != 0 && fromPlaceName != fromLegacy && LoggedMismatchMissions.Add(missionId))
            {
                IceLogging.Warning(
                    $"[CosmicTerritoryResolver] Mission {missionId} PlaceName -> {fromPlaceName} disagrees with legacy row-id -> {fromLegacy}. Using PlaceName.");
            }

            return fromPlaceName;
        }

        // Old row-id bands — kept so older/partial data still works until PlaceName is populated for every mission
        if (fromLegacy != 0)
        {
            if (LoggedFallbackMissions.Add(missionId))
            {
                IceLogging.Debug(
                    $"[CosmicTerritoryResolver] Mission {missionId} has no mapped PlaceName ({mission.PlaceName.RowId}); using legacy row-id -> {fromLegacy}");
            }

            return fromLegacy;
        }

        // Caller skips the mission — better than assigning Sinus (1237) by mistake
        if (LoggedUnresolvedMissions.Add(missionId))
        {
            IceLogging.Warning(
                $"[CosmicTerritoryResolver] Could not resolve territory for mission {missionId} (PlaceName row {mission.PlaceName.RowId})");
        }

        return 0;
    }

    private static uint ResolveFromPlaceName(uint placeNameRowId)
    {
        if (placeNameRowId == 0)
            return 0;

        return PlaceNameToTerritory.TryGetValue(placeNameRowId, out var territoryId) ? territoryId : 0;
    }

    /// <summary>
    /// Pre-PlaceName logic: mission row IDs were allocated in blocks per moon release.
    /// Do not extend the last band blindly for a fifth moon — add the moon to <see cref="CosmicMoonRegistry"/> and rely on PlaceName instead.
    /// </summary>
    private static uint ResolveLegacyMissionRowId(uint missionRowId)
    {
        if (missionRowId < 545)
            return CosmicMoonRegistry.Sinus.TerritoryId;
        if (missionRowId < 1040)
            return CosmicMoonRegistry.Phaenna.TerritoryId;
        if (missionRowId < 1370)
            return CosmicMoonRegistry.Oizys.TerritoryId;
        if (missionRowId < 1703)
            return CosmicMoonRegistry.Auxesia.TerritoryId;

        // Row IDs at or above 1703 are unknown until game data maps them; returning 0 avoids wrong planet filters
        return 0;
    }

    // TerritoryType uses several PlaceName columns; missions may reference any of them
    private static void RegisterCosmicTerritory(uint territoryId)
    {
        if (!KnownTerritoryIds.Add(territoryId))
            return;

        if (TerritorySheet == null)
            return;

        var territory = TerritorySheet.GetRow(territoryId);
        RegisterPlaceName(territory.PlaceName.RowId, territoryId);
        RegisterPlaceName(territory.PlaceNameZone.RowId, territoryId);
        RegisterPlaceName(territory.PlaceNameRegion.RowId, territoryId);

        var map = territory.Map.Value;
        if (map.RowId != 0)
            RegisterPlaceName(map.PlaceName.RowId, territoryId);
    }

    private static void RegisterPlaceName(uint placeNameRowId, uint territoryId)
    {
        if (placeNameRowId == 0)
            return;

        if (PlaceNameToTerritory.TryGetValue(placeNameRowId, out var existing) && existing != territoryId)
        {
            IceLogging.Warning(
                $"[CosmicTerritoryResolver] PlaceName {placeNameRowId} maps to territory {existing} and {territoryId}; keeping {existing}");
            return;
        }

        PlaceNameToTerritory[placeNameRowId] = territoryId;
    }
}
