# PlateUp! Mod Manager

A Windows desktop application for managing [PlateUp!](https://store.steampowered.com/app/1599600/PlateUp/) Steam Workshop mods. Subscribe, unsubscribe, organise mods into profiles, and switch between mod setups without ever opening the Steam client.

Built with WPF (.NET 10) and the Steam Community web API.

## Features

### Installed Mods
- View all currently installed Workshop mods with titles resolved from the Steam API
- Unsubscribe from individual mods or all mods at once
- Save your current mod list as a reusable profile

### Workshop Browser
- Search and browse the PlateUp! Steam Workshop directly in-app
- Subscribe to mods (and their dependencies) with one click
- Paginated results with debounced search and image previews

### Mod Profiles
- Save and load named mod profiles (a snapshot of subscribed mods)
- **Apply a profile** — automatically subscribes to the profile's mods and unsubscribes from everything else
- **Speed Run Mode** — temporarily subscribes to a set of mods, launches the game, waits for it to close, then unsubscribes and relaunches clean (useful for viewing speed run leaderboards without mods affecting your save)
- Built-in default profiles: *Speed Run Leaderboard* and *Clean (No Mods)*

### Settings
- Auto-detects Steam, PlateUp!, and Workshop folder paths from the Windows registry
- Manual path override if auto-detection doesn't suit your setup

### Steam Login
- Sign in via an embedded browser (WebView2) — no API key required
- Session is persisted locally so you don't need to re-login each time

## Getting Started

### Prerequisites
- **Windows 10/11**
- **[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** (included if you download the self-contained release)
- **Steam** installed with PlateUp! in your library

### Install
1. Download the latest release zip from the [Releases](../../releases) page
2. Extract to any folder
3. Run `HellfireStudios.PlateupModManager.UI.exe`

The app will auto-detect your Steam and PlateUp! paths on first launch. If it can't find them, configure the paths manually in **Settings**.

### Usage

1. **Sign in** — Go to **Settings** and click the Steam login button. Sign in with your Steam account in the browser window that appears.
2. **Browse & subscribe** — Use the **Workshop Browser** tab to find mods and subscribe with one click.
3. **Manage installed mods** — The **Installed Mods** tab shows everything currently in your Workshop folder. Unsubscribe from mods you no longer want.
4. **Create profiles** — On the **Installed Mods** tab, give your current setup a name and save it as a profile.
5. **Switch profiles** — Go to **Profiles** and click **Apply** on any profile to instantly switch your mod setup.
6. **Speed Run Mode** — On a speed run profile, click **Speed Run** to launch a temporary modded session that auto-cleans up afterwards.

## Building from Source

```bash
git clone https://github.com/HellfireStudios/PlateupModManager.git
cd PlateupModManager
dotnet restore
dotnet build
dotnet run --project HellfireStudios.PlateupModManager.UI
```

### Publish a self-contained release

```bash
dotnet publish HellfireStudios.PlateupModManager.UI -c Release -r win-x64 --self-contained true -o ./publish
```

## Release Process

Releases are automated via GitHub Actions:

1. Create a PR to `main` and apply a **semver label** (`semver:patch`, `semver:minor`, or `semver:major`)
2. The *Validate semver label* check ensures a label is present before merge
3. On merge, the *Build and Release* workflow:
   - Calculates the next semantic version from the latest git tag
   - Stamps the version into the project file
   - Builds a self-contained Windows x64 release
   - Creates a **draft GitHub Release** with auto-generated release notes and the zipped artifact

## License

This project is provided as-is. See the repository for licence details.
