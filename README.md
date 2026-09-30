# SMT-InstallandManager

Mod tools for Supermarket Together.

## SMTInstaller.exe

Windows installer that sets up:

- [BepInEx 5](https://github.com/BepInEx/BepInEx), the mod loader
- [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), in-game mod settings (F1)
- SMT Mod Browser (below), from this repo's GitHub releases

These three are always installed. It finds the game in any Steam library and always downloads the
latest release of each. Optional mods listed in `optional-mods.json` (below) show up on the Optional Mods tab, each with a checkbox.
It checks the versions of the mod DLLs already in the game folder, however they got there, and only
installs what's missing or out of date. When everything is current, the button reinstalls it all.
On start it checks this repo's releases for a newer installer and offers to update itself.

## SMT Mod Browser (plugin)

A BepInEx plugin that lets you browse, install, update and remove
[Thunderstore](https://thunderstore.io/c/supermarket-together/) mods from inside the game.
Press **F6** to open it (configurable in F1). Dependencies are installed automatically;
new mods load after restarting the game.
Every installed mod has a **Remove** button (click it twice to confirm) that deletes its folders.
Mods that were deprecated or taken off Thunderstore still show up so they can be removed.

Mods are installed the same way r2modman does (`BepInEx/plugins/Owner-Name/`). Updates or
removals of mods that are currently loaded are applied on the next launch by a small preloader
patcher (`BepInEx/patchers/SMTModBrowser/`).

## SMT UberEats (optional plugin)

Replaces the delivery where ordered boxes drop from the sky one at a time (0.5–1 s each).
When the order arrives, each box goes straight into a free storage slot, picked the same way employees pick one
(labelled slots for that product first, then the nearest empty ones). Boxes that don't fit are stacked in a neat
grid next to the delivery point. Settings in F1: mode (`StorageThenFloor`, `FloorOnly`, `Vanilla`), stack height,
and the delivery time (10 s by default, counted down in a banner at the top of the screen; orders placed
during the countdown come with the same delivery). Only the host needs it; everything is synced through the game's own network calls.
The host also posts the countdown and arrival in the game chat (setting *Announce in chat*), so every player sees them;
players who have the mod get the banner instead of the chat lines.

## SMT Random Announcements (optional plugin)

Plays random store announcements over the loudspeakers at random times of the in-game day while the store is open
("Cleanup on aisle 4", "Register 2 is now open", ... 90 of them built in). It uses the game's own announcement system, so you need
an announcement desk and at least one speaker placed in the store, and like the desk it stays quiet in public games.
Only the host needs it; every player hears the announcements on the speakers.

Each announcement is read by one of several voices, all variations of the game's English voice: Announcer,
Store Manager (deeper), Summer Intern (higher), Security (megaphone), Back Office (muffled), Warehouse (echo),
Chipmunk, and Big Boss (booming, with bragging lines of his own). Players who have the mod hear the host's chosen voice;
the host tells them which voice through a chat line (setting *Announce in chat*), which every player sees in the chat.
Players without the mod hear the plain voice.
Settings in F1: announcements per day (8 by default, spread at random over the opening hours), which voices to use,
*Show in chat* (turn it off to keep the announcements out of your own chat), and
**F9** to play one straight away (host only). The lines are in `BepInEx/config/SMTRandomAnnouncements.txt`,
one per line; `{aisle}`, `{register}` and `{minutes}` are filled in with random numbers. A line starting with a
voice name in brackets (`[Big Boss] ...`) is only read by that voice, and a voice with lines of its own reads only those.

## SMT Auto Manufacturing (optional plugin)

Keeps the manufacturing shelves stocked without anyone pressing buttons on the manufacturing desk. Every 10 seconds
the host looks at every product labelled on the manufacturing shelves and counts what's on the shelves, in manufacturing
storage, in boxes lying around, in the machines' queues and in the machines right now. When that's less than what fits on
the shelves plus one spare box, it queues another one on the machine with the shortest queue, emptiest product first.
Before queuing it checks that the store has the ingredients (in storage or on the shelves, where employees fetch them), and
skips products it can't make yet.

It only decides *what* to make: employees assigned to manufacturing still fetch the ingredients, start the machines and
restock the shelves, as in the unmodded game. Without them, the queue fills up and waits for a player to add the ingredients.
Settings in F1: on/off, boxes in reserve, queue per machine and the check interval. Only the host needs it.

## SMT Anyone Continues (optional plugin)

At the end of the day the unmodded game waits for the host to press a key before the next day starts, and only the host
sees the "press any key" prompt. With this mod every player sees the prompt, and a key press from any of them starts the
next day. The host and every player who should be able to continue need it; players without it just wait for someone
who has it. The host tells the others about the prompt, and they send their key press back, through the game's own chat
calls in lines the game's chat never shows.

## Layout

| Folder         | What                                                        |
| -------------- | ----------------------------------------------------------- |
| `Installer/`   | SMTInstaller.exe (WinForms, .NET Framework 4.8)             |
| `Plugin/`      | SMTModBrowser.dll (BepInEx 5 plugin, Unity 2022.3)          |
| `Preloader/`   | SMTModBrowser.Preloader.dll (applies deferred mod changes)  |
| `UberEats/`    | SMTUberEats.dll (optional BepInEx plugin, delivery)       |
| `RandomAnnouncements/` | SMTRandomAnnouncements.dll (optional BepInEx plugin, loudspeaker) |
| `AutoManufacturing/` | SMTAutoManufacturing.dll (optional BepInEx plugin, manufacturing) |
| `AnyoneContinues/` | SMTAnyoneContinues.dll (optional BepInEx plugin, end of day) |
| `sources/`     | The game's `Assembly-CSharp.dll` and `Mirror.dll` (not committed) |
| `Plugin.Tests/`| Tests for the plugin's install logic, run on plain .NET     |
| `dist/`        | Prebuilt installer exe and the plugin release zip           |

## Build

Builds on Windows or Linux with the .NET 8 SDK:

```
dotnet build -c Release
dotnet run -c Release --project Plugin.Tests
```

A Release build writes `dist/SMTInstaller_vX.Y.Z.exe` and `dist/SMTModBrowser_vX.Y.Z.zip`.
Attach them to a GitHub release so the installer can find them (the repo must be public for that).

UberEats, Random Announcements, Auto Manufacturing and Anyone Continues are built on their own because they need the game's assemblies. Copy `Assembly-CSharp.dll` and
`Mirror.dll` from the game's `Supermarket Together_Data/Managed/` folder into `sources/` (git ignores them), then:

```
dotnet build -c Release UberEats/SMTUberEats.csproj
dotnet build -c Release RandomAnnouncements/SMTRandomAnnouncements.csproj
dotnet build -c Release AutoManufacturing/SMTAutoManufacturing.csproj
dotnet build -c Release AnyoneContinues/SMTAnyoneContinues.csproj
```

That writes `dist/SMTUberEats_vX.Y.Z.zip`, `dist/SMTRandomAnnouncements_vX.Y.Z.zip`, `dist/SMTAutoManufacturing_vX.Y.Z.zip`
and `dist/SMTAnyoneContinues_vX.Y.Z.zip`.

- The installer finds updates of itself by the version in the exe's file name, so keep the
  `SMTInstaller_vX.Y.Z.exe` name and bump `<Version>` in `Installer/SMTInstaller.csproj` to release one.
  It looks through the last 30 non-prerelease releases, so a release doesn't have to include the exe.
- For every download the installer picks the highest version (read from the file name) among the repo's
  last 30 non-prerelease releases, so a release only needs to include the files that changed.

## Optional mods

The installer reads [`optional-mods.json`](optional-mods.json) from the `main` branch on start and shows each
entry as an optional mod the user can tick. To add one, attach its zip to a release, then add an entry:

```json
{
  "mods": [
    {
      "name": "Example Mod",
      "description": "One line shown under the name",
      "asset": "^SMTExampleMod_v.*\\.zip$",
      "file": "BepInEx/plugins/ExampleMod/ExampleMod.dll",
      "selected": false
    }
  ]
}
```

| Field         | Meaning                                                                          |
| ------------- | -------------------------------------------------------------------------------- |
| `name`        | Shown on the card                                                                |
| `description` | Shown under the name                                                             |
| `asset`       | Regex for the release zip's file name. Put the version in the name (`_v1.0.0`)    |
| `file`        | The mod's DLL, relative to the game folder. Its file version tells the installer which version is installed, so keep it in step with the zip name (the csproj `<Version>`). Without it, the mod is always offered for install |
| `repo`        | Optional, `owner/name` of another repo to download from. Defaults to this repo    |
| `selected`    | Optional, `true` to have it ticked by default                                    |

The zip is extracted into the game folder, like the others, so lay it out the same way as
`SMTModBrowser_vX.Y.Z.zip` (e.g. `BepInEx/plugins/ExampleMod/ExampleMod.dll`). No installer release is needed
to add or change entries; installers already out there pick the change up on their next start.
