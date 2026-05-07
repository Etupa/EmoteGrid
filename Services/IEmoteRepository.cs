using System.Collections.Generic;
using EmoteGrid.Models;

namespace EmoteGrid.Services;

public interface IEmoteRepository {
    IReadOnlyList<EmoteData> Emotes { get; }
    void Reload();
    IReadOnlyList<EmoteData> GetUnlockedEmotes();
    IReadOnlyList<EmoteData> GetLockedEmotes();
    IReadOnlyList<EmoteData> GetEmotesByIds(IEnumerable<ushort> ids);
}
