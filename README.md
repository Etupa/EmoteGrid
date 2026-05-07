# EmoteGrid

EmoteGrid is a standalone Dalamud plugin for Final Fantasy XIV that provides a clean, searchable, and responsive ImGui-based grid interface for all of your character's unlocked emotes.

## Usage
Type `/emotegrid` in your chat window to open or close the EmoteGrid interface.

## Features
- **Dynamic Grid Interface:** Displays all of your owned emotes in an aesthetically pleasing grid format. The icons flow naturally as you resize the window.
- **Search & Filtering:** Instantly filter your emotes by name or category.
- **Auto-Sync:** EmoteGrid automatically detects which emotes your character has unlocked—including premium emotes, quest emotes, and default emotes. If you unlock a new emote while the window is open, it appears instantly!
- **Direct Execution:** Click on any emote icon to execute it immediately using native game calls.


### Building from Source
This plugin targets Dalamud API v15 and requires the `.NET 10.0 Preview SDK`.
1. Clone this repository.
2. Open `EmoteGrid.sln` in your preferred IDE (Visual Studio, JetBrains Rider) or use the .NET CLI.
3. Build the solution using the `Release` configuration.
4. The compiled `EmoteGrid.dll` will be output to `EmoteGrid/bin/Release/`.
5. Load the plugin via Dalamud's `/xlplugins` -> Settings -> Experimental -> Dev Plugin Locations.
