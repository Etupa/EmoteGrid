using System;
using System.Collections.Generic;
using System.Linq;
using EmoteGrid.Models;

namespace EmoteGrid.Services;

public class TabManager : ITabManager {
    private readonly WindowConfig _config;
    private readonly Action? _onSave;

    public TabManager(WindowConfig config, Action? onSave = null) {
        _config = config;
        _onSave = onSave;
    }

    public void EnsureTabOrder() {
        if (_config.TabOrder.Count > 0) {
            // Repair: add any custom tabs missing from TabOrder
            foreach (var tab in _config.CustomTabs) {
                if (!_config.TabOrder.Contains(tab)) {
                    _config.TabOrder.Add(tab);
                }
            }
            // Repair: remove stale entries
            _config.TabOrder.RemoveAll(id =>
                !_config.IsDefaultTab(id) && !_config.CustomTabs.Contains(id));
            return;
        }

        // First run or migration: build TabOrder from existing state
        _config.TabOrder.Add(WindowConfig.AllEmotesTabId);
        _config.TabOrder.Add(WindowConfig.LockedTabId);
        foreach (var tab in _config.CustomTabs) {
            _config.TabOrder.Add(tab);
        }
        _onSave?.Invoke();
    }

    public void CreateTab(string name) {
        if (string.IsNullOrWhiteSpace(name) || _config.CustomTabs.Contains(name)) return;
        _config.CustomTabs.Add(name);
        _config.TabEmotes[name] = new List<ushort>();
        _config.TabOrder.Add(name);
        _onSave?.Invoke();
    }

    public void RenameTab(int orderIndex, string newName) {
        if (orderIndex < 0 || orderIndex >= _config.TabOrder.Count) return;
        if (string.IsNullOrWhiteSpace(newName)) return;

        var oldName = _config.TabOrder[orderIndex];

        if (oldName == WindowConfig.LockedTabId) {
            _config.CustomLockedTabName = newName;
            _onSave?.Invoke();
            return;
        }

        if (_config.IsDefaultTab(oldName)) return; // Can't rename All Emotes
        if (oldName == newName || _config.CustomTabs.Contains(newName)) return;

        // Update CustomTabs
        int customIdx = _config.CustomTabs.IndexOf(oldName);
        if (customIdx >= 0) _config.CustomTabs[customIdx] = newName;

        // Update TabEmotes
        if (_config.TabEmotes.ContainsKey(oldName)) {
            _config.TabEmotes[newName] = _config.TabEmotes[oldName];
            _config.TabEmotes.Remove(oldName);
        }

        // Update TabOrder
        _config.TabOrder[orderIndex] = newName;
        _onSave?.Invoke();
    }

    public void DeleteTab(int orderIndex) {
        if (orderIndex < 0 || orderIndex >= _config.TabOrder.Count) return;

        var tabId = _config.TabOrder[orderIndex];
        if (_config.IsDefaultTab(tabId)) return; // Can't delete default tabs

        _config.TabOrder.RemoveAt(orderIndex);
        _config.CustomTabs.Remove(tabId);
        _config.TabEmotes.Remove(tabId);
        _onSave?.Invoke();
    }

    public void DuplicateTab(string newName, IEnumerable<ushort> emoteIds) {
        var uniqueName = GenerateUniqueName(newName);
        _config.CustomTabs.Add(uniqueName);
        _config.TabEmotes[uniqueName] = emoteIds.ToList();
        _config.TabOrder.Add(uniqueName);
        _onSave?.Invoke();
    }

    public void MoveTabLeft(int orderIndex) {
        if (orderIndex <= 0 || orderIndex >= _config.TabOrder.Count) return;
        (_config.TabOrder[orderIndex], _config.TabOrder[orderIndex - 1]) =
            (_config.TabOrder[orderIndex - 1], _config.TabOrder[orderIndex]);
        _onSave?.Invoke();
    }

    public void MoveTabRight(int orderIndex) {
        if (orderIndex < 0 || orderIndex >= _config.TabOrder.Count - 1) return;
        (_config.TabOrder[orderIndex], _config.TabOrder[orderIndex + 1]) =
            (_config.TabOrder[orderIndex + 1], _config.TabOrder[orderIndex]);
        _onSave?.Invoke();
    }

    public void MoveTabTo(int fromOrderIndex, int toOrderIndex) {
        if (fromOrderIndex < 0 || fromOrderIndex >= _config.TabOrder.Count) return;
        if (toOrderIndex < 0 || toOrderIndex > _config.TabOrder.Count) return;
        if (fromOrderIndex == toOrderIndex) return;

        var movedTab = _config.TabOrder[fromOrderIndex];
        _config.TabOrder.RemoveAt(fromOrderIndex);
        int insertIndex = fromOrderIndex < toOrderIndex ? toOrderIndex - 1 : toOrderIndex;
        if (insertIndex < 0) insertIndex = 0;
        if (insertIndex > _config.TabOrder.Count) insertIndex = _config.TabOrder.Count;
        _config.TabOrder.Insert(insertIndex, movedTab);
        _onSave?.Invoke();
    }

    public void MoveEmoteToTab(ushort emoteId, string targetTab) {
        foreach (var list in _config.TabEmotes.Values) {
            list.Remove(emoteId);
        }
        if (!_config.TabEmotes.ContainsKey(targetTab)) {
            _config.TabEmotes[targetTab] = new List<ushort>();
        }
        _config.TabEmotes[targetTab].Add(emoteId);
        _onSave?.Invoke();
    }

    public void RemoveEmoteFromAllTabs(ushort emoteId) {
        bool removed = false;
        foreach (var list in _config.TabEmotes.Values) {
            if (list.Remove(emoteId)) removed = true;
        }
        if (removed) _onSave?.Invoke();
    }

    public void ReorderEmoteInTab(string tabName, ushort droppedEmoteId, ushort targetEmoteId) {
        if (!_config.TabEmotes.ContainsKey(tabName)) return;
        var list = _config.TabEmotes[tabName];

        foreach (var kvp in _config.TabEmotes) {
            if (kvp.Key != tabName) {
                kvp.Value.Remove(droppedEmoteId);
            }
        }

        int sourceIdx = list.IndexOf(droppedEmoteId);
        if (sourceIdx >= 0) list.RemoveAt(sourceIdx);

        int targetIdx = list.IndexOf(targetEmoteId);
        if (targetIdx >= 0) {
            list.Insert(targetIdx, droppedEmoteId);
        } else {
            list.Add(droppedEmoteId);
        }
        _onSave?.Invoke();
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
