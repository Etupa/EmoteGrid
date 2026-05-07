using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using EmoteGrid.Models;

namespace EmoteGrid.Services;

public class EmoteRepository : IEmoteRepository, IDisposable {
    private readonly List<EmoteData> _emotes = new();
    private readonly IDataManager _dataManager;
    private readonly IUnlockState _unlockState;
    private readonly IPluginLog _log;

    public EmoteRepository(IDataManager dataManager, IUnlockState unlockState, IPluginLog log) {
        _dataManager = dataManager;
        _unlockState = unlockState;
        _log = log;
    }

    public IReadOnlyList<EmoteData> Emotes => _emotes;

    public void Reload() {
        DisposeTextures();
        _emotes.Clear();
        LoadFromLumina();
    }

    public IReadOnlyList<EmoteData> GetUnlockedEmotes() {
        return _emotes.Where(e =>
            e.EmoteSheetData.UnlockLink == 0 || _unlockState.IsEmoteUnlocked(e.EmoteSheetData)
        ).ToList();
    }

    public IReadOnlyList<EmoteData> GetLockedEmotes() {
        return _emotes.Where(e =>
            e.EmoteSheetData.UnlockLink != 0 && !_unlockState.IsEmoteUnlocked(e.EmoteSheetData)
        ).ToList();
    }

    public IReadOnlyList<EmoteData> GetEmotesByIds(IEnumerable<ushort> ids) {
        return ids
            .Select(id => _emotes.FirstOrDefault(e => e.Id == id))
            .Where(e => e != null)
            .Select(e => e!)
            .ToList();
    }

    private void LoadFromLumina() {
        var emoteSheet = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Emote>();
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

        _log.Information($"Loaded {_emotes.Count} emotes from Lumina.");
    }

    private void DisposeTextures() {
        foreach (var emote in _emotes) {
            if (emote.SharedTexture is IDisposable disp) {
                disp.Dispose();
            }
        }
    }

    public void Dispose() {
        DisposeTextures();
        _emotes.Clear();
    }
}
