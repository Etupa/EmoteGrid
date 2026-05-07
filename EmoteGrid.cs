using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using EmoteGrid.Services;

namespace EmoteGrid;

public sealed class EmoteGridPlugin : IDalamudPlugin {
    // ── Dalamud Services (Composition Root) ──────────────────────────
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;

    // ── Application Components ───────────────────────────────────────
    public WindowSystem WindowSystem = new("EmoteGridPlugin");
    public static Configuration Config { get; private set; } = null!;

    private readonly IEmoteRepository _emoteRepo;
    private readonly MainWindow _mainWindow;
    private readonly ConfigWindow _configWindow;

    // ── Bootstrapping ────────────────────────────────────────────────
    public EmoteGridPlugin() {
        // Configuration
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Services
        _emoteRepo = new EmoteRepository(DataManager, UnlockState, PluginLog);
        var emoteExecutor = new EmoteExecutor(DataManager, PluginLog);
        var tabManager = new TabManager(Config);

        // Windows (injecting dependencies)
        _mainWindow = new MainWindow(Config, _emoteRepo, emoteExecutor, tabManager, PluginInterface, TextureProvider);
        _configWindow = new ConfigWindow(Config);
        _mainWindow.OnToggleConfig = () => _configWindow.IsOpen = !_configWindow.IsOpen;

        WindowSystem.AddWindow(_mainWindow);
        WindowSystem.AddWindow(_configWindow);

        // Commands
        CommandManager.AddHandler("/emotegrid", new CommandInfo(OnCommand) {
            HelpMessage = "Open the Emote Grid Window"
        });

        // Events
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += DrawMainUI;
    }

    // ── Teardown ─────────────────────────────────────────────────────
    public void Dispose() {
        WindowSystem.RemoveAllWindows();
        _mainWindow.Dispose();
        _configWindow.Dispose();
        if (_emoteRepo is IDisposable disposableRepo) disposableRepo.Dispose();

        CommandManager.RemoveHandler("/emotegrid");
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= DrawMainUI;
    }

    // ── Event Handlers ───────────────────────────────────────────────
    private void OnCommand(string command, string args) {
        if (args.Trim().Equals("refresh", System.StringComparison.OrdinalIgnoreCase)) {
            _mainWindow.RefreshEmotes();
            PluginLog.Information("Emote list refreshed.");
            return;
        }
        _mainWindow.Toggle();
    }

    private void DrawUI() => WindowSystem.Draw();
    private void DrawConfigUI() => _configWindow.IsOpen = true;
    private void DrawMainUI() => _mainWindow.IsOpen = true;
}
