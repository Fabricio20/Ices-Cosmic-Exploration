using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using ECommons.GameHelpers;
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
        private static NodeInfo selectedNode = new();

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
            void AddNode(GatheringRoute route, IGameObject target)
            {
                route.Nodes ??= [];
                var playerPos = Player.Position;
                if (route.Nodes.Any(x => x.NodeId == target.BaseId))
                    return;

                route.Nodes.Add(new NodeInfo()
                {
                    NodeId = target.BaseId,
                    Position = target.Position,
                    LandZone = playerPos
                });
            }

            if (!GatheringUtil.GatherSpots.TryGetValue(_selectedRoute, out var mapInfo))
                return;

            if (ImGuiEx.IconButton(FontAwesomeIcon.Flag, $"{_selectedRoute}_{mapInfo.X}_{mapInfo.Y}"))
            {
                Utils.SetGatheringRing(mapInfo.TerritoryId, mapInfo.X, mapInfo.Y, mapInfo.Radius, $"Route {_selectedRoute}");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text($"Territory: {mapInfo.TerritoryId}");
                ImGui.Text($"Location: {mapInfo.X}, {mapInfo.Y}");
                ImGui.Text($"Radius: {mapInfo.Radius}");
                ImGui.EndTooltip();
            }

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
                if (ImGui.Button("Save Route"))
                {
                    GatheringRouteLoader.SaveRoute(routeInfo);
                }

                if (ImGui.BeginChild("Node Selection", new(200, 200), true))
                {
                    if (Player.Available)
                    {
                        if (Svc.Objects.LocalPlayer.TargetObject != null)
                        {
                            var lastTarget = Svc.Objects.LocalPlayer.TargetObject;
                            if (lastTarget.ObjectKind == ObjectKind.GatheringPoint)
                            {
                                if (ImGui.Button($"Add Node: {lastTarget.BaseId}"))
                                {
                                    AddNode(routeInfo, lastTarget);
                                }
                            }
                        }

                        var objectList = Svc.Objects.Where(x => x.ObjectKind == ObjectKind.GatheringPoint)
                            .OrderBy(x => Player.DistanceTo(x.Position)).ToList();
                        foreach (var node in objectList)
                        {
                            ImGui.PushID($"{node.BaseId}##{node.BaseId}_{node.Position}");
                            if (ImGui.Button($"{node.BaseId} | {Player.DistanceTo(node.Position):N2}"))
                            {
                                AddNode(routeInfo, node);
                            }
                            ImGui.PopID();
                        }
                    }
                }
                ImGui.EndChild();

                ImGui.SameLine();
                if (ImGui.BeginChild("Node Editor", new Vector2(200, 200), true))
                {
                    if (routeInfo.Nodes != null)
                    {
                        for (int i = 0; i < routeInfo.Nodes.Count; i++)
                        {
                            var node = routeInfo.Nodes[i];

                            // Up button (disabled on first item)
                            ImGui.BeginDisabled(i == 0);
                            if (ImGui.ArrowButton($"##up_{i}", ImGuiDir.Up))
                            {
                                (routeInfo.Nodes[i - 1], routeInfo.Nodes[i]) = (routeInfo.Nodes[i], routeInfo.Nodes[i - 1]);
                            }
                            ImGui.EndDisabled();

                            ImGui.SameLine();

                            // Down button (disabled on last item)
                            ImGui.BeginDisabled(i == routeInfo.Nodes.Count - 1);
                            if (ImGui.ArrowButton($"##down_{i}", ImGuiDir.Down))
                            {
                                (routeInfo.Nodes[i + 1], routeInfo.Nodes[i]) = (routeInfo.Nodes[i], routeInfo.Nodes[i + 1]);
                            }
                            ImGui.EndDisabled();

                            ImGui.SameLine();
                            if (ImGui.Button($"{node.NodeId}"))
                            {
                                selectedNode = node;
                            }
                        }
                    }
                }
                ImGui.EndChild();

                ImGui.Separator();

                var nodeInfo = routeInfo.Nodes?.FirstOrDefault(x => x == selectedNode);
                if (nodeInfo is not null)
                {
                    ImGui.Text($"X: {nodeInfo.Position.X:N2} | Y: {nodeInfo.Position.Y:N2} | Z: {nodeInfo.Position.Z:N2}");
                }
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
