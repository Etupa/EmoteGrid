using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace EmoteGrid;

[Serializable]
public class Configuration : IPluginConfiguration {
    public const string AllEmotesTabId = "__all_emotes__";
    public const string LockedTabId = "__locked__";

    public int Version { get; set; } = 0;
    public float GridScale { get; set; } = 1.0f;
    public bool HideHeader { get; set; } = false;
    public int BackgroundOpacity { get; set; } = 100;
    public bool HideAllEmotesTab { get; set; } = false;
    public bool HideLockedEmotesTab { get; set; } = false;
    public string CustomLockedTabName { get; set; } = "Locked";

    // Unified tab rendering order (contains default tab IDs and custom tab names)
    public List<string> TabOrder { get; set; } = new();

    // List of custom tab names
    public List<string> CustomTabs { get; set; } = new();

    // Mapping from Custom Tab Name -> List of Emote IDs
    public Dictionary<string, List<ushort>> TabEmotes { get; set; } = new();

    public bool IsDefaultTab(string tabId) => tabId == AllEmotesTabId || tabId == LockedTabId;

    public void Save() {
        EmoteGridPlugin.PluginInterface.SavePluginConfig(this);
    }
}
