using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using ICE.Utilities.Cosmic_Helper;
using ICE.Utilities.GatheringHelper;
using ICE.Utilities.GatheringHelper.RouteLoader;
using System;
using System.Collections.Generic;
using System.Text;

namespace ICE.Ui.DebugWindowTabs
{
    internal class Ui_GatherEditor
    {
        private static int _selectedPlanetIndex = 0;
        private static uint _selectedRoute = 0;
        private static string _routeSearch = string.Empty;

        private static FileDialogManager fileDialogManager = new FileDialogManager();

        private static int count = 0;

        public static void Draw()
        {
            count = GatheringUtil.GatherSpots
                .Where(x => x.Value.JobId.Contains(16) || x.Value.JobId.Contains(17))
                .Count();

            ImGui.Text($"Total: {count}");
            ImGui.SameLine();
            if (ImGui.Button("Set Save Location"))
            {
                fileDialogManager.OpenFolderDialog("Select Export Folder", (success, path) =>
                {
                    if (success && !string.IsNullOrEmpty(path))
                    {
                        C.CustomRoutePath = path;
                        C.Save();
                        PluginLog.Information($"Export path set to: {path}");
                    }
                });
            }
            ImGui.SameLine();
            ImGui.Text($"{C.CustomRoutePath}");

            for (int i = 0; i < CosmicMoonRegistry.All.Length; i++)
            {
                var moon = CosmicMoonRegistry.All[i];
                ImGui.RadioButton(moon.DisplayName, ref _selectedPlanetIndex, i);

                if (i < CosmicMoonRegistry.All.Length - 1)
                    ImGui.SameLine();
            }

            var selected = CosmicMoonRegistry.All[_selectedPlanetIndex];
            
            if (ImGui.BeginTable("Gather Route Editor Table", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.SizingFixedFit, ImGui.GetContentRegionAvail()))
            {
                ImGui.TableSetupColumn("Route Selector", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Route Editor", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                RouteSelector();

                ImGui.TableNextColumn();
                RouteInfo();

                ImGui.EndTable();
            }

            fileDialogManager.Draw();
        }

        private static void RouteSelector()
        {
            var planet = CosmicMoonRegistry.All[_selectedPlanetIndex];

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##routeSearch", "Search...", ref _routeSearch, 64);

            var routes = GatheringUtil.GatherSpots
                .Where(x => x.Value.TerritoryId == planet.TerritoryId)
                .Where(x => x.Value.JobId.Contains(16) || x.Value.JobId.Contains(17))
                .Where(x => string.IsNullOrEmpty(_routeSearch) ||
                            $"{x.Key} | X: {x.Value.X} Y:{x.Value.Y}"
                                .Contains(_routeSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();

            using (var child = ImRaii.Child("##routeList", new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 20), false))
            {
                if (!child) return;

                foreach (var route in routes)
                {
                    var label = $"{route.Key} | X: {route.Value.X:N0} Y:{route.Value.Y:N0}";
                    if (ImGui.Selectable(label, _selectedRoute == route.Key))
                        _selectedRoute = route.Key;
                }
            }
        }

        private static void RouteInfo()
        {
            if (!GatheringUtil.GatherSpots.TryGetValue(_selectedRoute, out var mapInfo))
                return;

            using (var child = ImRaii.Child("##missionList", new Vector2(-1, 5 * ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                foreach (var mission in mapInfo.MissionIds)
                {
                    if (!CosmicHelper.SheetMissionDict.TryGetValue(mission, out var sheetInfo))
                        continue;

                    for (int i = 0; i < sheetInfo.Jobs.Count; i++)
                    {
                        ImGui.Image(CosmicHelper.ClassInfoDict[sheetInfo.Jobs[i]].JobIcon.GetWrapOrEmpty().Handle, new(20, 20));
                        ImGui.SameLine();
                    }
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"[{mission}] - {sheetInfo.Name}");
                }
            }

            if (GatheringRouteLoader.LoadedRoutes.TryGetValue(_selectedRoute, out var routeInfo))
            {

            }
            else
            {
                ImGui.Text("No route file exist. Do you want to create one?");
                if (ImGui.Button("Create files"))
                {
                    GatheringRouteLoader.CreateMissingStubs();
                }
            }
        }
    }
}
