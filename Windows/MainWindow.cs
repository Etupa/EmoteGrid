using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace EmoteGrid;

public class MainWindow : Window, IDisposable {
    private readonly List<EmoteData> _emotes = new();
    private Configuration _config;

    private bool _isCreatingTab = false;
    private string _newTabName = "";

    private bool _isRenamingTab = false;
    private int _renamingTabIndex = -1;
    private string _renameTabName = "";

    private const string EmotePayloadType = "EMOTE_PAYLOAD";
    private const string TabPayloadType = "TAB_PAYLOAD";

    public Action? OnToggleConfig;

    public MainWindow(Configuration config) : base("Emote Grid##EmoteGrid") {
        _config = config;

        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(300, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void OnOpen() {
        RefreshEmotes();
    }

    public override void PreDraw() {
        if (_config.HideHeader) {
            Flags |= ImGuiWindowFlags.NoTitleBar;
        } else {
            Flags &= ~ImGuiWindowFlags.NoTitleBar;
        }
        
        BgAlpha = _config.BackgroundOpacity / 100f;
    }

    public void RefreshEmotes() {
        _emotes.Clear();
        LoadEmotes();
    }

    private unsafe void LoadEmotes() {
        var emoteSheet = EmoteGridPlugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Emote>();
        if (emoteSheet == null) return;

        foreach (var emote in emoteSheet) {
            if (emote.Icon == 0) continue;
            
            var cat = emote.EmoteCategory.RowId;
            if (cat == 0) continue;
            
            var name = emote.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            _emotes.Add(new EmoteData {
                Id = (ushort)emote.RowId,
                Name = name,
                IconId = emote.Icon,
                Category = cat,
                EmoteSheetData = emote
            });
        }

        _emotes.Sort((a, b) => {
            var catCompare = a.Category.CompareTo(b.Category);
            if (catCompare != 0) return catCompare;
            return a.Id.CompareTo(b.Id);
        });
        
        EmoteGridPlugin.PluginLog.Information($"Loaded {_emotes.Count} emotes from Lumina.");
    }

    public void Dispose() {
        foreach (var emote in _emotes) {
            if (emote.SharedTexture is IDisposable disp) {
                disp.Dispose();
            }
        }
        _emotes.Clear();
    }

    public override unsafe void Draw() {
        // Gear button at far right of window, above tab bar
        var cursorStart = ImGui.GetCursorPos();
        var contentWidth = ImGui.GetContentRegionAvail().X;
        var frameHeight = ImGui.GetFrameHeight();
        ImGui.SetCursorPosX(cursorStart.X + contentWidth - frameHeight);
        EmoteGridPlugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push();
        if (ImGui.Button(FontAwesomeIcon.Cog.ToIconString(), new Vector2(frameHeight, frameHeight))) {
            OnToggleConfig?.Invoke();
        }
        EmoteGridPlugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Pop();
        ImGui.SetCursorPos(cursorStart);

        if (ImGui.BeginTabBar("EmoteTabs")) {

            if (!_config.HideAllEmotesTab && ImGui.BeginTabItem("All Emotes", ImGuiTabItemFlags.NoReorder)) {
                if (ImGui.BeginPopupContextItem("all_emotes_context")) {
                    if (ImGui.MenuItem("Duplicate")) {
                        string newTab = "All Emotes (Copy)";
                        int copyCount = 1;
                        while (_config.CustomTabs.Contains(newTab)) {
                            copyCount++;
                            newTab = $"All Emotes (Copy {copyCount})";
                        }
                        _config.CustomTabs.Add(newTab);
                        _config.TabEmotes[newTab] = _emotes.Where(e => e.EmoteSheetData.UnlockLink == 0 || EmoteGridPlugin.UnlockState.IsEmoteUnlocked(e.EmoteSheetData)).Select(e => e.Id).ToList();
                        _config.Save();
                    }
                    ImGui.EndPopup();
                }

                // Drop target for TAB reordering onto All Emotes (Move to index 0)
                if (ImGui.BeginDragDropTarget()) {
                    var tabPayload = ImGui.AcceptDragDropPayload(TabPayloadType);
                    if (!tabPayload.IsNull && tabPayload.Data != null) {
                        int droppedTabIndex = *(int*)tabPayload.Data;
                        if (droppedTabIndex >= 0 && droppedTabIndex < _config.CustomTabs.Count) {
                            var movedTab = _config.CustomTabs[droppedTabIndex];
                            _config.CustomTabs.RemoveAt(droppedTabIndex);
                            _config.CustomTabs.Insert(0, movedTab);
                            _config.Save();
                        }
                    }
                    ImGui.EndDragDropTarget();
                }

                DrawGrid(_emotes, null, false);
                ImGui.EndTabItem();
            }

            if (!_config.HideLockedEmotesTab && ImGui.BeginTabItem("Locked", ImGuiTabItemFlags.NoReorder)) {
                if (ImGui.BeginPopupContextItem("locked_emotes_context")) {
                    if (ImGui.MenuItem("Duplicate")) {
                        string newTab = "Locked (Copy)";
                        int copyCount = 1;
                        while (_config.CustomTabs.Contains(newTab)) {
                            copyCount++;
                            newTab = $"Locked (Copy {copyCount})";
                        }
                        _config.CustomTabs.Add(newTab);
                        _config.TabEmotes[newTab] = _emotes.Where(e => e.EmoteSheetData.UnlockLink != 0 && !EmoteGridPlugin.UnlockState.IsEmoteUnlocked(e.EmoteSheetData)).Select(e => e.Id).ToList();
                        _config.Save();
                    }
                    ImGui.EndPopup();
                }

                // Drop target for TAB reordering onto Locked Emotes (Move to index 0)
                if (ImGui.BeginDragDropTarget()) {
                    var tabPayload = ImGui.AcceptDragDropPayload(TabPayloadType);
                    if (!tabPayload.IsNull && tabPayload.Data != null) {
                        int droppedTabIndex = *(int*)tabPayload.Data;
                        if (droppedTabIndex >= 0 && droppedTabIndex < _config.CustomTabs.Count) {
                            var movedTab = _config.CustomTabs[droppedTabIndex];
                            _config.CustomTabs.RemoveAt(droppedTabIndex);
                            _config.CustomTabs.Insert(0, movedTab);
                            _config.Save();
                        }
                    }
                    ImGui.EndDragDropTarget();
                }

                DrawGrid(_emotes, null, true);
                ImGui.EndTabItem();
            }

            for (int i = 0; i < _config.CustomTabs.Count; i++) {
                DrawCustomTab(i);
            }

            if (ImGui.TabItemButton("+", ImGuiTabItemFlags.Trailing | ImGuiTabItemFlags.NoReorder)) {
                _isCreatingTab = true;
            }

            ImGui.EndTabBar();
        }

        HandleModals();
    }

    private unsafe void DrawCustomTab(int index) {
        if (index >= _config.CustomTabs.Count) return;
        var tabName = _config.CustomTabs[index];
        var emotesInTab = _config.TabEmotes.ContainsKey(tabName) ? _config.TabEmotes[tabName] : new List<ushort>();

        if (ImGui.BeginTabItem($"{tabName}###tab_{index}", ImGuiTabItemFlags.NoReorder)) {
            // Drag source for TAB reordering
            if (ImGui.BeginDragDropSource()) {
                int sourceIndex = index;
                ImGui.SetDragDropPayload(TabPayloadType, BitConverter.GetBytes(sourceIndex), ImGuiCond.None);
                ImGui.Text($"Move Tab: {tabName}");
                ImGui.EndDragDropSource();
            }

            // Drop target for TAB reordering or EMOTE receiving
            if (ImGui.BeginDragDropTarget()) {
                var emotePayload = ImGui.AcceptDragDropPayload(EmotePayloadType);
                if (!emotePayload.IsNull && emotePayload.Data != null) {
                    ushort droppedEmoteId = *(ushort*)emotePayload.Data;
                    
                    foreach (var list in _config.TabEmotes.Values) {
                        list.Remove(droppedEmoteId);
                    }
                    if (!_config.TabEmotes.ContainsKey(tabName)) {
                        _config.TabEmotes[tabName] = new List<ushort>();
                    }
                    _config.TabEmotes[tabName].Add(droppedEmoteId);
                    _config.Save();
                }

                var tabPayload = ImGui.AcceptDragDropPayload(TabPayloadType);
                if (!tabPayload.IsNull && tabPayload.Data != null) {
                    int droppedTabIndex = *(int*)tabPayload.Data;
                    if (droppedTabIndex != index && droppedTabIndex >= 0 && droppedTabIndex < _config.CustomTabs.Count) {
                        var movedTab = _config.CustomTabs[droppedTabIndex];
                        _config.CustomTabs.RemoveAt(droppedTabIndex);
                        // If we removed a tab before the target index, the target index shifts left
                        int insertIndex = droppedTabIndex < index ? index - 1 : index;
                        _config.CustomTabs.Insert(insertIndex, movedTab);
                        _config.Save();
                    }
                }

                ImGui.EndDragDropTarget();
            }

            if (ImGui.BeginPopupContextItem($"tab_context_{index}")) {
                if (ImGui.MenuItem("Rename")) {
                    _isRenamingTab = true;
                    _renamingTabIndex = index;
                    _renameTabName = tabName;
                }
                
                if (ImGui.MenuItem("Duplicate")) {
                    string newTab = $"{tabName} (Copy)";
                    int copyCount = 1;
                    while (_config.CustomTabs.Contains(newTab)) {
                        copyCount++;
                        newTab = $"{tabName} (Copy {copyCount})";
                    }
                    _config.CustomTabs.Add(newTab);
                    _config.TabEmotes[newTab] = new List<ushort>(emotesInTab);
                    _config.Save();
                }

                bool canDelete = emotesInTab.Count == 0 || ImGui.GetIO().KeyCtrl;
                
                if (!canDelete) {
                    ImGui.BeginDisabled();
                }
                string deleteText = emotesInTab.Count > 0 ? "Delete (Hold Ctrl)" : "Delete";
                if (ImGui.MenuItem(deleteText)) {
                    _config.CustomTabs.RemoveAt(index);
                    _config.TabEmotes.Remove(tabName);
                    _config.Save();
                }
                if (!canDelete) {
                    ImGui.EndDisabled();
                }

                ImGui.EndPopup();
            }

            var filteredEmotes = emotesInTab
                .Select(id => _emotes.FirstOrDefault(e => e.Id == id))
                .Where(e => e != null)
                .Select(e => e!)
                .ToList();
            DrawGrid(filteredEmotes, tabName);

            ImGui.EndTabItem();
        }
    }

    private void DrawGrid(IEnumerable<EmoteData> emotes, string? activeTabName, bool showLockedOnly = false) {
        var iconSize = 34.0f * ImGuiHelpers.GlobalScale * _config.GridScale;
        var padding = 4.0f * ImGuiHelpers.GlobalScale;
        var cellSize = iconSize + padding;

        string gridId = activeTabName ?? (showLockedOnly ? "locked_emotes" : "all_emotes");

        if (ImGui.BeginChild($"EmoteGridScrollable##{gridId}")) {
            var contentRegion = ImGui.GetContentRegionAvail();
            var totalCellWidth = cellSize + (ImGui.GetStyle().CellPadding.X * 2);
            var columns = (int)(contentRegion.X / totalCellWidth);
            if (columns < 1) columns = 1;

            if (ImGui.BeginTable($"EmoteGridTable##{gridId}", columns, ImGuiTableFlags.None)) {
                
                int currentColumn = 0;

                foreach (var emote in emotes) {
                    bool isLocked = emote.EmoteSheetData.UnlockLink != 0 && !EmoteGridPlugin.UnlockState.IsEmoteUnlocked(emote.EmoteSheetData);
                    if (showLockedOnly) {
                        if (!isLocked) continue;
                    } else {
                        if (isLocked) continue;
                    }

                    if (currentColumn == 0) {
                        ImGui.TableNextRow();
                    }
                    ImGui.TableSetColumnIndex(currentColumn);

                    DrawEmoteIcon(emote, iconSize, activeTabName, showLockedOnly);

                    currentColumn++;
                    if (currentColumn >= columns) {
                        currentColumn = 0;
                    }
                }

                ImGui.EndTable();
            }
            ImGui.EndChild();
        }
    }

    private unsafe void DrawEmoteIcon(EmoteData emote, float size, string? activeTabName, bool useTextCommand = false) {
        if (emote.IconLoadFailed) return;

        try {
            if (emote.SharedTexture == null) {
                emote.SharedTexture = EmoteGridPlugin.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(emote.IconId));
            }

            var tex = emote.SharedTexture.GetWrapOrDefault();
            if (tex != null) {
                ImGui.PushID($"emote_{emote.Id}");
                if (ImGui.ImageButton(tex.Handle, new Vector2(size, size))) {
                    if (useTextCommand) {
                        ExecuteEmoteViaTextCommand(emote);
                    } else {
                        ExecuteEmote(emote.Id);
                    }
                }

                // Drag Source for Emote
                if (ImGui.BeginDragDropSource()) {
                    ushort sourceEmoteId = emote.Id;
                    ImGui.SetDragDropPayload(EmotePayloadType, BitConverter.GetBytes(sourceEmoteId), ImGuiCond.None);
                    ImGui.Text($"Move {emote.Name}");
                    ImGui.EndDragDropSource();
                }

                if (activeTabName != null && ImGui.BeginDragDropTarget()) {
                    var emotePayload = ImGui.AcceptDragDropPayload(EmotePayloadType);
                    if (!emotePayload.IsNull && emotePayload.Data != null) {
                        ushort droppedEmoteId = *(ushort*)emotePayload.Data;
                        var list = _config.TabEmotes[activeTabName];
                        
                        foreach (var kvp in _config.TabEmotes) {
                            if (kvp.Key != activeTabName) {
                                kvp.Value.Remove(droppedEmoteId);
                            }
                        }
                        
                        int sourceIdx = list.IndexOf(droppedEmoteId);
                        if (sourceIdx >= 0) {
                            list.RemoveAt(sourceIdx);
                        }
                        
                        int targetIdx = list.IndexOf(emote.Id);
                        if (targetIdx >= 0) {
                            list.Insert(targetIdx, droppedEmoteId);
                        } else {
                            list.Add(droppedEmoteId);
                        }
                        _config.Save();
                    }
                    ImGui.EndDragDropTarget();
                }
                
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(emote.Name);
                }

                if (_config.CustomTabs.Count > 0 && ImGui.BeginPopupContextItem($"emote_context_{emote.Id}")) {
                    if (ImGui.BeginMenu("Move to Tab")) {
                        foreach (var tab in _config.CustomTabs) {
                            if (ImGui.MenuItem(tab)) {
                                foreach (var list in _config.TabEmotes.Values) {
                                    list.Remove(emote.Id);
                                }
                                
                                if (!_config.TabEmotes.ContainsKey(tab)) {
                                    _config.TabEmotes[tab] = new List<ushort>();
                                }
                                _config.TabEmotes[tab].Add(emote.Id);
                                _config.Save();
                            }
                        }
                        ImGui.EndMenu();
                    }
                    if (ImGui.MenuItem("Remove from Tabs")) {
                         bool removed = false;
                         foreach (var list in _config.TabEmotes.Values) {
                             if (list.Remove(emote.Id)) removed = true;
                         }
                         if (removed) _config.Save();
                    }
                    ImGui.EndPopup();
                }

                ImGui.PopID();
            }
        } catch (Exception) {
            emote.IconLoadFailed = true;
        }
    }

    private unsafe void ExecuteEmote(ushort emoteId) {
        var agentModule = FFXIVClientStructs.FFXIV.Client.UI.UIModule.Instance()->GetAgentModule();
        if (agentModule == null) return;

        var agentEmote = (AgentEmote*)agentModule->GetAgentByInternalId(AgentId.Emote);
        if (agentEmote == null) return;

        agentEmote->ExecuteEmote(emoteId);
    }

    private unsafe void ExecuteEmoteViaTextCommand(EmoteData emote) {
        try {
            var textCommandRef = emote.EmoteSheetData.TextCommand;
            if (textCommandRef.RowId == 0) {
                // Fallback to AgentEmote if no text command
                ExecuteEmote(emote.Id);
                return;
            }

            var textCommandSheet = EmoteGridPlugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TextCommand>();
            if (textCommandSheet == null) {
                ExecuteEmote(emote.Id);
                return;
            }

            var textCommand = textCommandSheet.GetRow(textCommandRef.RowId);
            var command = textCommand.Command.ToString();
            if (string.IsNullOrEmpty(command)) {
                ExecuteEmote(emote.Id);
                return;
            }

            var uiModule = FFXIVClientStructs.FFXIV.Client.UI.UIModule.Instance();
            if (uiModule == null) return;

            var shellModule = uiModule->GetRaptureShellModule();
            if (shellModule == null) return;

            var utf8Command = new FFXIVClientStructs.FFXIV.Client.System.String.Utf8String();
            utf8Command.SetString(command);
            shellModule->ExecuteCommandInner(&utf8Command, uiModule);
            utf8Command.Dtor();
        } catch (Exception ex) {
            EmoteGridPlugin.PluginLog.Error($"Failed to execute emote via text command: {ex.Message}");
            // Fallback to AgentEmote
            ExecuteEmote(emote.Id);
        }
    }

    private void HandleModals() {
        if (_isCreatingTab) {
            ImGui.OpenPopup("Create New Tab");
            _isCreatingTab = false;
            _newTabName = "";
        }

        bool createOpen = true;
        if (ImGui.BeginPopupModal("Create New Tab", ref createOpen, ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.InputText("Tab Name", ref _newTabName, 50);
            if (ImGui.Button("Create") && !string.IsNullOrWhiteSpace(_newTabName)) {
                if (!_config.CustomTabs.Contains(_newTabName)) {
                    _config.CustomTabs.Add(_newTabName);
                    _config.TabEmotes[_newTabName] = new List<ushort>();
                    _config.Save();
                }
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (_isRenamingTab) {
            ImGui.OpenPopup("Rename Tab");
            _isRenamingTab = false;
        }

        bool renameOpen = true;
        if (ImGui.BeginPopupModal("Rename Tab", ref renameOpen, ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.InputText("New Tab Name", ref _renameTabName, 50);
            if (ImGui.Button("Rename") && !string.IsNullOrWhiteSpace(_renameTabName)) {
                if (_renamingTabIndex >= 0 && _renamingTabIndex < _config.CustomTabs.Count) {
                    var oldName = _config.CustomTabs[_renamingTabIndex];
                    if (oldName != _renameTabName && !_config.CustomTabs.Contains(_renameTabName)) {
                        _config.CustomTabs[_renamingTabIndex] = _renameTabName;
                        if (_config.TabEmotes.ContainsKey(oldName)) {
                            _config.TabEmotes[_renameTabName] = _config.TabEmotes[oldName];
                            _config.TabEmotes.Remove(oldName);
                        }
                        _config.Save();
                    }
                }
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private class EmoteData {
        public ushort Id;
        public string Name = "";
        public uint IconId;
        public uint Category;
        public Lumina.Excel.Sheets.Emote EmoteSheetData;
        public Dalamud.Interface.Textures.ISharedImmediateTexture? SharedTexture;
        public bool IconLoadFailed;
    }
}
