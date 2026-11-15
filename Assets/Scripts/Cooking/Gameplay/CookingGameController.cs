using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CookingRehab
{
    /// <summary>
    /// Drives a cooking session: grabs the current ingredient with a pinch,
    /// carries it with the tracked hand, validates drops, counts pouring time
    /// and stirring revolutions, then shows the results screen.
    /// </summary>
    public class CookingGameController : MonoBehaviour
    {
        public static CookingGameController Instance { get; private set; }

        public Camera sceneCamera;
        public MediaPipeVRHand handTracker;
        public HandGestureRecognizer gestures;
        public CookingWorld world;
        public CookingUI ui;

        private CookingAudio _audio;
        private RecipeDefinition _recipe;
        private RecipeStep _step;
        private CookingStation _activeStation;
        private int _stepIndex;

        private CookingIngredient _held;
        private HandSide _heldSide;
        private Vector3 _grabOffset;
        private Quaternion _grabRotationOffset;
        private float _grabScale = 1f;

        // A pinch release is not dropped the same frame: pinch tracking can
        // flicker for a moment while the hand crosses the camera image, and
        // dropping the prop there is what made the pot steps feel impossible.
        private bool _dropPending;
        private HandSide _dropSide;
        private float _dropAt;
        private bool _dropForceReturn;

        private LiquidStream _pourStream;
        private float _pourAccum;
        private float _pourHoldSeconds;
        private float _heldSince;
        private int _chopCount;
        private bool _chopArmed;
        private float _chopUpTravel;
        private float _chopDownTravel;
        private float _chopLastViewportY;
        private bool _chopHasViewportY;
        private int _sprinkleCount;
        private int _stirCount;
        private int _stirTotal;
        private int _grabCount;
        private int _wrongDrops;
        private float _elapsed;
        private float _advanceAt = -1f;
        private float _resultsAt = -1f;
        private float _difficultyScale = 1f;

        // A pinch during a Sprinkle or Stir step does nothing, but players
        // keep closing their fingers over the bowl. Explain it once in a
        // while instead of silently ignoring them.
        private float _lastPinchHintAt = -10f;

        // Microwave / pot heating (a Heat step): an open hand held over the
        // station fills the timer.
        private float _heatAccum;
        private bool _heatAwayHintShown;

        // The authored kitchen framing is kept for cooking as well as for the
        // menu: the prep board now carries every station, so the view must
        // show the whole board rather than zoom into a part of the kitchen
        // (see Awake).
        private float _menuFov = 60f;
        private float _cookingFov = 60f;

        private bool _cooking;
        private bool _stepComplete;
        private bool _finished;
        private bool _started;

        // Menu pinch fallback driven by MediaPipeVRHand's own pinch state.
        // The gesture recognizer normally publishes PinchStarted, but the
        // menu must keep working even if that event is somehow missed.
        private bool _menuPinchFallbackArmed;
        private bool _menuLeftPinch;
        private bool _menuRightPinch;

        // A release this soon after the pinch grabbed the prop is treated as
        // an accidental flick, not as a deliberate placement.
        private const float MinCarrySeconds = 0.15f;

        private float GrabRadius
        {
            get { return 0.26f * _difficultyScale; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (SceneManager.GetActiveScene().name != "CookingGameScene")
                return;

            if (Object.FindAnyObjectByType<CookingGameController>() != null)
                return;

            GameObject gameObject = new GameObject("Cooking Game");
            gameObject.AddComponent<CookingGameController>();
            Debug.Log("[Cooking] Auto-booted the cooking game controller.");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (_started)
                return;

            _started = true;
            ResolveReferences();
            if (sceneCamera == null)
            {
                Debug.LogError("[Cooking] No scene camera found. Cooking game disabled.");
                enabled = false;
                return;
            }

            // Keep the authored framing while cooking. The prep board holds
            // every station from the pot on the left to the microwave on
            // the right; zooming in used to push the outer stations over
            // the screen edges, where a webcam hand cannot reach them.
            _menuFov = sceneCamera.fieldOfView;
            _cookingFov = _menuFov;

            ui = GetComponent<CookingUI>();
            if (ui == null)
                ui = gameObject.AddComponent<CookingUI>();
            ui.Build(sceneCamera, this);

            float depth = ResolveInteractionDepth();
            if (handTracker != null)
                handTracker.SetBoardDepth(depth);

            world = GetComponent<CookingWorld>();
            if (world == null)
                world = gameObject.AddComponent<CookingWorld>();
            world.Build(sceneCamera, depth, ui.LabelCanvas);
            world.SetVisible(false);

            _audio = GetComponent<CookingAudio>();
            if (_audio == null)
                _audio = gameObject.AddComponent<CookingAudio>();

            if (gestures != null)
            {
                gestures.PinchStarted += OnPinchStarted;
                gestures.PinchReleased += OnPinchReleased;
                gestures.StirRevolutionCompleted += OnStirRevolution;
            }
            else
            {
                Debug.LogWarning("[Cooking] Hand tracking was not found. " +
                                 "The menu still works with the mouse.");
            }

            ui.ShowMenu();
        }

        private void ResolveReferences()
        {
            if (handTracker == null)
                handTracker = Object.FindAnyObjectByType<MediaPipeVRHand>();

            if (sceneCamera == null && handTracker != null)
                sceneCamera = handTracker.sceneCamera;
            if (sceneCamera == null)
                sceneCamera = Camera.main;
            if (sceneCamera == null)
                sceneCamera = Object.FindAnyObjectByType<Camera>();

            if (handTracker == null)
                return;

            gestures = handTracker.gestureRecognizer;
            if (gestures == null)
                gestures = handTracker.GetComponent<HandGestureRecognizer>();
            if (gestures == null)
                gestures = handTracker.gameObject.AddComponent<HandGestureRecognizer>();

            Camera trackerCamera = handTracker.sceneCamera != null
                ? handTracker.sceneCamera
                : sceneCamera;
            gestures.Initialize(trackerCamera, handTracker);
        }

        private float ResolveInteractionDepth()
        {
            float depth = 1.07f;

            if (handTracker == null || sceneCamera == null)
                return depth;

            float leftDepth = HandDepth(handTracker.leftHand);
            float rightDepth = HandDepth(handTracker.rightHand);

            if (leftDepth > 0.3f && rightDepth > 0.3f)
                depth = (leftDepth + rightDepth) * 0.5f;
            else if (leftDepth > 0.3f)
                depth = leftDepth;
            else if (rightDepth > 0.3f)
                depth = rightDepth;

            return Mathf.Clamp(depth, 0.75f, 1.65f);
        }

        private float HandDepth(MediaPipeVRHand.HandRig hand)
        {
            if (hand == null || hand.handRoot == null)
                return -1f;

            float depth = sceneCamera.WorldToViewportPoint(hand.handRoot.position).z;
            return depth > 0f ? depth : -1f;
        }

        private void Update()
        {
            UpdateCameraFocus();

            if (gestures != null && ui != null)
                ui.RefreshHands(gestures.LeftState, gestures.RightState);

            UpdateMenuPinchFallback();

            if (_finished)
            {
                if (Time.time >= _resultsAt)
                {
                    _finished = false;
                    _resultsAt = -1f;
                    ShowResultsScreen();
                }
                return;
            }

            if (!_cooking)
                return;

            _elapsed += Time.deltaTime;
            if (ui != null)
                ui.SetTimer(_elapsed);

            if (_stepComplete)
            {
                if (Time.time >= _advanceAt)
                    AdvanceStep();
                return;
            }

            if (_step != null && _step.action == CookingActionType.Heat)
                UpdateHeating();

            UpdateCirclePreview();
            UpdateHeldIngredient();
            UpdatePendingDrop();
            UpdateAutoGrab();
        }

        /// <summary>
        /// Eases the camera to the target field of view. Menu and cooking
        /// use the authored framing, so this normally does nothing; it is
        /// kept so a changed field of view still blends in smoothly instead
        /// of snapping.
        /// </summary>
        private void UpdateCameraFocus()
        {
            if (sceneCamera == null)
                return;

            float target = (_cooking || _finished) ? _cookingFov : _menuFov;
            float current = sceneCamera.fieldOfView;
            if (Mathf.Abs(current - target) < 0.01f)
            {
                sceneCamera.fieldOfView = target;
                return;
            }

            float t = 1f - Mathf.Exp(-5f * Time.deltaTime);
            sceneCamera.fieldOfView = Mathf.Lerp(current, target, t);
        }

        // ------------------------------------------------------------------
        // Gesture events
        // ------------------------------------------------------------------

        private void OnPinchStarted(HandSide side)
        {
            if (gestures == null || ui == null)
                return;

            HandGestureState state = gestures.GetState(side);
            Debug.Log("[Cooking] PinchStarted " + side + " (cooking=" + _cooking +
                      ", tracked=" + state.isTracked + ")");

            // The carrying hand closed again during the short drop grace:
            // the release was a tracking flicker, not a drop.
            if (_held != null && side == _heldSide)
            {
                _dropPending = false;
                return;
            }

            // Stirring, seasoning and heating need no grab at all: stirring
            // and seasoning are done with an open hand circling over the
            // station, and heating is an open hand held over it. A pinch in
            // those steps only clicks the UI.
            if (!_cooking || _stepComplete || _step == null ||
                _step.action == CookingActionType.Stir ||
                _step.action == CookingActionType.Sprinkle ||
                _step.action == CookingActionType.Heat)
            {
                // Seasoning and stirring need no pinch. Say so instead of
                // just ignoring the gesture; players were pinching at the
                // bowl over and over, expecting the seasoning to be grabbed.
                if (_cooking && !_stepComplete && _step != null &&
                    (_step.action == CookingActionType.Sprinkle ||
                     _step.action == CookingActionType.Stir) &&
                    Time.unscaledTime - _lastPinchHintAt > 6f)
                {
                    _lastPinchHintAt = Time.unscaledTime;
                    ui.ShowToast(
                        "No need to pinch — circle your open hand over the " +
                        CookingNames.StationZh(_step.stationId),
                        CookingTheme.Accent);
                }

                ui.HandlePinchClick(state);
                return;
            }

            bool grabbed = TryGrab(side);
            if (!grabbed)
            {
                bool clicked = ui.HandlePinchClick(state);
                if (!clicked && _step != null &&
                    _step.action == CookingActionType.Chop)
                {
                    ui.ShowToast("Pinch the knife on the cutting board",
                        CookingTheme.Accent);
                }
            }
        }

        private void OnPinchReleased(HandSide side)
        {
            if (!_cooking || _held == null || side != _heldSide)
                return;

            // Give the pinch a short grace period instead of dropping the
            // prop on the same frame. If the fingers close again, the release
            // was tracking noise and the carry simply continues; if the hand
            // was over the target station, UpdatePendingDrop places the prop
            // immediately, so a deliberate drop still feels instant.
            ScheduleDrop(0.18f, false);
        }

        /// <summary>
        /// Extra menu input path on top of HandGestureRecognizer's
        /// PinchStarted: MediaPipeVRHand already tracks its own pinch state,
        /// and the menu must answer a pinch even if the gesture event is
        /// missed. It is deliberately limited to the menu / results screens
        /// and armed only after the cooking screen closes, so a pinch that is
        /// still held when returning from a recipe cannot start the next one.
        /// </summary>
        private void UpdateMenuPinchFallback()
        {
            if (handTracker == null || gestures == null || ui == null || _cooking ||
                (!ui.IsMenuVisible && !ui.IsResultsVisible))
            {
                _menuPinchFallbackArmed = false;
                _menuLeftPinch = false;
                _menuRightPinch = false;
                return;
            }

            bool left = handTracker.leftHand != null &&
                        handTracker.leftHand.isPinching;
            bool right = handTracker.rightHand != null &&
                         handTracker.rightHand.isPinching;

            if (!_menuPinchFallbackArmed)
            {
                // Remember whatever is already held without clicking it.
                _menuLeftPinch = left;
                _menuRightPinch = right;
                _menuPinchFallbackArmed = true;
                return;
            }

            if (left && !_menuLeftPinch)
                ui.HandlePinchClick(gestures.GetState(HandSide.Left));
            if (right && !_menuRightPinch)
                ui.HandlePinchClick(gestures.GetState(HandSide.Right));

            _menuLeftPinch = left;
            _menuRightPinch = right;
        }

        private void OnStirRevolution(HandSide side, int totalRevolutions)
        {
            if (!_cooking || _stepComplete || _step == null)
                return;

            bool stirStep = _step.action == CookingActionType.Stir;
            bool sprinkleStep = _step.action == CookingActionType.Sprinkle;
            if (!stirStep && !sprinkleStep)
                return;
            if (!HandMatches(side, _step.requiredHand))
                return;

            HandGestureState state = gestures.GetState(side);
            if (!IsHandOverStation(_activeStation, state, true, 1.35f))
                return;

            if (sprinkleStep)
            {
                RegisterSprinkleTurn();
                return;
            }

            _stirCount++;
            _stirTotal++;

            float fill = Mathf.Clamp01((float)_stirCount /
                                       Mathf.Max(1, _step.requiredRevolutions));
            if (ui != null)
            {
                ui.SetProgress(fill, _stirCount + " / " +
                                     _step.requiredRevolutions + " turns");
            }

            if (_audio != null)
                _audio.PlayStir();

            if (_activeStation != null)
            {
                _activeStation.StirContents();
                CookingEffects.Burst(_activeStation.AnchorPosition,
                    _activeStation.activeColor, 7, 0.5f);
            }

            if (_stirCount >= _step.requiredRevolutions)
            {
                CompleteStep();
            }
            else if (_stirCount == 1 && ui != null)
            {
                ui.ShowToast("Nice — keep circling", CookingTheme.Success);
            }
        }

        // ------------------------------------------------------------------
        // Grabbing and dropping
        // ------------------------------------------------------------------

        private bool TryGrab(HandSide side)
        {
            if (_held != null || gestures == null || world == null)
                return false;

            // Chop steps grab the knife, not the food; Serve steps grab the
            // dish that was lifted out of the pot or bowl.
            CookingIngredient ingredient;
            if (_step.action == CookingActionType.Chop)
                ingredient = world.FindIngredient("knife");
            else if (_step.action == CookingActionType.Serve)
                ingredient = world.FindIngredient("dish");
            else
                ingredient = FindTargetIngredient();

            if (ingredient == null)
                return false;

            HandGestureState state = gestures.GetState(side);
            if (!state.isTracked)
                return false;

            Vector3 itemPoint = ingredient.transform.position + Vector3.up * 0.07f;
            float radius = GrabRadius;

            // The hand is drawn on a plane well in front of the board so it is
            // never hidden behind the props, so the raw 3D distance to an
            // ingredient is usually more than half a metre even when the two
            // overlap on screen. Only props already on the hand plane can pass
            // this test; everything else is matched on screen below.
            bool closeInWorld = (state.pinchWorldPosition - itemPoint).sqrMagnitude <=
                                radius * radius;

            Vector3 itemViewport = sceneCamera != null
                ? sceneCamera.WorldToViewportPoint(itemPoint)
                : Vector3.zero;
            bool closeOnScreen = false;
            if (sceneCamera != null && itemViewport.z > 0f)
            {
                // Aim with what the player can actually see: the raw landmark
                // pinch, the rendered pinch bone and the wrist all count, so
                // the grab area lines up with the hand model instead of a
                // single landmark that may sit slightly off it. The radius is
                // generous because this is a rehab exercise, not a precision
                // test; the knife keeps a wider area because its grab point
                // sits above the visible blade.
                float screenRadius =
                    (_step.action == CookingActionType.Chop ? 0.23f : 0.17f) *
                    Mathf.Clamp(_difficultyScale, 0.9f, 1.25f);
                Vector2 itemPoint2D = new Vector2(itemViewport.x, itemViewport.y);
                // The whole visible hand counts, not only the exact pinch
                // point: the rendered hand can be larger than the target, and
                // a player who covers the ingredient with the palm expects
                // the grab to happen.
                closeOnScreen =
                    Vector2.Distance(state.viewportPosition, itemPoint2D) <= screenRadius ||
                    Vector2.Distance(state.palmViewport, itemPoint2D) <= screenRadius ||
                    Vector2.Distance(ProjectToViewport(state.pinchWorldPosition),
                        itemPoint2D) <= screenRadius ||
                    Vector2.Distance(ProjectToViewport(state.worldPosition),
                        itemPoint2D) <= screenRadius;

                // As a last resort, a pinch anywhere over the station where
                // the item lives still counts. The current step names the
                // ingredient, so the gesture is unambiguous, and it means a
                // small aiming error can never leave the step stuck.
                if (!closeOnScreen)
                {
                    CookingStation home = GrabHomeStation();
                    if (home != null)
                    {
                        // Use the same on-screen point as the station tests:
                        // the pot's rim anchor projects off the left edge, so
                        // the raw projection would leave the whole pot area
                        // untouchable for Serve and ingredient grabs.
                        Vector2 homePoint2D = StationViewportPoint(
                            home.AnchorPosition);
                        float homeRadius = Mathf.Clamp(
                            home.viewportRadius * 2.1f, 0.20f, 0.32f);
                        closeOnScreen =
                            Vector2.Distance(state.viewportPosition, homePoint2D) <= homeRadius ||
                            Vector2.Distance(state.palmViewport, homePoint2D) <= homeRadius ||
                            Vector2.Distance(ProjectToViewport(state.pinchWorldPosition),
                                homePoint2D) <= homeRadius ||
                            Vector2.Distance(ProjectToViewport(state.worldPosition),
                                homePoint2D) <= homeRadius;
                    }
                }
            }

            if (!closeInWorld && !closeOnScreen)
                return false;

            if (!HandMatches(side, _step.requiredHand))
            {
                if (ui != null)
                {
                    ui.ShowToast("This step needs " +
                                 CookingNames.HandZh(_step.requiredHand),
                                 CookingTheme.Danger);
                }
                return false;
            }

            _held = ingredient;
            _heldSide = side;
            _dropPending = false;
            ingredient.BeginGrab();

            if (_step.action == CookingActionType.Chop)
                ResetChopTracking();

            // The prep board sits behind the hand plane so the hand is always
            // visible. Move the picked-up prop onto the hand's depth plane,
            // keeping its current screen position, so it follows the pinch
            // one-to-one instead of trailing behind the hand.
            Vector3 handPoint = state.pinchWorldPosition;
            Vector3 handViewport = sceneCamera != null
                ? sceneCamera.WorldToViewportPoint(handPoint)
                : Vector3.zero;
            if (sceneCamera != null && itemViewport.z > 0f && handViewport.z > 0f)
            {
                Vector3 itemOnHandPlane = sceneCamera.ViewportToWorldPoint(
                    new Vector3(itemViewport.x, itemViewport.y, handViewport.z));
                _grabOffset = itemOnHandPlane - handPoint;
            }
            else
            {
                _grabOffset = ingredient.transform.position - handPoint;
            }

            _grabRotationOffset = Quaternion.Inverse(state.handRotation) *
                                  ingredient.transform.rotation;

            // Keep the prop looking the same size when it jumps from the
            // board depth to the hand depth, then settle slightly smaller.
            float depthRatio = itemViewport.z > 0f && handViewport.z > 0f
                ? handViewport.z / itemViewport.z
                : 1f;
            _grabScale = ingredient.homeScale * depthRatio;
            ingredient.transform.localScale = Vector3.one * _grabScale;
            _heldSince = Time.unscaledTime;
            _grabCount++;

            if (_audio != null)
                _audio.PlayPick();

            if (world != null)
                world.ShowPlacementHint(_activeStation);

            UpdateProgress();
            return true;
        }

        /// <summary>
        /// Projects a hand anchor to viewport space. Points behind the camera
        /// return a position far away so they never match a grab radius.
        /// </summary>
        private Vector2 ProjectToViewport(Vector3 worldPoint)
        {
            if (sceneCamera == null)
                return new Vector2(-10f, -10f);

            Vector3 viewport = sceneCamera.WorldToViewportPoint(worldPoint);
            return viewport.z > 0f
                ? new Vector2(viewport.x, viewport.y)
                : new Vector2(-10f, -10f);
        }

        /// <summary>
        /// Where the item wanted by the current step waits when it is not
        /// carried: the cutting board holds the food and the knife, while a
        /// finished dish waits on the station it was cooked in.
        /// </summary>
        private CookingStation GrabHomeStation()
        {
            if (world == null || _step == null)
                return null;

            if (_step.action == CookingActionType.Serve)
            {
                CookingStation source = world.GetStation(_step.sourceStationId);
                if (source != null)
                    return source;
            }

            return world.boardStation;
        }

        /// <summary>
        /// A pinch that started away from the prop still picks it up once the
        /// hand moves onto it. Patients often pinch first and then reach, and
        /// before this the pinch was only sampled at its very first moment, so
        /// moving forward with a closed pinch did nothing at all.
        /// </summary>
        private void UpdateAutoGrab()
        {
            if (!_cooking || _stepComplete || _step == null || _held != null ||
                gestures == null)
            {
                return;
            }

            // Stirring, seasoning and heating use an open hand over the
            // station, so a held pinch must not snatch a prop there.
            if (_step.action == CookingActionType.Stir ||
                _step.action == CookingActionType.Sprinkle ||
                _step.action == CookingActionType.Heat)
            {
                return;
            }

            if (TryAutoGrab(HandSide.Left))
                return;

            TryAutoGrab(HandSide.Right);
        }

        private bool TryAutoGrab(HandSide side)
        {
            // Check the required hand first: this runs every frame while a
            // pinch is held, and the wrong-hand toast is already shown once
            // when the pinch starts.
            if (!HandMatches(side, _step.requiredHand))
                return false;

            HandGestureState state = gestures.GetState(side);
            if (!state.isTracked || !state.isPinching)
                return false;

            return TryGrab(side);
        }

        private CookingIngredient FindTargetIngredient()
        {
            if (world == null || _step == null || !_step.NeedsIngredient)
                return null;

            IReadOnlyList<CookingIngredient> ingredients = world.Ingredients;
            for (int i = 0; i < ingredients.Count; i++)
            {
                CookingIngredient ingredient = ingredients[i];
                if (ingredient == null || ingredient.isConsumed)
                    continue;
                if (ingredient.ingredientId == _step.ingredientId)
                    return ingredient;
            }

            return null;
        }

        /// <summary>
        /// A Heat step (the microwave, or a pot simmer) completes while an
        /// open hand stays over the station. Holding still is the whole
        /// gesture, so it also works for patients who cannot pinch reliably.
        /// </summary>
        private void UpdateHeating()
        {
            if (_step == null || _activeStation == null || gestures == null ||
                ui == null)
                return;

            HandGestureState left = gestures.GetState(HandSide.Left);
            HandGestureState right = gestures.GetState(HandSide.Right);

            bool over = IsHandOverStation(_activeStation, left, true) ||
                        IsHandOverStation(_activeStation, right, true);

            float required = Mathf.Max(0.1f, _step.requiredHeatSeconds);

            if (!over)
            {
                if (!_heatAwayHintShown)
                {
                    _heatAwayHintShown = true;
                    ui.SetProgress(Mathf.Clamp01(_heatAccum / required),
                        "Hold your open hand over the " +
                        CookingNames.StationZh(_step.stationId));
                }
                return;
            }

            if (_heatAwayHintShown)
            {
                _heatAwayHintShown = false;
                UpdateProgress();
            }

            _heatAccum += Time.deltaTime;

            ui.SetProgress(Mathf.Clamp01(_heatAccum / required),
                _heatAccum.ToString("F1") + " / " +
                required.ToString("F1") + " s");

            // A little warmth every half second, so the station visibly
            // works while the hand stays over it.
            if (Mathf.Repeat(_heatAccum, 0.5f) < Time.deltaTime)
            {
                CookingEffects.Burst(_activeStation.AnchorPosition,
                    new Color(1f, 0.72f, 0.32f, 1f), 4, 0.3f);
            }

            if (_heatAccum >= required)
                CompleteStep();
        }

        /// <summary>
        /// Shows the part of the current circle that has already been drawn.
        /// Before this the counter only jumped when a whole turn completed,
        /// so a slow circle looked as if the game was not seeing the hand at
        /// all. The progress bar now moves with the palm.
        /// </summary>
        private void UpdateCirclePreview()
        {
            if (!_cooking || _stepComplete || _step == null ||
                gestures == null || ui == null)
                return;

            bool sprinkle = _step.action == CookingActionType.Sprinkle;
            bool stir = _step.action == CookingActionType.Stir;
            if (!sprinkle && !stir)
                return;

            float bestProgress = 0f;
            for (int i = 0; i < 2; i++)
            {
                HandSide side = i == 0 ? HandSide.Left : HandSide.Right;
                if (!HandMatches(side, _step.requiredHand))
                    continue;

                HandGestureState state = gestures.GetState(side);
                if (!state.isTracked ||
                    !IsHandOverStation(_activeStation, state, true, 1.35f))
                    continue;

                if (state.stirProgress > bestProgress)
                    bestProgress = state.stirProgress;
            }

            if (bestProgress <= 0.03f)
                return;

            int required = sprinkle
                ? Mathf.Max(1, _step.requiredSprinkleTurns)
                : Mathf.Max(1, _step.requiredRevolutions);
            int count = sprinkle ? _sprinkleCount : _stirCount;

            ui.SetProgress(
                Mathf.Clamp01((count + bestProgress) / required),
                count + " / " + required + " turns");
        }

        private void UpdateHeldIngredient()
        {
            if (_held == null || gestures == null || _step == null)
                return;

            HandGestureState state = gestures.GetState(_heldSide);
            if (!state.isTracked)
            {
                // Keep the prop in hand for a moment: tracking often drops
                // for a fraction of a second while the hand crosses the edge
                // of the webcam image, and a short dropout must not undo a
                // careful carry. 0.7 s is enough for the hand to come back
                // into view after reaching for the microwave.
                if (!_dropPending)
                    ScheduleDrop(0.7f, true);
                return;
            }

            float dt = Time.deltaTime;
            float follow = 1f - Mathf.Exp(-26f * dt);

            Vector3 targetPosition = state.pinchWorldPosition + _grabOffset;
            _held.transform.position = Vector3.Lerp(
                _held.transform.position, targetPosition, follow);

            Quaternion targetRotation = state.handRotation * _grabRotationOffset;
            _held.transform.rotation = Quaternion.Slerp(
                _held.transform.rotation, targetRotation, follow);

            float targetScale = _grabScale * 0.82f;
            _held.transform.localScale = Vector3.Lerp(_held.transform.localScale,
                Vector3.one * targetScale, 1f - Mathf.Exp(-14f * dt));

            if (_step.action == CookingActionType.Chop)
            {
                UpdateChopping(state);
                return;
            }

            if (_step.action != CookingActionType.Pour)
                return;

            // As with a drop, the bottle itself is what the player is
            // watching, so either the pinch point or the carried bottle may
            // be over the station for the pour to count. The pour zone is
            // deliberately wide: chasing a small sweet spot is not the
            // exercise, the wrist tilt is.
            bool overStation = IsHandOverStation(_activeStation, state, false, 1.7f) ||
                               IsIngredientOverStation(_held, _activeStation, 1.7f);
            if (state.isPouring && overStation)
            {
                _pourAccum += dt;
                _pourHoldSeconds += dt;

                if (_pourStream == null && world != null)
                    _pourStream = world.BeginPour(GetPourColor(_held));

                if (_pourStream != null && _activeStation != null)
                {
                    _pourStream.Show(
                        _held.transform.TransformPoint(Vector3.up * 0.16f),
                        _activeStation.AnchorPosition);
                }

                UpdateProgress();

                if (_pourAccum >= _step.requiredPourSeconds)
                {
                    CompleteStep();
                    return;
                }
            }
            else
            {
                HidePourStream();
            }
        }

        /// <summary>
        /// Counts one chop per deliberate up-then-down stroke of the hand that
        /// holds the knife. The stroke is measured from the hand's own screen
        /// position instead of fixed points above and below the cutting board,
        /// so small, natural chops in front of the camera still register no
        /// matter where the board happens to project on screen.
        /// </summary>
        private void UpdateChopping(HandGestureState state)
        {
            if (world == null)
                return;

            float y = state.viewportPosition.y;
            if (!_chopHasViewportY)
            {
                _chopLastViewportY = y;
                _chopHasViewportY = true;
                return;
            }

            float dy = Mathf.Clamp(y - _chopLastViewportY, -0.08f, 0.08f);
            _chopLastViewportY = y;

            // About 2% of the screen height per half-stroke: reachable for a
            // rehab patient, but far above normal tracking jitter.
            const float upStroke = 0.022f;
            const float downStroke = 0.025f;

            if (dy > 0f)
            {
                _chopUpTravel += dy;
                _chopDownTravel = 0f;
                if (!_chopArmed && _chopUpTravel >= upStroke)
                    _chopArmed = true;
            }
            else if (dy < 0f)
            {
                _chopDownTravel -= dy;
                _chopUpTravel = 0f;
                if (_chopArmed && _chopDownTravel >= downStroke)
                {
                    _chopArmed = false;
                    _chopDownTravel = 0f;
                    RegisterChop();
                }
            }
        }

        private void ResetChopTracking()
        {
            _chopArmed = false;
            _chopUpTravel = 0f;
            _chopDownTravel = 0f;
            _chopHasViewportY = false;
        }

        /// <summary>
        /// Seasoning is a circling gesture, like stirring: an open hand
        /// drawn in slow circles over the station. The station's ring marks
        /// the target area, and every full revolution adds a pinch of the
        /// seasoning. Nothing has to be pinched, grabbed or aimed at.
        /// </summary>
        private void RegisterSprinkleTurn()
        {
            _sprinkleCount++;

            if (_audio != null)
                _audio.PlayDrop();

            Color color = CookingTheme.Accent;
            if (world != null)
            {
                CookingIngredient seasoning =
                    world.FindIngredient(_step.ingredientId);
                if (seasoning != null && seasoning.definition != null)
                    color = seasoning.definition.color;
            }

            if (_activeStation != null)
            {
                CookingEffects.Burst(
                    _activeStation.AnchorPosition + Vector3.up * 0.05f,
                    color, 6, 0.4f);
            }

            int required = Mathf.Max(1, _step.requiredSprinkleTurns);
            if (ui != null)
            {
                ui.SetProgress(Mathf.Clamp01((float)_sprinkleCount / required),
                    _sprinkleCount + " / " + required + " turns");
            }

            if (_sprinkleCount >= required)
                CompleteStep();
            else if (_sprinkleCount == 1 && ui != null)
                ui.ShowToast("Nice — keep circling to sprinkle",
                    CookingTheme.Success);
        }

        private void RegisterChop()
        {
            _chopCount++;

            if (_audio != null)
                _audio.PlayDrop();

            CookingIngredient food = world != null
                ? world.FindIngredient(_step.ingredientId)
                : null;
            Color color = food != null && food.definition != null
                ? food.definition.color
                : CookingTheme.Accent;

            if (world != null)
            {
                CookingEffects.Burst(
                    world.CuttingBoardSurface + Vector3.up * 0.015f,
                    color, 5, 0.35f);
            }

            int required = Mathf.Max(1, _step.requiredChops);
            if (ui != null)
            {
                ui.SetProgress(Mathf.Clamp01((float)_chopCount / required),
                    _chopCount + " / " + required + " chops");
            }

            if (_chopCount >= required)
                CompleteChop(food);
        }

        private void CompleteChop(CookingIngredient food)
        {
            if (world != null)
            {
                if (food == null)
                    food = world.FindIngredient(_step.ingredientId);
                if (food != null)
                {
                    food.MarkChopped();
                    world.PlaceOnCuttingBoard(food);
                }

                world.HidePlacementHint();
            }

            // The knife is a tool, not an ingredient: it goes back onto the
            // cutting board instead of being consumed.
            CookingIngredient knife = _held;
            _held = null;
            if (knife != null)
                knife.ReturnHome();

            if (_audio != null)
                _audio.PlaySuccess();

            CompleteStep();
        }

        private void ScheduleDrop(float delay, bool forceReturn)
        {
            if (_held == null)
                return;

            _dropPending = true;
            _dropSide = _heldSide;
            _dropAt = Time.unscaledTime + Mathf.Max(0f, delay);
            _dropForceReturn = forceReturn;
        }

        /// <summary>
        /// Finishes a pinch release after its grace period. A real release
        /// over the target station is honoured immediately; a release
        /// elsewhere waits a fraction of a second so a momentary tracking
        /// flicker cannot tear the ingredient out of the player's hand.
        /// </summary>
        private void UpdatePendingDrop()
        {
            if (!_dropPending)
                return;

            if (_held == null)
            {
                _dropPending = false;
                return;
            }

            if (gestures != null)
            {
                HandGestureState state = gestures.GetState(_dropSide);
                if (state.isPinching)
                {
                    // The pinch is back: keep carrying.
                    _dropPending = false;
                    return;
                }

                if (!_dropForceReturn && state.isTracked &&
                    (IsHandOverStation(_activeStation, state, false) ||
                     IsIngredientOverStation(_held, _activeStation)))
                {
                    _dropPending = false;
                    DropHeld(false);
                    return;
                }
            }

            if (Time.unscaledTime >= _dropAt)
            {
                _dropPending = false;
                DropHeld(_dropForceReturn);
            }
        }

        private void DropHeld(bool forceReturn)
        {
            CookingIngredient ingredient = _held;
            if (ingredient == null || _step == null)
                return;

            HandGestureState state = gestures.GetState(_heldSide);
            bool handLost = forceReturn || !state.isTracked;

            // Only a tracked, deliberate release can place the ingredient.
            // If the hand left the camera the prop always returns, otherwise
            // a short tracking dropout would "place" the item the moment it
            // is grabbed, long before the player lets go of the pinch.
            // Pouring uses a wider target zone than plain drops.
            float targetRadiusScale =
                _step.action == CookingActionType.Pour ? 1.7f : 1f;
            bool overStation = !handLost &&
                (IsHandOverStation(_activeStation, state, false, targetRadiusScale) ||
                 IsIngredientOverStation(ingredient, _activeStation, targetRadiusScale));
            bool correctHand = HandMatches(_heldSide, _step.requiredHand);
            bool carriedLongEnough =
                Time.unscaledTime - _heldSince >= MinCarrySeconds;

            HidePourStream();

            // The knife is a tool. Releasing it never places it: it returns to
            // the cutting board and the chop count for this step is kept.
            if (_step.action == CookingActionType.Chop)
            {
                _held = null;
                ResetChopTracking();
                ingredient.ReturnHome();
                if (ui != null)
                    ui.ShowToast("Keep pinching and chop up and down",
                        CookingTheme.Muted);
                if (world != null)
                    world.SetActiveStep(_step);
                UpdateProgress();
                return;
            }

            // A release right after the grab is a pinch flick, not a
            // deliberate placement: the prop simply returns home. Pouring is
            // exempt because its release is handled gently below (the bottle
            // is tilted, not dropped onto the station).
            if (!handLost && !carriedLongEnough &&
                _step.action != CookingActionType.Pour)
            {
                _held = null;
                ingredient.ReturnHome();
                if (ui != null)
                    ui.ShowToast("Hold the pinch while you carry the ingredient",
                        CookingTheme.Muted);
                if (world != null)
                    world.SetActiveStep(_step);
                UpdateProgress();
                return;
            }

            if (overStation && correctHand)
            {
                // Serve: the dish is set down on the plate, not "added" to it.
                if (_step.action == CookingActionType.Serve)
                {
                    Vector3 servePoint = _activeStation != null
                        ? _activeStation.AnchorPosition
                        : ingredient.transform.position;
                    Color serveColor = ingredient.definition != null
                        ? ingredient.definition.color
                        : CookingTheme.Success;

                    _held = null;
                    ingredient.MarkConsumed();
                    CookingEffects.Burst(servePoint, serveColor, 12, 0.8f);
                    CookingEffects.Burst(servePoint, CookingTheme.Success, 18, 1.05f);

                    if (world != null)
                        world.ShowFinishedDish(_recipe);

                    if (_audio != null)
                        _audio.PlaySuccess();
                    CompleteStep();
                    return;
                }

                // The cutting board keeps the ingredient as a prop: it is not
                // consumed, so it can be picked up again for chopping or for
                // the pot/bowl/plate step that follows.
                if (_step.action == CookingActionType.AddIngredient &&
                    _step.stationId == "board")
                {
                    Color boardColor = ingredient.definition != null
                        ? ingredient.definition.color
                        : CookingTheme.Accent;

                    _held = null;
                    ingredient.EndGrab();
                    if (world != null)
                    {
                        world.PlaceOnCuttingBoard(ingredient);
                        CookingEffects.Burst(
                            world.CuttingBoardSurface + Vector3.up * 0.02f,
                            boardColor, 8, 0.5f);
                    }

                    CompleteStep();
                    return;
                }

                if (_step.action == CookingActionType.Pour &&
                    _pourAccum < _step.requiredPourSeconds)
                {
                    if (ui != null)
                        ui.ShowToast("Almost — keep tilting over the " +
                                     CookingNames.StationZh(_step.stationId) +
                                     " to pour",
                                     CookingTheme.Muted);

                    // Keep the accumulated pour progress so the player can
                    // grab the bottle again and continue where they stopped.
                    _held = null;
                    ingredient.ReturnHome();
                    if (_audio != null)
                        _audio.PlayDrop();
                    if (world != null)
                        world.SetActiveStep(_step);
                    UpdateProgress();
                    return;
                }

                _held = null;

                Vector3 burstPoint = _activeStation != null
                    ? _activeStation.AnchorPosition
                    : ingredient.transform.position;
                Color burstColor = ingredient.definition != null
                    ? ingredient.definition.color
                    : CookingTheme.Accent;
                CookingEffects.Burst(burstPoint, burstColor, 12, 0.8f);

                if (_activeStation != null)
                    _activeStation.AddProcessed(ingredient, _step.action);
                ingredient.MarkConsumed();
                CompleteStep();
                return;
            }

            // Pouring is never punished either: releasing away from the
            // station returns the bottle, keeps the time already poured and
            // shows where to hold it. A shaky hand cannot fail the step.
            if (_step.action == CookingActionType.Pour)
            {
                _held = null;
                ingredient.ReturnHome();

                if (_audio != null)
                    _audio.PlayDrop();

                if (ui != null)
                {
                    if (handLost)
                        ui.ShowToast("Hand left the frame — the bottle returned",
                            CookingTheme.Muted);
                    else if (!correctHand)
                        ui.ShowToast("This step needs " +
                                     CookingNames.HandZh(_step.requiredHand),
                                     CookingTheme.Danger);
                    else
                        ui.ShowToast("Hold the bottle over the " +
                                     CookingNames.StationZh(_step.stationId) +
                                     " and tilt your palm",
                                     CookingTheme.Muted);
                }

                if (world != null)
                    world.SetActiveStep(_step);
                UpdateProgress();
                return;
            }

            // The hand was lost, or the release happened somewhere else.
            if (!handLost)
            {
                _wrongDrops++;
                _pourAccum = 0f;
            }

            _held = null;

            // A misplaced release is part of learning the gesture, not a
            // failure: play only the soft drop tone and keep the hint muted,
            // like the pour steps. The rehabilitation game should never make
            // a shaky hand sound like a mistake.
            if (_audio != null)
                _audio.PlayDrop();

            if (ui != null)
            {
                if (handLost)
                    ui.ShowToast("Hand left the frame — ingredient returned",
                        CookingTheme.Muted);
                else if (!correctHand)
                    ui.ShowToast("This step needs " +
                                 CookingNames.HandZh(_step.requiredHand),
                                 CookingTheme.Danger);
                else
                    ui.ShowToast("Not over the target — try again",
                        CookingTheme.Muted);
            }

            ingredient.ReturnHome();
            if (world != null)
                world.SetActiveStep(_step);
            UpdateProgress();
        }

        private void HidePourStream()
        {
            if (_pourStream != null)
            {
                if (world != null)
                    world.EndPour();
                _pourStream = null;
            }
        }

        private static Color GetPourColor(CookingIngredient ingredient)
        {
            if (ingredient == null || ingredient.definition == null)
                return new Color(0.9f, 0.7f, 0.3f, 1f);

            return ingredient.definition.color;
        }

        // ------------------------------------------------------------------
        // Step flow
        // ------------------------------------------------------------------

        public void StartRecipe(RecipeDefinition recipe)
        {
            if (!_started || recipe == null || recipe.steps == null ||
                recipe.steps.Count == 0 || world == null || ui == null)
            {
                Debug.LogWarning("[Cooking] StartRecipe rejected (started=" +
                                 _started + ", recipe=" + (recipe != null) +
                                 ", world=" + (world != null) +
                                 ", ui=" + (ui != null) + ")");
                return;
            }

            Debug.Log("[Cooking] StartRecipe " + recipe.recipeId);

            if (_held != null)
            {
                _held.ReturnHome();
                _held = null;
            }
            _dropPending = false;
            HidePourStream();

            _recipe = recipe;
            _stepIndex = 0;
            _step = null;
            _activeStation = null;
            _grabCount = 0;
            _stirCount = 0;
            _stirTotal = 0;
            _pourAccum = 0f;
            _pourHoldSeconds = 0f;
            _wrongDrops = 0;
            _elapsed = 0f;
            _stepComplete = false;
            _finished = false;
            _advanceAt = -1f;
            _resultsAt = -1f;

            _difficultyScale = recipe.difficulty == RecipeDifficulty.Easy
                ? 1.25f
                : recipe.difficulty == RecipeDifficulty.Medium
                    ? 1f
                    : 0.9f;

            world.ClearRecipe();
            world.SetVisible(true);
            world.PopulateRecipe(recipe);

            ui.ShowHud(recipe);
            ui.SetTimer(0f);
            _cooking = true;

            if (_audio != null)
                _audio.PlayPick();
            ui.ShowToast("Starting " + recipe.displayName, recipe.themeColor);
            EnterStep(0);
        }

        private void EnterStep(int index)
        {
            if (_recipe == null || index < 0 || index >= _recipe.steps.Count)
                return;

            _stepIndex = index;
            _step = _recipe.steps[index];
            _activeStation = world != null ? world.GetStation(_step.stationId) : null;
            _pourAccum = 0f;
            _stirCount = 0;
            _chopCount = 0;
            _sprinkleCount = 0;
            _heatAccum = 0f;
            _heatAwayHintShown = false;

            // Circling is now measured even while the fingers are pinched,
            // so clear the last step's motion; otherwise a hand that was
            // crossing the screen could add a turn the instant a Sprinkle
            // step appears.
            if (gestures != null)
                gestures.ResetCircles();
            // A chop must be a deliberate up-then-down stroke, so grabbing
            // the knife by itself never counts as one.
            ResetChopTracking();
            _stepComplete = false;
            _advanceAt = -1f;

            if (_held != null)
            {
                _held.ReturnHome();
                _held = null;
            }
            HidePourStream();

            if (world != null)
            {
                // A chop step expects its ingredient on the cutting board
                // (the previous step usually puts it there). A serve step
                // lifts a fresh dish out of the source station.
                if (_step.action == CookingActionType.Chop)
                    world.SnapIngredientToCuttingBoard(_step.ingredientId);
                else if (_step.action == CookingActionType.Serve)
                    world.SpawnServeProp(_recipe,
                        world.GetStation(_step.sourceStationId));

                world.SetActiveStep(_step);
                if (_step.action == CookingActionType.Stir ||
                    _step.action == CookingActionType.Sprinkle ||
                    _step.action == CookingActionType.Heat)
                    world.ShowPlacementHint(_activeStation);
                else
                    world.HidePlacementHint();
            }
            if (ui != null)
            {
                ui.SetStep(_recipe, index);
                UpdateProgress();
                if (_step.requiredHand != HandUsage.Any)
                {
                    ui.ShowToast("This step: use " +
                                 CookingNames.HandZh(_step.requiredHand),
                                 CookingTheme.Accent);
                }
            }
        }

        private void CompleteStep()
        {
            if (!_cooking || _stepComplete || _step == null)
                return;

            _stepComplete = true;
            _advanceAt = Time.time + 0.95f;

            HidePourStream();

            if (_held != null)
            {
                if (_activeStation != null)
                    _activeStation.AddProcessed(_held, _step.action);
                _held.MarkConsumed();
                _held = null;
            }
            else if (_step.action == CookingActionType.Sprinkle &&
                     _activeStation != null && world != null)
            {
                // Seasoning never picks the shaker up: the circling hand
                // sprinkles straight into the station, but the station still
                // shows the scattered seasoning afterwards.
                CookingIngredient seasoning =
                    world.FindIngredient(_step.ingredientId);
                if (seasoning != null)
                    _activeStation.AddProcessed(seasoning, _step.action);
            }

            if (world != null)
                world.HidePlacementHint();

            if (_step.action == CookingActionType.Stir && _activeStation != null)
                _activeStation.StirContents();

            if (_audio != null)
                _audio.PlaySuccess();
            if (_activeStation != null)
                _activeStation.PulseSuccess(CookingTheme.Success);
            if (ui != null)
            {
                ui.SetProgress(1f, "Done");
                ui.ShowToast("Great! " + _step.title + " complete",
                    CookingTheme.Success);
            }
        }

        private void AdvanceStep()
        {
            _stepComplete = false;
            _advanceAt = -1f;

            if (_recipe == null)
            {
                ReturnToMenu();
                return;
            }

            if (_stepIndex + 1 >= _recipe.steps.Count)
            {
                FinishRecipe();
                return;
            }

            EnterStep(_stepIndex + 1);
        }

        private void FinishRecipe()
        {
            _cooking = false;
            _stepComplete = false;
            _advanceAt = -1f;

            if (_held != null)
            {
                _held.MarkConsumed();
                _held = null;
            }
            HidePourStream();

            if (world != null)
            {
                world.ClearHighlights();

                // Keep the dish on screen before the results panel covers it.
                world.ShowFinishedDish(_recipe);
            }

            if (_audio != null)
                _audio.PlayComplete();
            if (_activeStation != null)
                _activeStation.PulseSuccess(CookingTheme.Success);
            if (ui != null)
            {
                ui.SetProgress(1f, "Dish ready!");
                ui.ShowToast("Dish ready — " +
                             (_recipe != null ? _recipe.displayName : "enjoy your meal"),
                             CookingTheme.Success);
            }

            _finished = true;
            _resultsAt = Time.time + 3.4f;
        }

        private void ShowResultsScreen()
        {
            int stars = _wrongDrops <= 1 ? 3 : (_wrongDrops <= 4 ? 2 : 1);

            if (ui != null)
            {
                ui.ShowResults(_recipe, _elapsed, _grabCount, _stirTotal,
                    _pourHoldSeconds, _wrongDrops, stars);
            }
        }

        private void UpdateProgress()
        {
            if (_step == null || ui == null)
                return;

            switch (_step.action)
            {
                case CookingActionType.Pour:
                {
                    if (_held == null)
                    {
                        ui.SetProgress(0f, "Grab the bottle, hold it over the " +
                                           CookingNames.StationZh(_step.stationId) +
                                           " and tilt your palm");
                        break;
                    }

                    if (_pourAccum <= 0.001f)
                    {
                        ui.SetProgress(0f, "Hold over the " +
                                           CookingNames.StationZh(_step.stationId) +
                                           " and tilt to pour");
                        break;
                    }

                    float required = Mathf.Max(0.01f, _step.requiredPourSeconds);
                    float fill = Mathf.Clamp01(_pourAccum / required);
                    ui.SetProgress(fill,
                        _pourAccum.ToString("F1") + " / " +
                        _step.requiredPourSeconds.ToString("F1") + " s");
                    break;
                }
                case CookingActionType.Stir:
                {
                    int required = Mathf.Max(1, _step.requiredRevolutions);
                    float fill = Mathf.Clamp01((float)_stirCount / required);
                    ui.SetProgress(fill, _stirCount + " / " + required + " turns");
                    break;
                }
                case CookingActionType.Chop:
                {
                    int required = Mathf.Max(1, _step.requiredChops);
                    if (_held == null)
                    {
                        ui.SetProgress(0f,
                            "Grab the knife, then chop up and down");
                    }
                    else
                    {
                        ui.SetProgress(Mathf.Clamp01((float)_chopCount / required),
                            _chopCount + " / " + required + " chops");
                    }
                    break;
                }
                case CookingActionType.Sprinkle:
                {
                    int required = Mathf.Max(1, _step.requiredSprinkleTurns);
                    if (_sprinkleCount <= 0)
                    {
                        ui.SetProgress(0f,
                            "Move your open hand in circles over the " +
                            CookingNames.StationZh(_step.stationId) +
                            " to sprinkle");
                    }
                    else
                    {
                        ui.SetProgress(
                            Mathf.Clamp01((float)_sprinkleCount / required),
                            _sprinkleCount + " / " + required + " turns");
                    }
                    break;
                }
                case CookingActionType.Heat:
                {
                    float required = Mathf.Max(0.1f, _step.requiredHeatSeconds);
                    if (_heatAccum <= 0.001f)
                    {
                        ui.SetProgress(0f, "Hold your open hand over the " +
                                           CookingNames.StationZh(_step.stationId));
                    }
                    else
                    {
                        ui.SetProgress(Mathf.Clamp01(_heatAccum / required),
                            _heatAccum.ToString("F1") + " / " +
                            required.ToString("F1") + " s");
                    }
                    break;
                }
                case CookingActionType.Serve:
                {
                    if (_held == null)
                    {
                        ui.SetProgress(0f, "Grab the dish from the " +
                                           CookingNames.StationZh(
                                               _step.sourceStationId));
                    }
                    else
                    {
                        ui.SetProgress(0f, "Release over the Plate");
                    }
                    break;
                }
                default:
                    if (_held != null)
                    {
                        ui.SetProgress(0f, "Release over the " +
                                           CookingNames.StationZh(_step.stationId));
                    }
                    else
                    {
                        ui.SetProgress(0f, "Grab the highlighted ingredient");
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Helpers and external buttons
        // ------------------------------------------------------------------

        /// <summary>
        /// Viewport position of a station target, clamped to the screen so a
        /// target anchor can never end up where no hand can reach it.
        /// </summary>
        private Vector2 StationViewportPoint(Vector3 worldPoint)
        {
            if (sceneCamera == null)
                return new Vector2(-10f, -10f);

            Vector3 viewport = sceneCamera.WorldToViewportPoint(worldPoint);
            return viewport.z > 0f
                ? new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y))
                : new Vector2(-10f, -10f);
        }

        private bool IsHandOverStation(CookingStation station,
            HandGestureState state, bool usePalm, float radiusScale = 1f)
        {
            if (station == null || sceneCamera == null || !state.isTracked)
                return false;

            Vector2 handViewport = usePalm ? state.palmViewport : state.viewportPosition;

            // Either the visible ring at the base of the station or the
            // anchor above it counts as the station, so a player aiming at
            // the ring around the pot and one aiming at its opening both land
            // inside the drop circle.
            Vector2 stationRing = StationViewportPoint(station.transform.position);
            Vector2 stationAnchor = StationViewportPoint(station.AnchorPosition);

            // Generous enough for a patient and for small props such as
            // seasoning shakers, but tight enough that a prop still sitting on
            // the far side of the prep board does not count as over it.
            // Pouring uses a wider circle still: missing the exact spot is
            // what made the bottle steps feel impossible.
            float radius = Mathf.Clamp(
                station.viewportRadius * 1.6f, 0.11f, 0.20f) *
                Mathf.Max(0.1f, radiusScale);
            return Mathf.Min(
                Vector2.Distance(handViewport, stationRing),
                Vector2.Distance(handViewport, stationAnchor)) <= radius;
        }

        private bool IsIngredientOverStation(CookingIngredient ingredient,
            CookingStation station, float radiusScale = 1f)
        {
            if (ingredient == null || station == null || sceneCamera == null)
                return false;

            Vector3 ingredientViewport =
                sceneCamera.WorldToViewportPoint(ingredient.transform.position);
            if (ingredientViewport.z <= 0f)
                return false;

            Vector2 ingredientPoint = new Vector2(
                ingredientViewport.x, ingredientViewport.y);
            Vector2 stationRing = StationViewportPoint(station.transform.position);
            Vector2 stationAnchor = StationViewportPoint(station.AnchorPosition);

            float radius = Mathf.Clamp(
                station.viewportRadius * 1.7f, 0.12f, 0.22f) *
                Mathf.Max(0.1f, radiusScale);
            return Mathf.Min(
                Vector2.Distance(ingredientPoint, stationRing),
                Vector2.Distance(ingredientPoint, stationAnchor)) <= radius;
        }

        private static bool HandMatches(HandSide side, HandUsage usage)
        {
            if (usage == HandUsage.Left)
                return side == HandSide.Left;
            if (usage == HandUsage.Right)
                return side == HandSide.Right;
            return true;
        }

        public void PlayUiClick()
        {
            if (_audio != null)
                _audio.PlayPick();
        }

        public void ReturnToMenu()
        {
            _cooking = false;
            _stepComplete = false;
            _finished = false;
            _advanceAt = -1f;
            _resultsAt = -1f;

            if (_held != null)
            {
                _held.ReturnHome();
                _held = null;
            }
            _dropPending = false;
            HidePourStream();

            if (world != null)
            {
                world.ClearHighlights();
                world.ClearRecipe();
                world.SetVisible(false);
            }
            if (ui != null)
                ui.ShowMenu();
        }

        public void RestartRecipe()
        {
            if (_recipe != null)
                StartRecipe(_recipe);
        }

        private void OnDestroy()
        {
            if (gestures != null)
            {
                gestures.PinchStarted -= OnPinchStarted;
                gestures.PinchReleased -= OnPinchReleased;
                gestures.StirRevolutionCompleted -= OnStirRevolution;
            }

            if (Instance == this)
                Instance = null;
        }
    }
}
