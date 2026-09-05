using System;
using System.Collections.Generic;
using System.Linq;

namespace EmoteGrid.Models;

[Serializable]
public class WindowConfig {
    public const string AllEmotesTabId = "__all_emotes__";
    public const string LockedTabId = "__locked__";

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "Emote Grid";
    public float GridScale { get; set; } = 1.0f;
    public bool HideHeader { get; set; } = false;
    public bool HideTabBar { get; set; } = false;
    public int BackgroundOpacity { get; set; } = 100;
    public int IconOpacity { get; set; } = 100;
    public bool HideAllEmotesTab { get; set; } = false;
    public bool HideLockedEmotesTab { get; set; } = false;
    public string CustomLockedTabName { get; set; } = "Locked";
    public string? SelectedTab { get; set; } = null;
    public bool IsOpen { get; set; } = false;

    // Unified tab rendering order (contains default tab IDs and custom tab names)
    public List<string> TabOrder { get; set; } = new();

    // List of custom tab names
    public List<string> CustomTabs { get; set; } = new();

    // Mapping from Custom Tab Name -> List of Emote IDs
    public Dictionary<string, List<ushort>> TabEmotes { get; set; } = new();

    public bool IsDefaultTab(string tabId) => tabId == AllEmotesTabId || tabId == LockedTabId;

    public WindowConfig Clone(string newTitle) {
        return new WindowConfig {
            Id = Guid.NewGuid().ToString("N")[..8],
            Title = newTitle,
            GridScale = GridScale,
            HideHeader = HideHeader,
            HideTabBar = HideTabBar,
            BackgroundOpacity = BackgroundOpacity,
            IconOpacity = IconOpacity,
            HideAllEmotesTab = HideAllEmotesTab,
            HideLockedEmotesTab = HideLockedEmotesTab,
            CustomLockedTabName = CustomLockedTabName,
            SelectedTab = SelectedTab,
            IsOpen = true,
            TabOrder = new List<string>(TabOrder),
            CustomTabs = new List<string>(CustomTabs),
            TabEmotes = TabEmotes.ToDictionary(kvp => kvp.Key, kvp => new List<ushort>(kvp.Value))
        };
    }
}
