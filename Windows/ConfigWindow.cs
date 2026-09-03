using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using EmoteGrid.Models;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace EmoteGrid;

public class ConfigWindow : Window, IDisposable {
    private readonly Configuration _config;
    private readonly EmoteGridPlugin _plugin;

    public WindowConfig? SelectedWindowConfig { get; set; }

    public ConfigWindow(Configuration config, EmoteGridPlugin plugin) : base("EmoteGrid Settings") {
        _config = config;
        _plugin = plugin;

        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(350, 260),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw() {
        if (_config.Windows.Count == 0) return;

        var currentConfig = SelectedWindowConfig;
        if (currentConfig == null || !_config.Windows.Contains(currentConfig)) {
            currentConfig = _config.Windows[0];
            SelectedWindowConfig = currentConfig;
        }

        bool save = false;

        // ── Window Selector Header ───────────────────────────────────
        int currentIndex = _config.Windows.IndexOf(currentConfig);
        string currentPreview = $"{currentConfig.Title} ({(currentConfig.IsOpen ? "Open" : "Closed")})";

        if (ImGui.BeginCombo("Target Window", currentPreview)) {
            for (int i = 0; i < _config.Windows.Count; i++) {
                var win = _config.Windows[i];
                string label = $"{win.Title} ({(win.IsOpen ? "Open" : "Closed")})###win_opt_{win.Id}";
                bool isSelected = i == currentIndex;

                if (ImGui.Selectable(label, isSelected)) {
                    SelectedWindowConfig = win;
                    currentConfig = win;
                }

                if (isSelected) {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        // Window Actions
        if (ImGui.Button("+ Duplicate Window")) {
            var matchingWindow = _plugin.GetMainWindow(currentConfig.Id);
            if (matchingWindow != null) {
                var newWin = _plugin.DuplicateWindow(matchingWindow);
                SelectedWindowConfig = newWin.WindowConfig;
                currentConfig = newWin.WindowConfig;
            }
        }

        ImGui.SameLine();

        bool canDelete = currentConfig.Id != "main" && _config.Windows.Count > 1;
        if (!canDelete) ImGui.BeginDisabled();
        if (ImGui.Button("Delete Window")) {
            var matchingWindow = _plugin.GetMainWindow(currentConfig.Id);
            if (matchingWindow != null) {
                _plugin.DeleteWindow(matchingWindow);
                SelectedWindowConfig = _config.Windows.FirstOrDefault();
                currentConfig = SelectedWindowConfig ?? _config.Windows[0];
            }
        }
        if (!canDelete) ImGui.EndDisabled();

        ImGui.SameLine();

        bool isOpen = currentConfig.IsOpen;
        if (ImGui.Checkbox("Window Open", ref isOpen)) {
            currentConfig.IsOpen = isOpen;
            var matchingWindow = _plugin.GetMainWindow(currentConfig.Id);
            if (matchingWindow != null) {
                matchingWindow.IsOpen = isOpen;
            }
            save = true;
        }

        ImGui.Separator();

        // ── Window Title ─────────────────────────────────────────────
        string title = currentConfig.Title;
        if (ImGui.InputText("Window Title", ref title, 50)) {
            currentConfig.Title = title;
            var matchingWindow = _plugin.GetMainWindow(currentConfig.Id);
            matchingWindow?.UpdateTitle(title);
            save = true;
        }
        string normName = EmoteGridPlugin.NormalizeName(currentConfig.Title);
        ImGui.TextDisabled($"CLI Toggle: /emotegrid {normName} (or /emotegrid \"{currentConfig.Title}\")");

        // ── Grid Scale ───────────────────────────────────────────────
        float scale = currentConfig.GridScale;
        if (ImGui.SliderFloat("Grid Scale", ref scale, 0.25f, 2.0f, "%.2f")) {
            scale = (float)Math.Round(scale * 20.0f) / 20.0f;
            currentConfig.GridScale = scale;
            save = true;
        }

        // ── Hide Header ──────────────────────────────────────────────
        bool hideHeader = currentConfig.HideHeader;
        if (ImGui.Checkbox("Hide Window Header", ref hideHeader)) {
            currentConfig.HideHeader = hideHeader;
            save = true;
        }

        // ── Hide Tab Bar ─────────────────────────────────────────────
        bool hideTabBar = currentConfig.HideTabBar;
        if (ImGui.Checkbox("Hide Tab Bar (Including Settings Wheel)", ref hideTabBar)) {
            currentConfig.HideTabBar = hideTabBar;
            save = true;
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Hides the tab bar and settings gear button.\nUse '/emotegrid settings' to access settings when hidden.");
        }

        // ── Displayed Tab (when Tab Bar is hidden) ───────────────────
        if (currentConfig.HideTabBar) {
            string selectedTab = currentConfig.SelectedTab ?? WindowConfig.AllEmotesTabId;
            string selectedLabel = selectedTab switch {
                WindowConfig.AllEmotesTabId => "All Emotes",
                WindowConfig.LockedTabId => currentConfig.CustomLockedTabName,
                _ => selectedTab
            };

            if (ImGui.BeginCombo("Displayed Tab", selectedLabel)) {
                foreach (var tabId in currentConfig.TabOrder) {
                    bool visible = tabId switch {
                        WindowConfig.AllEmotesTabId => !currentConfig.HideAllEmotesTab,
                        WindowConfig.LockedTabId => !currentConfig.HideLockedEmotesTab,
                        _ => currentConfig.CustomTabs.Contains(tabId)
                    };
                    if (!visible) continue;

                    string tabLabel = tabId switch {
                        WindowConfig.AllEmotesTabId => "All Emotes",
                        WindowConfig.LockedTabId => currentConfig.CustomLockedTabName,
                        _ => tabId
                    };
                    bool isSelected = currentConfig.SelectedTab == tabId;

                    if (ImGui.Selectable(tabLabel, isSelected)) {
                        currentConfig.SelectedTab = tabId;
                        save = true;
                    }

                    if (isSelected) {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }
        }

        // ── Default Tabs Visibility ──────────────────────────────────
        bool hideAllEmotesTab = currentConfig.HideAllEmotesTab;
        if (ImGui.Checkbox("Hide 'All Emotes' Tab", ref hideAllEmotesTab)) {
            currentConfig.HideAllEmotesTab = hideAllEmotesTab;
            save = true;
        }

        bool hideLockedEmotesTab = currentConfig.HideLockedEmotesTab;
        if (ImGui.Checkbox("Hide 'Locked Emotes' Tab", ref hideLockedEmotesTab)) {
            currentConfig.HideLockedEmotesTab = hideLockedEmotesTab;
            save = true;
        }

        // ── Background Opacity ───────────────────────────────────────
        int opacity = currentConfig.BackgroundOpacity;
        if (ImGui.SliderInt("Background Opacity %", ref opacity, 0, 100, "%d")) {
            currentConfig.BackgroundOpacity = opacity;
            save = true;
        }

        if (save) {
            _config.Save();
        }
    }
}
