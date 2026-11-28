# 3D Snake (Unity)

A snake game that lives in a cube — you can move forward, sideways, **and up and down**.

![Menu](docs/screenshots/menu.png)
![Gameplay](docs/screenshots/gameplay.png)

[中文说明](README.md)

## Download

Windows 64-bit build (unzip and run `Snake3D.exe` — Unity is not required):

| Version | Highlights | Download |
|---|---|---|
| **v1.3** | Skin system, Laoda theme (face texture / iced tea / custom SFX), second-person camera, translucent UI | **[Download](https://github.com/Pwn-pan/snake-3D/releases/download/v1.3/3Dsnake_v1.3.zip)** (28 MB) |
| v1.1 | Cloud leaderboard, nickname, audio volume control | [Download](https://github.com/Pwn-pan/snake-3D/releases/download/v1.1/3D._v11_.zip) (26 MB) |
| v1.0 | Difficulty & map selection, pause, game-over screen | [Download](https://github.com/Pwn-pan/snake-3D/releases/download/v1.0/3D._v10_.zip) (26 MB) |
| v0.9 | First playable version | [Download](https://github.com/Pwn-pan/snake-3D/releases/download/v0.9/3D._v9_.zip) (25 MB) |

> Latest release is recommended; older builds are kept for reference.

Requirements: Windows 10 / 11 (64-bit) and a DX11-capable GPU. **No Unity installation or runtime is required.**

## Controls

| Input | Action |
|---|---|
| `A` / `D` | Turn left / right 90° relative to the current heading |
| `W` / `S` | Keep climbing / descending (press `A`/`D` to level out) |
| `Shift` | Boost — the body lights up |
| Left mouse drag | Orbit the camera (free view) |
| Mouse wheel | Zoom |
| `Esc` / `P` | Pause |
| `R` | Restart after game over |
| `L` | Toggle the layer highlight (debug) |

## Rules

- You spawn at the exact centre of the map. The starting length depends on difficulty (1 / 3 / 5 segments).
- The body grows segment by segment; eating food adds one more.
- Hitting a wall or your own body **does not kill you instantly** — the snake stops and a grace window opens (about one move interval). Turn into a free direction to escape; run out of time and the run ends.
- Reaching the target score wins the run.

## Difficulty & Maps

| Difficulty | Target score | Start length |
|---|---|---|
| Easy | 20% of all cells | 1 segment |
| Normal | 30% of all cells | 3 segments |
| Hard | 50% of all cells | 5 segments |

| Map | Food | Easy / Normal / Hard target |
|---|---|---|
| 3×3×3 | 1 | 5 / 8 / 14 |
| 5×5×5 | 2 | 25 / 38 / 63 |
| 7×7×7 | 3 | 69 / 103 / 172 |

## Features

- **Grid logic, smooth visuals** — the simulation steps one cell every 0.5 s while the renderer interpolates between cells; turns are only accepted on grid points.
- **A body that fills its path** — every visited cell has solid geometry, with extra filler blocks at corners, so the snake never shows gaps.
- **Fully procedural audio** — a 16-second looping soundtrack (chords, bass, melody, percussion) and every sound effect are synthesised at runtime. No audio assets in the repo.
- **Two camera modes** — a fixed third-person view that orbits the head on a circle (the head stays centred), or a free orbit view around the map.
- **Real-time lighting** — a directional light that follows the camera, a point light attached to the head, fog, soft shadows.
- **Layer highlight** — the horizontal layer containing the head is outlined with translucent blocks and a wireframe box, making height easy to judge.
- **Leaderboard**: one board per difficulty + map, storing nickname, score and run time; a name keeps only its best run. Data is stored locally, with a hook for a cloud backend.
- Settings (volumes, speed, camera responsiveness, view mode) persist through `PlayerPrefs`.

## Opening in Unity

1. Install **Unity 2022.3 LTS** (developed on 2022.3.62f3c1).
2. Unity Hub → Add → select this folder.
3. **The first open takes 1–2 minutes** — Unity generates `Library/` and imports assets. This is expected.
4. Open `Assets/Scenes/Game.unity`.
5. Press Play.

The scene is already wired up:

```
Main Camera   + CameraOrbit + LightFollowCamera + AudioListener
Directional Light
Game          + SnakeGame + MenuUI + AudioManager
```

## Building for Windows

From the editor menu:

```
Snake → Build Windows
```

Or headless, with the editor closed:

```bash
Unity.exe -batchmode -quit -projectPath "<repo path>" -executeMethod SnakeBuild.BuildWindows
```

Output goes to `Builds/Win64/Snake3D.exe`. `Snake → Setup Scene` regenerates a fully configured scene.

## Project layout

```
Assets/
├── Snake/
│   ├── SnakeGame.cs          Core: grid movement, collision, rendering, lighting, layer highlight
│   ├── MenuUI.cs             Code-built UI: menu, difficulty, map, settings, pause, results
│   ├── CameraOrbit.cs        Fixed and free camera modes
│   ├── LightFollowCamera.cs  Directional light that tracks the camera
│   ├── AudioManager.cs       Procedural music and sound effects
│   ├── GameSettings.cs       PlayerPrefs-backed settings
│   └── SliderReleaseSound.cs Plays a preview when a slider is released
├── Editor/SnakeBuild.cs      One-click scene setup + player build
└── Scenes/Game.unity         Ready-to-play scene
```

## License

MIT
