using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using EmoteGrid.Models;
using EmoteGrid.Services;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace EmoteGrid;

public class MainWindow : Window, IDisposable {
    private readonly Configuration _config;
    private readonly IEmoteRepository _emoteRepo;
    private readonly IEmoteExecutor _emoteExecutor;
    private readonly ITabManager _tabManager;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ITextureProvider _textureProvider;

    private bool _isCreatingTab = false;
    private string _newTabName = "";

    private bool _isRenamingTab = false;
    private int _renamingTabIndex = -1;
    private string _renameTabName = "";

    private const string EmotePayloadType = "EMOTE_PAYLOAD";
    private const string TabPayloadType = "TAB_PAYLOAD";

    public Action? OnToggleConfig;

    public MainWindow(
        Configuration config,
        IEmoteRepository emoteRepo,
        IEmoteExecutor emoteExecutor,
        ITabManager tabManager,
        IDalamudPluginInterface pluginInterface,
        ITextureProvider textureProvider
    ) : base("Emote Grid##EmoteGrid") {
        _config = config;
        _emoteRepo = emoteRepo;
        _emoteExecutor = emoteExecutor;
        _tabManager = tabManager;
        _pluginInterface = pluginInterface;
        _textureProvider = textureProvider;

        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(300, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void OnOpen() {
        _emoteRepo.Reload();
    }

    public override void PreDraw() {
        if (_config.HideHeader) {
            Flags |= ImGuiWindowFlags.NoTitleBar;
        } else {
            Flags &= ~ImGuiWindowFlags.NoTitleBar;
        }
        Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        BgAlpha = _config.BackgroundOpacity / 100f;
    }

    public void RefreshEmotes() {
        _emoteRepo.Reload();
    }

    public void Dispose() { }

    // ── Draw ─────────────────────────────────────────────────────────

    public override unsafe void Draw() {
        var cursorStart = ImGui.GetCursorPos();
        var contentWidth = ImGui.GetContentRegionAvail().X;
        var gearSize = ImGui.GetFrameHeight();

        if (ImGui.BeginTabBar("EmoteTabs")) {
            _tabManager.EnsureTabOrder();

            for (int i = 0; i < _config.TabOrder.Count; i++) {
                var tabId = _config.TabOrder[i];

                if (tabId == Configuration.AllEmotesTabId) {
                    if (!_config.HideAllEmotesTab) DrawDefaultTab(i, "All Emotes", false, true);
                } else if (tabId == Configuration.LockedTabId) {
                    if (!_config.HideLockedEmotesTab) DrawDefaultTab(i, _config.CustomLockedTabName, true, false);
                } else {
                    DrawCustomTab(i, tabId);
                }
            }

            if (ImGui.TabItemButton("+", ImGuiTabItemFlags.Trailing | ImGuiTabItemFlags.NoReorder)) {
                _isCreatingTab = true;
            }

            ImGui.EndTabBar();
        }

        var cursorAfterTab = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(cursorStart.X + contentWidth - gearSize, cursorStart.Y));

        _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push();
        if (ImGui.Button(FontAwesomeIcon.Cog.ToIconString(), new Vector2(gearSize, gearSize))) {
            OnToggleConfig?.Invoke();
        }
        _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Pop();

        ImGui.SetCursorPos(cursorAfterTab);

        HandleModals();
    }

    // ── Default Tabs (All Emotes / Locked) ───────────────────────────

    private unsafe void DrawDefaultTab(int orderIndex, string label, bool showLocked, bool showDuplicate) {
        if (!ImGui.BeginTabItem($"{label}###order_{orderIndex}", ImGuiTabItemFlags.NoReorder)) return;

        // Drag source
        if (ImGui.BeginDragDropSource()) {
            ImGui.SetDragDropPayload(TabPayloadType, BitConverter.GetBytes(orderIndex), ImGuiCond.None);
            ImGui.Text($"Move Tab: {label}");
            ImGui.EndDragDropSource();
        }

        // Drop target
        if (ImGui.BeginDragDropTarget()) {
            var tabPayload = ImGui.AcceptDragDropPayload(TabPayloadType);
            if (!tabPayload.IsNull && tabPayload.Data != null) {
                int droppedIndex = *(int*)tabPayload.Data;
                if (droppedIndex != orderIndex) {
                    _tabManager.MoveTabTo(droppedIndex, orderIndex);
                }
            }
            ImGui.EndDragDropTarget();
        }

        // Context menu
        if (ImGui.BeginPopupContextItem($"default_tab_context_{orderIndex}")) {
            if (showLocked && ImGui.MenuItem("Rename")) {
                _isRenamingTab = true;
                _renamingTabIndex = orderIndex;
                _renameTabName = label;
            }
            if (showDuplicate && ImGui.MenuItem("Duplicate")) {
                var ids = showLocked
                    ? _emoteRepo.GetLockedEmotes().Select(e => e.Id)
                    : _emoteRepo.GetUnlockedEmotes().Select(e => e.Id);
                _tabManager.DuplicateTab(label, ids);
            }
            if (showDuplicate) ImGui.Separator();
            if (orderIndex > 0 && ImGui.MenuItem("Move Left")) {
                _tabManager.MoveTabLeft(orderIndex);
            }
            if (orderIndex < _config.TabOrder.Count - 1 && ImGui.MenuItem("Move Right")) {
                _tabManager.MoveTabRight(orderIndex);
            }
            ImGui.EndPopup();
        }

        DrawGrid(_emoteRepo.Emotes, null, showLocked);
        ImGui.EndTabItem();
    }

    // ── Custom Tabs ──────────────────────────────────────────────────

    private unsafe void DrawCustomTab(int orderIndex, string tabName) {
        var emotesInTab = _config.TabEmotes.ContainsKey(tabName) ? _config.TabEmotes[tabName] : new List<ushort>();

        if (!ImGui.BeginTabItem($"{tabName}###order_{orderIndex}", ImGuiTabItemFlags.NoReorder)) return;

        // Drag source
        if (ImGui.BeginDragDropSource()) {
            ImGui.SetDragDropPayload(TabPayloadType, BitConverter.GetBytes(orderIndex), ImGuiCond.None);
            ImGui.Text($"Move Tab: {tabName}");
            ImGui.EndDragDropSource();
        }

        // Drop target (tabs + emotes)
        if (ImGui.BeginDragDropTarget()) {
            var emotePayload = ImGui.AcceptDragDropPayload(EmotePayloadType);
            if (!emotePayload.IsNull && emotePayload.Data != null) {
                ushort droppedEmoteId = *(ushort*)emotePayload.Data;
                _tabManager.MoveEmoteToTab(droppedEmoteId, tabName);
            }

            var tabPayload = ImGui.AcceptDragDropPayload(TabPayloadType);
            if (!tabPayload.IsNull && tabPayload.Data != null) {
                int droppedIndex = *(int*)tabPayload.Data;
                if (droppedIndex != orderIndex) {
                    _tabManager.MoveTabTo(droppedIndex, orderIndex);
                }
            }

            ImGui.EndDragDropTarget();
        }

        // Context menu
        if (ImGui.BeginPopupContextItem($"tab_context_{orderIndex}")) {
            if (ImGui.MenuItem("Rename")) {
                _isRenamingTab = true;
                _renamingTabIndex = orderIndex;
                _renameTabName = tabName;
            }

            if (ImGui.MenuItem("Duplicate")) {
                _tabManager.DuplicateTab(tabName, emotesInTab);
            }

            ImGui.Separator();

            if (orderIndex > 0 && ImGui.MenuItem("Move Left")) {
                _tabManager.MoveTabLeft(orderIndex);
            }
            if (orderIndex < _config.TabOrder.Count - 1 && ImGui.MenuItem("Move Right")) {
                _tabManager.MoveTabRight(orderIndex);
            }

            ImGui.Separator();

            bool canDelete = emotesInTab.Count == 0 || ImGui.GetIO().KeyCtrl;
            if (!canDelete) ImGui.BeginDisabled();
            string deleteText = emotesInTab.Count > 0 ? "Delete (Hold Ctrl)" : "Delete";
            if (ImGui.MenuItem(deleteText)) {
                _tabManager.DeleteTab(orderIndex);
            }
            if (!canDelete) ImGui.EndDisabled();

            ImGui.EndPopup();
        }

        var filteredEmotes = _emoteRepo.GetEmotesByIds(emotesInTab);
        DrawGrid(filteredEmotes, tabName);

        ImGui.EndTabItem();
    }

    // ── Grid Rendering ───────────────────────────────────────────────

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
                    bool isLocked = emote.EmoteSheetData.UnlockLink != 0 &&
                                    !EmoteGridPlugin.UnlockState.IsEmoteUnlocked(emote.EmoteSheetData);
                    if (showLockedOnly) {
                        if (!isLocked) continue;
                    } else {
                        if (isLocked) continue;
                    }

                    if (currentColumn == 0) ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(currentColumn);

                    // Center the icon in the cell to eliminate uneven right-side padding
                    var cellAvail = ImGui.GetContentRegionAvail().X;
                    var buttonTotalWidth = iconSize + (ImGui.GetStyle().FramePadding.X * 2);
                    var offset = (cellAvail - buttonTotalWidth) / 2.0f;
                    if (offset > 0) {
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
                    }

                    DrawEmoteIcon(emote, iconSize, activeTabName, showLockedOnly);

                    currentColumn++;
                    if (currentColumn >= columns) currentColumn = 0;
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
                emote.SharedTexture = _textureProvider.GetFromGameIcon(
                    new Dalamud.Interface.Textures.GameIconLookup(emote.IconId));
            }

            var tex = emote.SharedTexture.GetWrapOrDefault();
            if (tex == null) return;

            ImGui.PushID($"emote_{emote.Id}");

            if (ImGui.ImageButton(tex.Handle, new Vector2(size, size))) {
                _emoteExecutor.Execute(emote, useTextCommand);
            }

            // Drag Source
            if (ImGui.BeginDragDropSource()) {
                ushort sourceEmoteId = emote.Id;
                ImGui.SetDragDropPayload(EmotePayloadType, BitConverter.GetBytes(sourceEmoteId), ImGuiCond.None);
                ImGui.Text($"Move {emote.Name}");
                ImGui.EndDragDropSource();
            }

            // Drop Target (custom tabs only)
            if (activeTabName != null && ImGui.BeginDragDropTarget()) {
                var emotePayload = ImGui.AcceptDragDropPayload(EmotePayloadType);
                if (!emotePayload.IsNull && emotePayload.Data != null) {
                    ushort droppedEmoteId = *(ushort*)emotePayload.Data;
                    _tabManager.ReorderEmoteInTab(activeTabName, droppedEmoteId, emote.Id);
                }
                ImGui.EndDragDropTarget();
            }

            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip(emote.Name);
            }

            // Context menu (move to tab / remove)
            if (_config.CustomTabs.Count > 0 && ImGui.BeginPopupContextItem($"emote_context_{emote.Id}")) {
                if (ImGui.BeginMenu("Move to Tab")) {
                    foreach (var tab in _config.CustomTabs) {
                        if (ImGui.MenuItem(tab)) {
                            _tabManager.MoveEmoteToTab(emote.Id, tab);
                        }
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.MenuItem("Remove from Tabs")) {
                    _tabManager.RemoveEmoteFromAllTabs(emote.Id);
                }
                ImGui.EndPopup();
            }

            ImGui.PopID();
        } catch (Exception) {
            emote.IconLoadFailed = true;
        }
    }

    // ── Modals ───────────────────────────────────────────────────────

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
                _tabManager.CreateTab(_newTabName);
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
                _tabManager.RenameTab(_renamingTabIndex, _renameTabName);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}
