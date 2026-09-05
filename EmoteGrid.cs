using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using EmoteGrid.Models;
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
    [PluginService] internal static IChatGui? ChatGui { get; private set; }

    // ── Application Components ───────────────────────────────────────
    public WindowSystem WindowSystem = new("EmoteGridPlugin");
    public static Configuration Config { get; private set; } = null!;

    private readonly IEmoteRepository _emoteRepo;
    private readonly IEmoteExecutor _emoteExecutor;
    private readonly ConfigWindow _configWindow;
    private readonly List<MainWindow> _mainWindows = new();
    private MainWindow? _lastFocusedMainWindow;

    // ── Bootstrapping ────────────────────────────────────────────────
    public EmoteGridPlugin() {
        // Configuration
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize();

        // If not logged in (e.g. game launched to title screen), ensure all windows start closed
        if (!ClientState.IsLoggedIn) {
            foreach (var winConfig in Config.Windows) {
                winConfig.IsOpen = false;
            }
        }

        // Services
        _emoteRepo = new EmoteRepository(DataManager, UnlockState, PluginLog);
        _emoteExecutor = new EmoteExecutor(DataManager, PluginLog);

        // Windows (injecting dependencies)
        _configWindow = new ConfigWindow(Config, this);
        WindowSystem.AddWindow(_configWindow);

        foreach (var winConfig in Config.Windows) {
            CreateAndRegisterMainWindow(winConfig);
        }

        // Commands
        CommandManager.AddHandler("/emotegrid", new CommandInfo(OnCommand) {
            HelpMessage = "Emote Grid commands. Use '/emotegrid [name]', '/emotegrid settings', or '/emotegrid duplicate'."
        });

        // Events
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += DrawMainUI;
        ClientState.Logout += OnLogout;
    }

    // ── Teardown ─────────────────────────────────────────────────────
    public void Dispose() {
        ClientState.Logout -= OnLogout;
        CloseAllWindows();

        WindowSystem.RemoveAllWindows();
        foreach (var win in _mainWindows) {
            win.Dispose();
        }
        _mainWindows.Clear();
        _configWindow.Dispose();
        if (_emoteRepo is IDisposable disposableRepo) disposableRepo.Dispose();

        CommandManager.RemoveHandler("/emotegrid");
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= DrawMainUI;
    }

    private void OnLogout(int type, int code) {
        CloseAllWindows();
    }

    public void CloseAllWindows() {
        foreach (var win in _mainWindows) {
            win.IsOpen = false;
            win.WindowConfig.IsOpen = false;
        }
        _configWindow.IsOpen = false;
        Config.Save();
    }

    // ── Window Management ────────────────────────────────────────────

    private MainWindow CreateAndRegisterMainWindow(WindowConfig winConfig) {
        var tabManager = new TabManager(winConfig, () => Config.Save());
        var window = new MainWindow(winConfig, _emoteRepo, _emoteExecutor, tabManager, PluginInterface, TextureProvider, () => Config.Save());

        window.OnToggleConfig = () => {
            _configWindow.SelectedWindowConfig = winConfig;
            _configWindow.IsOpen = true;
        };
        window.OnFocused = () => {
            _lastFocusedMainWindow = window;
        };

        _mainWindows.Add(window);
        WindowSystem.AddWindow(window);

        if (_lastFocusedMainWindow == null) {
            _lastFocusedMainWindow = window;
        }

        return window;
    }

    public MainWindow? GetMainWindow(string id) {
        return _mainWindows.FirstOrDefault(w => w.WindowConfig.Id == id);
    }

    public MainWindow? FindMainWindow(string search) {
        if (string.IsNullOrWhiteSpace(search)) return null;

        string cleanSearch = search.Trim('\"', '\'', ' ').Trim();
        if (string.IsNullOrEmpty(cleanSearch)) return null;

        // 1. Exact match on Window Title
        var match = _mainWindows.FirstOrDefault(w =>
            string.Equals(w.WindowConfig.Title, cleanSearch, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // 2. Match on Window Id
        match = _mainWindows.FirstOrDefault(w =>
            string.Equals(w.WindowConfig.Id, cleanSearch, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // 3. Match on normalized (alphanumeric only, lowercase)
        string normSearch = NormalizeName(cleanSearch);
        if (!string.IsNullOrEmpty(normSearch)) {
            match = _mainWindows.FirstOrDefault(w =>
                string.Equals(NormalizeName(w.WindowConfig.Title), normSearch, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // 4. Match by 1-based index (e.g. "2" or "#2")
        string numberStr = cleanSearch.TrimStart('#');
        if (int.TryParse(numberStr, out int index) && index >= 1 && index <= _mainWindows.Count) {
            return _mainWindows[index - 1];
        }

        // 5. Match by title contains
        match = _mainWindows.FirstOrDefault(w =>
            w.WindowConfig.Title.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        return null;
    }

    public static string NormalizeName(string name) {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var chars = name.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    public MainWindow DuplicateWindow(MainWindow sourceWindow) {
        var sourceConfig = sourceWindow.WindowConfig;
        string newTitle = GenerateUniqueWindowTitle(sourceConfig.Title);
        var newConfig = sourceConfig.Clone(newTitle);

        Config.Windows.Add(newConfig);
        Config.Save();

        var newWindow = CreateAndRegisterMainWindow(newConfig);

        if (sourceWindow.Position.HasValue) {
            newWindow.Position = sourceWindow.Position.Value + new Vector2(30, 30);
            newWindow.PositionCondition = Dalamud.Bindings.ImGui.ImGuiCond.FirstUseEver;
        }

        newWindow.IsOpen = true;
        _lastFocusedMainWindow = newWindow;

        PluginLog.Information($"Created duplicate window '{newTitle}' (Id: {newConfig.Id}).");
        ChatGui?.Print($"[EmoteGrid] Created duplicate window: {newTitle}");

        return newWindow;
    }

    public void DeleteWindow(MainWindow window) {
        if (window.WindowConfig.Id == "main" || Config.Windows.Count <= 1) {
            PluginLog.Warning("Cannot delete the primary main window.");
            ChatGui?.PrintError("[EmoteGrid] Cannot delete the primary main window.");
            return;
        }

        window.IsOpen = false;
        WindowSystem.RemoveWindow(window);
        _mainWindows.Remove(window);
        Config.Windows.Remove(window.WindowConfig);
        Config.Save();
        window.Dispose();

        if (_lastFocusedMainWindow == window) {
            _lastFocusedMainWindow = _mainWindows.FirstOrDefault();
        }

        PluginLog.Information($"Deleted window '{window.WindowConfig.Title}'.");
        ChatGui?.Print($"[EmoteGrid] Deleted window: {window.WindowConfig.Title}");
    }

    private string GenerateUniqueWindowTitle(string baseTitle) {
        var existingTitles = Config.Windows.Select(w => w.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int count = 2;
        string candidate = $"{baseTitle} {count}";
        while (existingTitles.Contains(candidate)) {
            count++;
            candidate = $"{baseTitle} {count}";
        }
        return candidate;
    }

    // ── Event Handlers ───────────────────────────────────────────────

    private void OnCommand(string command, string args) {
        string trimmed = args.Trim();
        if (string.IsNullOrEmpty(trimmed)) {
            if (_mainWindows.Count > 0) {
                _mainWindows[0].Toggle();
                Config.Save();
            }
            return;
        }

        string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string firstWord = parts[0].ToLowerInvariant();

        switch (firstWord) {
            case "settings":
            case "config":
                var targetConfig = _lastFocusedMainWindow?.WindowConfig ?? Config.Windows.FirstOrDefault();
                _configWindow.SelectedWindowConfig = targetConfig;
                _configWindow.IsOpen = true;
                break;

            case "duplicate":
            case "clone":
                var source = _lastFocusedMainWindow ?? _mainWindows.FirstOrDefault();
                if (source != null) {
                    DuplicateWindow(source);
                } else {
                    PluginLog.Warning("No Emote Grid window found to duplicate.");
                    ChatGui?.PrintError("[EmoteGrid] No Emote Grid window found to duplicate.");
                }
                break;

            case "refresh":
                foreach (var win in _mainWindows) {
                    win.RefreshEmotes();
                }
                PluginLog.Information("Emote list refreshed across all windows.");
                ChatGui?.Print("[EmoteGrid] Emote list refreshed.");
                break;

            case "all":
                bool anyOpen = _mainWindows.Any(w => w.IsOpen);
                foreach (var win in _mainWindows) {
                    win.IsOpen = !anyOpen;
                    win.WindowConfig.IsOpen = !anyOpen;
                }
                Config.Save();
                break;

            case "toggle":
                if (parts.Length > 1) {
                    string target = trimmed[parts[0].Length..].Trim();
                    ToggleWindowByName(target);
                } else {
                    if (_mainWindows.Count > 0) {
                        _mainWindows[0].Toggle();
                        Config.Save();
                    }
                }
                break;

            case "help":
                ChatGui?.Print("[EmoteGrid] Available commands:");
                ChatGui?.Print("  /emotegrid - Toggle the main window");
                ChatGui?.Print("  /emotegrid <name|#index> - Toggle a specific window (e.g. /emotegrid emotegrid2)");
                ChatGui?.Print("  /emotegrid settings - Open the settings window");
                ChatGui?.Print("  /emotegrid duplicate - Clone the current window with independent settings");
                ChatGui?.Print("  /emotegrid refresh - Reload emotes from game data");
                ChatGui?.Print("  /emotegrid all - Toggle all windows");
                break;

            default:
                // Treat whole argument as a target window identifier
                ToggleWindowByName(trimmed);
                break;
        }
    }

    private void ToggleWindowByName(string name) {
        var match = FindMainWindow(name);
        if (match != null) {
            match.Toggle();
            Config.Save();
            ChatGui?.Print($"[EmoteGrid] Toggled '{match.WindowConfig.Title}' ({(match.IsOpen ? "Open" : "Closed")}).");
        } else {
            string clean = name.Trim('\"', '\'', ' ');
            ChatGui?.PrintError($"[EmoteGrid] No window found matching '{clean}'.");
            string available = string.Join(", ", _mainWindows.Select(w => $"'{w.WindowConfig.Title}' (/emotegrid {NormalizeName(w.WindowConfig.Title)})"));
            ChatGui?.Print($"[EmoteGrid] Available windows: {available}");
        }
    }

    private void DrawUI() => WindowSystem.Draw();
    private void DrawConfigUI() {
        _configWindow.SelectedWindowConfig = _lastFocusedMainWindow?.WindowConfig ?? Config.Windows.FirstOrDefault();
        _configWindow.IsOpen = true;
    }
    private void DrawMainUI() {
        if (_mainWindows.Count > 0) {
            _mainWindows[0].IsOpen = true;
            _mainWindows[0].WindowConfig.IsOpen = true;
            Config.Save();
        }
    }
}
