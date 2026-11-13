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
5. Press **Play**, allow webcam access, and hold both hands in front of the camera.

Unity does not restore a scene on first open, so a fresh clone starts on an empty
untitled scene. Open the scene above manually.

The cooking game bootstraps itself at runtime in `CookingGameScene`, so no prefab or
scene object has to be added. Everything (UI, prep board, stations, ingredient props,
audio and effects) is generated from code. The in-game UI is in English and uses a
dynamically chosen OS font; on a minimal Linux install, make sure a common desktop font
package is present if labels show as boxes.

### Other scenes

- `Assets/Scenes/HandTracking/Hand Landmark Detection.unity` — MediaPipe hand landmark
  demo, useful for checking that the webcam and the native plugin work.

## Cooking game

The recipe menu appears as soon as the scene starts. Pick a recipe with the mouse or by
pinching, then follow the instruction banner at the top of the screen. A small ring
follows the tracked hand so the pinch point is visible, a pinch near a button counts as a
click, and a pinch anywhere inside the menu panel chooses the nearest recipe card, so
aiming at a card pixel does not matter. The tip line
under the title shows live hand-tracking status (both hands, one hand, or none detected),
so a menu that cannot be pinched says why. Menu buttons are activated directly from the
hit test instead of relying on the EventSystem, and mouse clicks fall back to the same
path, so the menu keeps working even when the input module does not. Every recipe now
follows a short cooking-game loop modelled on games such as Overcooked: **place whole
ingredients on the cutting board → chop them with the knife → carry them to the pot or
bowl → stir/cook → serve the finished dish onto the plate**. The instructions name each
stage, and every step has a single highlighted ingredient or tool and a highlighted target
station (cutting board, pot, mixing bowl, microwave or plate).

Once you pick an item up, the target station's ring grows and pulses faster to show where
it goes — that single ring is the whole placement hint. A release within 0.15 s
of grabbing is treated as an accidental pinch flick (seasoning and pouring use gentle
handling instead), and a hand that leaves the camera returns the item instead of placing
it, so items can no longer be placed before the player actually lets go.

On the cutting board, pinch the knife, lift it and chop up and down: each full down-and-up
stroke counts. The ingredient turns into a heap of chopped pieces on the board and can be
carried on to the next station. Placed ingredients do not disappear. They stay in the
bowl, plate or pot as chopped pieces, scattered sprinkles or coloured liquid layers, and
stirring swirls them around, so the dish visibly builds up from step to step. The whole
cooking set — pot and microwave included — is built on the prep board, so every target
sits in the same reachable area and a dropped ingredient always lands in the middle of
the pot; for a Heat step, hold an open hand over the pot or microwave until the timer
fills. Everything is cleared when a new recipe starts. After the last step (or after the
Serve step), the finished dish is served on the plate — garnish, steam and a floating
"DISH READY!" label — and the pot, bowl, microwave and cutting board are emptied, so only
the plated dish remains. The dish stays for a few seconds before the results panel opens.
While a recipe runs the camera keeps the authored kitchen framing; the kitchen behind the
board is scenery, and the tighter view used to push scene objects over the screen edges.
The hands are drawn at 0.7× their camera-image size, and a small aim ring follows each
tracked hand, marking the exact point that grabbing and station checks use — the ring is
what the player moves onto an ingredient.

| Gesture | How | Used for |
|---|---|---|
| Pinch | Touch thumb and index finger together | Grab an ingredient or the knife, click UI buttons |
| Release | Separate thumb and index finger | Drop an ingredient over the target station |
| Chop | Hold the knife, raise it over the cutting board, then move down and up | One chop stroke per full movement |
| Sprinkle | Circle one hand over the highlighted station — the fingers may stay relaxed or half closed | Seasoning: one turn per full revolution, nothing to grab |
| Pour | While pinching, tilt the wrist about 22° outward | Hold over the station until the pour timer fills |
| Stir | Circle one hand over the station | Complete one revolution (about 250° of travel within 6 s) |
| Heat | Hold an open hand over the microwave or pot | Fill the heating timer |

Pinch recognition uses hysteresis, and gestures must remain stable for a short time, so
small tracking jitters do not trigger grabs or drops. Missing detections are tolerated
for 0.32 s; a held ingredient survives 0.7 s without tracking (enough to recover from a
short dropout while carrying), and losing tracking never counts as a mistake. Circling
counts even if the fingers are half closed, and the progress bar fills with the current
circle instead of only jumping after a whole turn.

Hands are mirrored on purpose: the hand on the **left side of the screen** is treated
as the left hand, even though MediaPipe labels it "Right" because the camera image is
mirrored. Some hard steps require a specific hand.

### Recipes and difficulty

| Recipe | Difficulty | Main training |
|---|---|---|
| Cucumber Salad | Easy | Chop, carry, sprinkle, pour, stir, serve |
| Ethiopian Salata | Easy | Chopping every vegetable, reach, pour, stir, serve |
| Tomato and Egg Stir-fry | Medium | Chop tomato and spring onion, precise pour, 4 stirs, serve |
| Steamed Tofu with Soy Sauce | Medium | Chopping tofu and spring onion, microwave heating, precise placement, two slow pours |
| Shiro Wat | Hard | Chop, sprinkle, two pours, 5 stirs, serve |
| Doro Wat | Hard | Alternating left/right hands, chopping every ingredient, 2 stirs, serve |

Difficulty changes more than the recipe length: the grab tolerance is larger for easy
recipes (1.25×) and slightly tighter for hard ones (0.9×), hard recipes require longer pours and
more stirring, and Doro Wat assigns specific hands to each step. The results screen
records time, grabs, stir revolutions, pouring time, mistakes and a 1–3 star rating.

### Gesture design notes

- Pinch enters at a thumb–index distance of 0.38 × palm width and exits at 0.58 × palm
  width (hysteresis).
- Pour is measured relative to the palm normal captured when the pinch starts, with a
  gentle 22° start / 12° stop threshold. The baseline survives a one-frame pinch
  flicker, so the wrist tilt does not reset mid-pour. The pour target uses a wider
  screen-space circle than other drops, and setting the bottle down (anywhere) keeps
  the pour progress and never counts as a mistake.
- Chopping is measured as hand travel in screen space, relative to the hand's own
  starting point: the hand must first move up by about 2 % of the screen height, then
  down by about 2.5 %; the next chop requires another raise. This prevents grabbing the
  knife from counting as a chop and keeps the motion usable regardless of where the
  board sits in the room.
- Seasoning works like stirring, not like dropping: the target station's ring pulses,
  and circling an open hand over it sprinkles the seasoning. One full revolution
  counts as one turn, and the step completes automatically after 3 turns (4 for the
  chickpea flour in Shiro Wat). The shaker stays on the board untouched, so a shaky
  pinch can never stall a seasoning step.
- Stir is a signed sweep-angle detector over a sliding 6-second window; straight or
  back-and-forth movement cannot complete a revolution.
- Grabbing uses the 3D pinch point, while station targeting uses screen-space distance
  so a patient does not have to reach the physical pot several metres away.

The Unity XRI / VR templates were not used: they expect controller or tracked-hand
input, while this project receives webcam landmarks from MediaPipe and drives its own
rigged hand models. The gameplay layer is therefore built directly on the existing
MediaPipe rig with runtime UGUI.

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
  Scripts/Cooking/      CookingRehab gameplay (runtime-built, no prefabs)
    Core/               Types, ingredient catalog, recipe catalog
    Gesture/            HandGestureRecognizer
    Gameplay/           Controller, world, stations, ingredients
    UI/                 Runtime UGUI factory, HUD, world labels
    Visuals/            Materials, ingredient props, effects, audio
Packages/
  com.github.homuler.mediapipe/              embedded MediaPipe plugin
  com.unity.render-pipelines.high-definition/ embedded HDRP 17.6.0
```

Both package folders are **embedded**, so they are committed in full and Unity resolves
them from disk rather than the registry. They are not listed in `Packages/manifest.json`.

## Recipes

The in-game recipe data lives in
[`Assets/Scripts/Cooking/Core/RecipeDefinition.cs`](Assets/Scripts/Cooking/Core/RecipeDefinition.cs).
The original recipe and rehabilitation-motion notes are collected in
[`recipes.md`](recipes.md).
