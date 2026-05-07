using System;
using Dalamud.Plugin.Services;
using EmoteGrid.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace EmoteGrid.Services;

public class EmoteExecutor : IEmoteExecutor {
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _log;

    public EmoteExecutor(IDataManager dataManager, IPluginLog log) {
        _dataManager = dataManager;
        _log = log;
    }

    public void Execute(EmoteData emote, bool viaTextCommand = false) {
        if (viaTextCommand) {
            ExecuteViaTextCommand(emote);
        } else {
            ExecuteViaAgent(emote.Id);
        }
    }

    private unsafe void ExecuteViaAgent(ushort emoteId) {
        var agentModule = FFXIVClientStructs.FFXIV.Client.UI.UIModule.Instance()->GetAgentModule();
        if (agentModule == null) return;

        var agentEmote = (AgentEmote*)agentModule->GetAgentByInternalId(AgentId.Emote);
        if (agentEmote == null) return;

        agentEmote->ExecuteEmote(emoteId);
    }

    private unsafe void ExecuteViaTextCommand(EmoteData emote) {
        try {
            var textCommandRef = emote.EmoteSheetData.TextCommand;
            if (textCommandRef.RowId == 0) {
                ExecuteViaAgent(emote.Id);
                return;
            }

            var textCommandSheet = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.TextCommand>();
            if (textCommandSheet == null) {
                ExecuteViaAgent(emote.Id);
                return;
            }

            var textCommand = textCommandSheet.GetRow(textCommandRef.RowId);
            var command = textCommand.Command.ToString();
            if (string.IsNullOrEmpty(command)) {
                ExecuteViaAgent(emote.Id);
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
            _log.Error($"Failed to execute emote via text command: {ex.Message}");
            ExecuteViaAgent(emote.Id);
        }
    }
}
