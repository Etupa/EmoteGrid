using System.Collections.Generic;

namespace EmoteGrid.Services;

public interface ITabManager {
    void EnsureTabOrder();
    void CreateTab(string name);
    void RenameTab(int orderIndex, string newName);
    void DeleteTab(int orderIndex);
    void DuplicateTab(string sourceName, IEnumerable<ushort> emoteIds);
    void MoveTabLeft(int orderIndex);
    void MoveTabRight(int orderIndex);
    void MoveTabTo(int fromOrderIndex, int toOrderIndex);
    void MoveEmoteToTab(ushort emoteId, string targetTab);
    void RemoveEmoteFromAllTabs(ushort emoteId);
    void ReorderEmoteInTab(string tabName, ushort droppedEmoteId, ushort targetEmoteId);
    string GenerateUniqueName(string baseName);
}
