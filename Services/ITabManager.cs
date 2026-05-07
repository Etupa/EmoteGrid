using System.Collections.Generic;

namespace EmoteGrid.Services;

public interface ITabManager {
    void CreateTab(string name);
    void RenameTab(int index, string newName);
    void DeleteTab(int index);
    void DuplicateTab(string newName, IEnumerable<ushort> emoteIds);
    void MoveTabTo(int fromIndex, int toIndex);
    void MoveEmoteToTab(ushort emoteId, string targetTab);
    void RemoveEmoteFromAllTabs(ushort emoteId);
    void ReorderEmoteInTab(string tabName, ushort droppedEmoteId, ushort targetEmoteId);
    string GenerateUniqueName(string baseName);
}
