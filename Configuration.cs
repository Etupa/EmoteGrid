using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using EmoteGrid.Models;

namespace EmoteGrid;

[Serializable]
public class Configuration : IPluginConfiguration {
    public const string AllEmotesTabId = WindowConfig.AllEmotesTabId;
    public const string LockedTabId = WindowConfig.LockedTabId;

    public int Version { get; set; } = 1;

    // List of all configured windows. Windows[0] is always the primary main window.
    public List<WindowConfig> Windows { get; set; } = new();

    // ── Legacy properties (retained for backward compatibility and migration) ──
    public float GridScale { get; set; } = 1.0f;
    public bool HideHeader { get; set; } = false;
    public bool HideTabBar { get; set; } = false;
    public int BackgroundOpacity { get; set; } = 100;
    public bool HideAllEmotesTab { get; set; } = false;
    public bool HideLockedEmotesTab { get; set; } = false;
    public string CustomLockedTabName { get; set; } = "Locked";
    public List<string> TabOrder { get; set; } = new();
    public List<string> CustomTabs { get; set; } = new();
    public Dictionary<string, List<ushort>> TabEmotes { get; set; } = new();

    public bool IsDefaultTab(string tabId) => tabId == AllEmotesTabId || tabId == LockedTabId;

    public void Initialize() {
        if (Windows == null || Windows.Count == 0) {
            Windows = new List<WindowConfig>();
            var mainWindow = new WindowConfig {
                Id = "main",
                Title = "Emote Grid",
                GridScale = GridScale,
                HideHeader = HideHeader,
                HideTabBar = HideTabBar,
                BackgroundOpacity = BackgroundOpacity,
                HideAllEmotesTab = HideAllEmotesTab,
                HideLockedEmotesTab = HideLockedEmotesTab,
                CustomLockedTabName = CustomLockedTabName,
                TabOrder = TabOrder.Count > 0 ? new List<string>(TabOrder) : new List<string>(),
                CustomTabs = CustomTabs.Count > 0 ? new List<string>(CustomTabs) : new List<string>(),
                TabEmotes = TabEmotes.Count > 0
                    ? TabEmotes.ToDictionary(kvp => kvp.Key, kvp => new List<ushort>(kvp.Value))
                    : new Dictionary<string, List<ushort>>()
            };
            Windows.Add(mainWindow);
        } else {
            if (string.IsNullOrEmpty(Windows[0].Id)) {
                Windows[0].Id = "main";
            }
        }
    }

    public void Save() {
        if (Windows.Count > 0) {
            var main = Windows[0];
            GridScale = main.GridScale;
            HideHeader = main.HideHeader;
            HideTabBar = main.HideTabBar;
            BackgroundOpacity = main.BackgroundOpacity;
            HideAllEmotesTab = main.HideAllEmotesTab;
            HideLockedEmotesTab = main.HideLockedEmotesTab;
            CustomLockedTabName = main.CustomLockedTabName;
            TabOrder = main.TabOrder;
            CustomTabs = main.CustomTabs;
            TabEmotes = main.TabEmotes;
        }

        EmoteGridPlugin.PluginInterface.SavePluginConfig(this);
    }
}
