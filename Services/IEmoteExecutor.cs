using EmoteGrid.Models;

namespace EmoteGrid.Services;

public interface IEmoteExecutor {
    void Execute(EmoteData emote, bool viaTextCommand = false);
}
