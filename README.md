# over the hill — Dev Console Lite

A minimal [MelonLoader](https://melonwiki.xyz/) mod for **over the hill** (Funselektor) that adds an
in-game console with three commands:

| Command | What it does |
|---|---|
| `money <amount>` | add money to your local inventory |
| `day [hour] [minute]` | set the time of day (default `12:00`) |
| `car` | print the current vehicle: name, speed, mass, wheels, climb angle, substeps |

`help`, `clear` and `close` are included as console basics. There are **no vehicle stat modifiers**
(no mass / grip / climb changes).

## Requirements

- **over the hill** on Steam, Unity **6000.0.75f1**, IL2CPP build
- **MelonLoader 0.7.3** (x64, Open-Beta) installed into the game folder
- The game must have been launched once with MelonLoader so it generates
  `MelonLoader\Il2CppAssemblies\` (202 interop DLLs)

## Install

1. Close the game.
2. Copy `dist\OTHDevConsoleLite.dll` into:

   ```
   <Steam>\steamapps\common\over the hill\Mods\
   ```

   Create the `Mods` folder if it doesn't exist.
3. Launch the game.

Do **not** run this alongside the full `OTHDevConsole.dll` — both bind F8 and you'll get two
consoles.

## Use

- Press **F8** or **`** (backquote) in the game window to toggle the console.
- Type a command, **Enter** to run. **Esc** closes. **Up/Down** browse history.
- Lines starting with `!` are errors.

Examples:

```
money 5000
day
day 7 30
car
```

## Multiplayer / host gating

The mod is **client-side only**; it does not send anything over the network.

- `money` and `day` are **host-only**. Host is detected from Photon Fusion's `NetworkRunner`
  (`IsServer`, `IsSinglePlayer`, `IsSharedModeMasterClient`). If you're a client you get:
  `!host only - you are a client in this session`.
- `day` writes the networked `TimeManager` (`Hours`/`Minutes`) and calls `ApplyWeatherAndTime()`,
  so on the host the change replicates to the session. Single-player falls back to
  `OfflineTimeManager`.
- `money` edits your own local profile (`PlayerInventoryManager` is a plain `MonoBehaviour`, not a
  `NetworkBehaviour`), so it is not shared with other players.
- `car` is read-only and always safe.

## Build from source

```powershell
cd src\OTHDevConsoleLite
dotnet build -c Release
```

The project references the game's generated interop assemblies. If the game is not at the default
path, pass it explicitly:

```powershell
dotnet build -c Release -p:GameDir="D:\path\to\over the hill"
```

Output: `src\OTHDevConsoleLite\bin\Release\net6.0\OTHDevConsoleLite.dll`.

## Notes

- Money is an inventory item (`PlayerInventoryManager.MoneyItemId`, id `1608550983`), not a save
  field. Changes live in memory until the game saves.
- `day` needs a loaded run; at the main menu there is no `TimeManager`/`OfflineTimeManager`.
- Tested against game build `26.10.19_B8DAF8D` (`Application.version = 0.0.1`), MelonLoader 0.7.3.
- No game assets are included in this repository.

## License

MIT
