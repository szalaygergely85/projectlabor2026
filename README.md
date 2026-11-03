# Projektlabor 2026

Unity project for a hand-tracking cooking/rehabilitation game. Hand input comes from
the [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin); rendering
uses HDRP.

## Requirements

| | |
|---|---|
| Unity | **6000.6.5f1** (exactly — see `ProjectSettings/ProjectVersion.txt`) |
| Platform | Windows / macOS / Linux desktop |
| Hardware | A webcam, for hand tracking |

Opening the project in a different Unity 6000.x version triggers an asset upgrade and
reserialization, which produces large, noisy diffs. Install the matching version from
Unity Hub first.

## Getting started

1. Clone the repository.
2. Open **Unity Hub → Add → Add project from disk** and select the cloned folder.
3. Open the project. The first import takes a while — it builds the HDRP shader cache
   and imports the MediaPipe models.
4. Open the scene **`Assets/Scenes/CookingGameScene.unity`**.

Unity does not restore a scene on first open, so a fresh clone starts on an empty
untitled scene. Open the scene above manually.

### Other scenes

- `Assets/Scenes/HandTracking/Hand Landmark Detection.unity` — MediaPipe hand landmark
  demo, useful for checking that the webcam and the native plugin work.

## Large files

Models, native plugins and textures are stored with **Git LFS**. If Unity reports
missing DLLs or corrupt assets, the LFS content probably was not fetched — run:

```
git lfs pull
```

## Project layout

```
Assets/
  Scenes/               CookingGameScene (main), HandTracking demo
  Prefabs/              House, MediaPipe UI and hand-tracking prefabs
  ImportedAssets/       Third-party art (VR hands, skybox)
  HDRPDefaultResources/ HDRP pipeline asset and global settings
  Settings/             URP assets, kept from before the HDRP switch
Packages/
  com.github.homuler.mediapipe/              embedded MediaPipe plugin
  com.unity.render-pipelines.high-definition/ embedded HDRP 17.6.0
```

Both package folders are **embedded**, so they are committed in full and Unity resolves
them from disk rather than the registry. They are not listed in `Packages/manifest.json`.

## Recipes

Gameplay recipes and the hand motions they exercise are collected in
[`recipes.md`](recipes.md).
