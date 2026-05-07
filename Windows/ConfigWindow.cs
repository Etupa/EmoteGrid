using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace EmoteGrid;

public class ConfigWindow : Window, IDisposable {
    private Configuration _config;

    public ConfigWindow(Configuration config) : base("EmoteGrid Settings") {
        _config = config;

        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(300, 150),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw() {
        bool save = false;

        float scale = _config.GridScale;
        if (ImGui.SliderFloat("Grid Scale", ref scale, 0.25f, 2.0f, "%.2f")) {
            scale = (float)Math.Round(scale * 20.0f) / 20.0f;
            _config.GridScale = scale;
            save = true;
        }

        bool hideHeader = _config.HideHeader;
        if (ImGui.Checkbox("Hide Main Window Header", ref hideHeader)) {
            _config.HideHeader = hideHeader;
            save = true;
        }

        bool hideAllEmotesTab = _config.HideAllEmotesTab;
        if (ImGui.Checkbox("Hide 'All Emotes' Tab", ref hideAllEmotesTab)) {
            _config.HideAllEmotesTab = hideAllEmotesTab;
            save = true;
        }

        bool hideLockedEmotesTab = _config.HideLockedEmotesTab;
        if (ImGui.Checkbox("Hide 'Locked Emotes' Tab", ref hideLockedEmotesTab)) {
            _config.HideLockedEmotesTab = hideLockedEmotesTab;
            save = true;
        }

        int opacity = _config.BackgroundOpacity;
        if (ImGui.SliderInt("Background Opacity %", ref opacity, 0, 100, "%d")) {
            _config.BackgroundOpacity = opacity;
            save = true;
        }

        if (save) {
            _config.Save();
        }
    }
}
