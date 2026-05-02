# EmoteGrid

EmoteGrid is a standalone Dalamud plugin for Final Fantasy XIV that provides a simple ImGui-based grid interface for all of your character's unlocked emotes.

### Dalamud Custom Repo
Simply add the custom repo to Dalamud

```https://raw.githubusercontent.com/Etupa/EmoteGrid/master/pluginmaster.json```

## Usage
Type `/emotegrid` in your chat window to open or close the EmoteGrid interface.

## Features
- **Dynamic Grid Interface:** Displays all of your owned emotes in an aesthetically pleasing grid format. The icons flow naturally as you resize the window.
- **Drag'n'Drop:** You can arrange and move you're emote into multiples tabs and rearrange tabs too. 
- **Auto-Sync:** EmoteGrid automatically detects which emotes your character has unlocked.
- **Direct Execution:** Click on any emote icon to execute it immediately using native game calls.


### Building from Source
This plugin targets Dalamud API v15 and requires the `.NET 10.0 Preview SDK`.
1. Clone this repository.
2. Open `EmoteGrid.sln` in your preferred IDE (Visual Studio, JetBrains Rider) or use the .NET CLI.
3. Build the solution using the `Release` configuration.
4. The compiled `EmoteGrid.dll` will be output to `EmoteGrid/bin/Release/`.
5. Load the plugin via Dalamud's `/xlplugins` -> Settings -> Experimental -> Dev Plugin Locations.


