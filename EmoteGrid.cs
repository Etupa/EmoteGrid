using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

namespace EmoteGrid;

public sealed class EmoteGridPlugin : IDalamudPlugin {
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;

    public WindowSystem WindowSystem = new("EmoteGridPlugin");
    public MainWindow MainWindow { get; init; }
    public ConfigWindow ConfigWindow { get; init; }
    public static Configuration Config { get; private set; } = null!;

    public EmoteGridPlugin() {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow = new MainWindow(Config);
        ConfigWindow = new ConfigWindow(Config);
        MainWindow.OnToggleConfig = () => ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler("/emotegrid", new CommandInfo(OnCommand) {
            HelpMessage = "Open the Emote Grid Window"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += DrawMainUI;
    }

    public void Dispose() {
        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        ConfigWindow.Dispose();
        
        CommandManager.RemoveHandler("/emotegrid");
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= DrawMainUI;
    }

    private void OnCommand(string command, string args) {
        if (args.Trim().Equals("refresh", System.StringComparison.OrdinalIgnoreCase)) {
            MainWindow.RefreshEmotes();
            PluginLog.Information("Emote list refreshed.");
            return;
        }
        MainWindow.Toggle();
    }

    private void DrawUI() {
        WindowSystem.Draw();
    }

    private void DrawConfigUI() {
        ConfigWindow.IsOpen = true;
    }

    private void DrawMainUI() {
        MainWindow.IsOpen = true;
    }
}
