using System.Collections.Generic;
using System.Linq;

namespace EmoteGrid.Services;

public class TabManager : ITabManager {
    private readonly Configuration _config;

    public TabManager(Configuration config) {
        _config = config;
    }

    public void CreateTab(string name) {
        if (string.IsNullOrWhiteSpace(name) || _config.CustomTabs.Contains(name)) return;
        _config.CustomTabs.Add(name);
        _config.TabEmotes[name] = new List<ushort>();
        _config.Save();
    }

    public void RenameTab(int index, string newName) {
        if (index < 0 || index >= _config.CustomTabs.Count) return;
        if (string.IsNullOrWhiteSpace(newName)) return;

        var oldName = _config.CustomTabs[index];
        if (oldName == newName || _config.CustomTabs.Contains(newName)) return;

        _config.CustomTabs[index] = newName;
        if (_config.TabEmotes.ContainsKey(oldName)) {
            _config.TabEmotes[newName] = _config.TabEmotes[oldName];
            _config.TabEmotes.Remove(oldName);
        }
        _config.Save();
    }

    public void DeleteTab(int index) {
        if (index < 0 || index >= _config.CustomTabs.Count) return;
        var tabName = _config.CustomTabs[index];
        _config.CustomTabs.RemoveAt(index);
        _config.TabEmotes.Remove(tabName);
        _config.Save();
    }

    public void DuplicateTab(string newName, IEnumerable<ushort> emoteIds) {
        var uniqueName = GenerateUniqueName(newName);
        _config.CustomTabs.Add(uniqueName);
        _config.TabEmotes[uniqueName] = emoteIds.ToList();
        _config.Save();
    }

    public void MoveTabTo(int fromIndex, int toIndex) {
        if (fromIndex < 0 || fromIndex >= _config.CustomTabs.Count) return;
        if (toIndex < 0 || toIndex > _config.CustomTabs.Count) return;
        if (fromIndex == toIndex) return;

        var movedTab = _config.CustomTabs[fromIndex];
        _config.CustomTabs.RemoveAt(fromIndex);
        int insertIndex = fromIndex < toIndex ? toIndex - 1 : toIndex;
        if (insertIndex < 0) insertIndex = 0;
        if (insertIndex > _config.CustomTabs.Count) insertIndex = _config.CustomTabs.Count;
        _config.CustomTabs.Insert(insertIndex, movedTab);
        _config.Save();
    }

    public void MoveEmoteToTab(ushort emoteId, string targetTab) {
        foreach (var list in _config.TabEmotes.Values) {
            list.Remove(emoteId);
        }
        if (!_config.TabEmotes.ContainsKey(targetTab)) {
            _config.TabEmotes[targetTab] = new List<ushort>();
        }
        _config.TabEmotes[targetTab].Add(emoteId);
        _config.Save();
    }

    public void RemoveEmoteFromAllTabs(ushort emoteId) {
        bool removed = false;
        foreach (var list in _config.TabEmotes.Values) {
            if (list.Remove(emoteId)) removed = true;
        }
        if (removed) _config.Save();
    }

    public void ReorderEmoteInTab(string tabName, ushort droppedEmoteId, ushort targetEmoteId) {
        if (!_config.TabEmotes.ContainsKey(tabName)) return;
        var list = _config.TabEmotes[tabName];

        // Remove from all other tabs
        foreach (var kvp in _config.TabEmotes) {
            if (kvp.Key != tabName) {
                kvp.Value.Remove(droppedEmoteId);
            }
        }

        // Remove from current position in this tab
        int sourceIdx = list.IndexOf(droppedEmoteId);
        if (sourceIdx >= 0) {
            list.RemoveAt(sourceIdx);
        }

        // Insert before target
        int targetIdx = list.IndexOf(targetEmoteId);
        if (targetIdx >= 0) {
            list.Insert(targetIdx, droppedEmoteId);
        } else {
            list.Add(droppedEmoteId);
        }
        _config.Save();
    }

    public string GenerateUniqueName(string baseName) {
        if (!_config.CustomTabs.Contains(baseName)) return baseName;

        int copyCount = 1;
        string candidate = $"{baseName} (Copy)";
        while (_config.CustomTabs.Contains(candidate)) {
            copyCount++;
            candidate = $"{baseName} (Copy {copyCount})";
        }
        return candidate;
    }
}
