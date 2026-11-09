# Thanatophobia

A mod that makes online multiplayer actually playable again on old versions of Phasmophobia.

Multiplayer still connects on old versions, but it's a mess, constant stuttering, broken voice chat, and other problems. This mod points the game at a fresh free service you set up yourself, which smooths out the connection and gets voice chat working again.

## Compatibility

| Component | Supported Version |
|---|---|
| Phasmophobia | `0.26.8.1` |
| MelonLoader | `0.7.0` |
| .NET | `6.0` |

## Features

* Smooth online play (fixes the constant stuttering)
* Working voice chat (proximity and radio)
* More than 4 players in a lobby
* A speaking indicator and a list of who's talking nearby
* A custom nickname

## Setup

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) and start the game once, then close it.
2. Download `Thanatophobia.dll` from the [Releases](../../releases) page and drop it into the game's `Mods` folder.
3. Make a free account at the [Photon dashboard](https://dashboard.photonengine.com/), create an app, and copy its **App ID** (a long code).
4. Start the game, click **THANATOPHOBIA** on the main menu, paste the App ID, choose your region, and click **Save & Close**.

That's it, play multiplayer as normal.

**Everyone in a lobby must:** have the mod installed, be on the same game version, and use the same App ID and region. The App ID is like a shared key for your group, share yours with the friends you play with.

## Building it yourself
If you want to compile the mod from source, install the [.NET 6 SDK](https://dotnet.microsoft.com/download) and run:

```
dotnet build -c Release -p:GameDir="path\to\Phasmophobia"
```

## Disclaimer

Thanatophobia is an independent, community-developed project. It is not affiliated with, endorsed by, or associated with Kinetic Games.

Phasmophobia is the property of Kinetic Games. This repository does not distribute the game or any of its original game files. A legitimate copy of Phasmophobia is required to use this mod.

## License

[MIT](LICENSE) -- free to use and modify.
