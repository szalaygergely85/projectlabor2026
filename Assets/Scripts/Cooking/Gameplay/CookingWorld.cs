using System.Collections.Generic;
using UnityEngine;

namespace CookingRehab
{
    /// <summary>
    /// Builds and owns the physical cooking set: one prep board floating at
    /// hand depth that carries every station (cutting board, mixing bowl,
    /// pot, microwave and plate), plus the ingredient props for the current
    /// recipe. The kitchen behind the board is scenery; no scene object is
    /// ever used as a cooking target.
    /// </summary>
    public class CookingWorld : MonoBehaviour
    {
        public Camera sceneCamera;
        public Canvas labelCanvas;
        public Transform prepRoot;
        public Transform ingredientRoot;
        public CookingStation potStation;
        public CookingStation bowlStation;
        public CookingStation plateStation;
        public CookingStation boardStation;
        public CookingStation microwaveStation;

        private const float DishMoundBaseY = 0.105f;
        private const float DishMoundRadius = 0.18f;
        private const float DishMoundHeight = 0.085f;

        private readonly List<CookingIngredient> _ingredients =
            new List<CookingIngredient>();
        private readonly List<WorldLabel> _stationLabels = new List<WorldLabel>();
        private LiquidStream _pourStream;
        private GameObject _finishedDishRoot;
        private WorldLabel _finishedLabel;
        private CookingIngredient _knife;
        private CookingIngredient _serveDish;
        private float _interactionDepth = 1.07f;
        private Vector3 _flatForward = Vector3.forward;
        private Vector3 _flatRight = Vector3.right;
        private Vector3 _boardCenter;
        private Quaternion _boardRotation = Quaternion.identity;
        private float _boardTopY = 1.4f;
        private bool _built;

        public IReadOnlyList<CookingIngredient> Ingredients
        {
            get { return _ingredients; }
        }

        public void Build(Camera camera, float interactionDepth, Canvas labels)
        {
            if (_built)
                return;

            sceneCamera = camera;
            labelCanvas = labels;
            _interactionDepth = Mathf.Clamp(interactionDepth, 0.75f, 1.65f);

            if (prepRoot == null)
            {
                GameObject root = new GameObject("Cooking Prep");
                root.transform.SetParent(transform, false);
                prepRoot = root.transform;
            }

            if (ingredientRoot == null)
            {
                GameObject root = new GameObject("Ingredients");
                root.transform.SetParent(prepRoot, false);
                ingredientRoot = root.transform;
            }

            _flatForward = sceneCamera.transform.forward;
            _flatForward.y = 0f;
            if (_flatForward.sqrMagnitude < 0.0001f)
                _flatForward = Vector3.forward;
            _flatForward.Normalize();

            _flatRight = sceneCamera.transform.right;
            _flatRight.y = 0f;
            if (_flatRight.sqrMagnitude < 0.0001f)
                _flatRight = Vector3.right;
            _flatRight.Normalize();

            // Every station now lives on the prep board. The board is
            // centred in the view instead of being pushed right to leave
            // room for the kitchen pot, and it is pushed a little deeper
            // along the view ray so its near edge does not hang over the
            // front of the kitchen table. The hand rig is pulled toward
            // the camera (see MediaPipeVRHand.SetBoardDepth), so hands
            // still render in front of the board instead of being hidden
            // underneath it.
            const float boardDepthOffset = 0.18f;
            const float boardAnchorY = 0.46f;
            Vector3 boardPoint = sceneCamera.ViewportToWorldPoint(
                new Vector3(0.54f, boardAnchorY,
                    _interactionDepth + boardDepthOffset));
            _boardTopY = boardPoint.y;
            _boardCenter = new Vector3(boardPoint.x, _boardTopY, boardPoint.z);
            _boardRotation = Quaternion.LookRotation(_flatForward, Vector3.up);

            BuildBoard();
            BuildCuttingBoard();
            BuildBowlStation();
            BuildPlateStation();
            BuildPotStation();
            BuildMicrowaveStation();

            // The kitchen pot and microwave are no longer interaction
            // targets. Hide them so the backdrop does not show a second pot
            // or a second microwave behind the stand-ins on the board.
            // Everything else in the kitchen stays as scenery.
            HideKitchenObject(FindPot());
            HideKitchenObject(FindMicrowave());

            _built = true;
        }

        private void BuildBoard()
        {
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Prep Board";
            board.transform.SetParent(prepRoot, false);
            board.transform.position = _boardCenter - Vector3.up * 0.035f;
            board.transform.rotation = _boardRotation;
            board.transform.localScale = new Vector3(1.92f, 0.07f, 0.55f);
            RemoveCollider(board);
            ApplyMaterial(board, CookingMaterialFactory.Lit(CookingTheme.Wood, 0f, 0.32f));

            GameObject mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "Prep Mat";
            mat.transform.SetParent(prepRoot, false);
            mat.transform.position = _boardCenter + Vector3.up * 0.006f;
            mat.transform.rotation = _boardRotation;
            mat.transform.localScale = new Vector3(1.83f, 0.012f, 0.47f);
            RemoveCollider(mat);
            ApplyMaterial(mat, CookingMaterialFactory.Lit(
                new Color(0.16f, 0.23f, 0.17f, 1f), 0f, 0.2f));

            GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "Board Edge";
            edge.transform.SetParent(prepRoot, false);
            edge.transform.position = _boardCenter +
                _flatForward * 0.274f + Vector3.down * 0.015f;
            edge.transform.rotation = _boardRotation;
            edge.transform.localScale = new Vector3(1.92f, 0.012f, 0.018f);
            RemoveCollider(edge);
            ApplyMaterial(edge, CookingMaterialFactory.Emissive(
                new Color(0.98f, 0.68f, 0.32f, 1f), 0.6f, 0.2f));
        }

        /// <summary>
        /// The chopping station in the middle of the prep board. It is a real
        /// drop target: whole ingredients are placed here first, then chopped
        /// with the knife, then carried on to the pot, bowl or plate.
        /// </summary>
        private void BuildCuttingBoard()
        {
            Vector3 position = BoardPoint(0f, 0.04f);
            boardStation = CreateStation("board", "Cutting Board", position, 0.17f);

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Cutting Board";
            board.transform.SetParent(boardStation.transform, false);
            board.transform.localPosition = new Vector3(0f, 0.0175f, 0f);
            board.transform.rotation = _boardRotation;
            board.transform.localScale = new Vector3(0.34f, 0.035f, 0.32f);
            RemoveCollider(board);
            ApplyMaterial(board, CookingMaterialFactory.Lit(
                new Color(0.62f, 0.45f, 0.27f, 1f), 0f, 0.35f));

            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "Cutting Board Rim";
            rim.transform.SetParent(boardStation.transform, false);
            rim.transform.localPosition = new Vector3(0f, 0.034f, 0f);
            rim.transform.rotation = _boardRotation;
            rim.transform.localScale = new Vector3(0.28f, 0.004f, 0.26f);
            RemoveCollider(rim);
            ApplyMaterial(rim, CookingMaterialFactory.Lit(
                new Color(0.5f, 0.35f, 0.2f, 1f), 0f, 0.3f));

            CreateStationAnchor(boardStation, 0.04f);
            boardStation.contentsHeight = 0.04f;
            boardStation.contentsRadius = 0.15f;

            // The station ring sits on top of the raised wooden board.
            boardStation.SetMarkerHeight(0.045f);
            AddStationLabel(boardStation, Vector3.up * 0.26f);
        }

        private void BuildBowlStation()
        {
            Vector3 position = BoardPoint(-0.36f, 0.03f);
            bowlStation = CreateStation("bowl", "Mixing Bowl", position, 0.165f);

            GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "Mixing Bowl";
            bowl.transform.SetParent(bowlStation.transform, false);
            bowl.transform.localPosition = new Vector3(0f, 0.055f, 0f);
            bowl.transform.localScale = new Vector3(0.30f, 0.055f, 0.30f);
            RemoveCollider(bowl);
            ApplyMaterial(bowl, CookingMaterialFactory.Lit(
                CookingTheme.Ceramic, 0.02f, 0.55f));

            GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "Bowl Inner";
            inner.transform.SetParent(bowlStation.transform, false);
            inner.transform.localPosition = new Vector3(0f, 0.105f, 0f);
            inner.transform.localScale = new Vector3(0.25f, 0.008f, 0.25f);
            RemoveCollider(inner);
            ApplyMaterial(inner, CookingMaterialFactory.Lit(
                new Color(0.63f, 0.68f, 0.63f, 1f), 0.05f, 0.4f));

            CreateStationAnchor(bowlStation, 0.12f);
            bowlStation.contentsHeight = 0.115f;
            bowlStation.contentsRadius = 0.125f;
            AddStationLabel(bowlStation, Vector3.up * 0.30f);
        }

        private void BuildPlateStation()
        {
            Vector3 position = BoardPoint(0.36f, 0f);
            plateStation = CreateStation("plate", "Plate", position, 0.18f);

            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plate.name = "Plate";
            plate.transform.SetParent(plateStation.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.014f, 0f);
            plate.transform.localScale = new Vector3(0.34f, 0.014f, 0.34f);
            RemoveCollider(plate);
            ApplyMaterial(plate, CookingMaterialFactory.Lit(
                CookingTheme.Ceramic, 0.02f, 0.62f));

            GameObject plateInner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plateInner.name = "Plate Inner";
            plateInner.transform.SetParent(plateStation.transform, false);
            plateInner.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            plateInner.transform.localScale = new Vector3(0.28f, 0.006f, 0.28f);
            RemoveCollider(plateInner);
            ApplyMaterial(plateInner, CookingMaterialFactory.Lit(
                new Color(0.85f, 0.88f, 0.82f, 1f), 0.02f, 0.7f));

            CreateStationAnchor(plateStation, 0.05f);
            plateStation.contentsHeight = 0.035f;
            plateStation.contentsRadius = 0.145f;
            AddStationLabel(plateStation, Vector3.up * 0.28f);
        }

        private void BuildPotStation()
        {
            // The cooking game is played entirely on the prep board, so the
            // pot is a stand-in built here instead of the pot modelled in
            // the kitchen. The kitchen pot was a poor target: its bounds
            // centre is pulled sideways by the handle, so dropped pieces did
            // not land in the middle of the opening, and its rim sits close
            // to the edge of the webcam image. This pot is built around the
            // station origin, which makes the contents perfectly centred.
            Vector3 position = BoardPoint(-0.72f, 0.05f);
            potStation = CreateStation("pot", "Pot", position, 0.20f);
            potStation.transform.rotation = _boardRotation;
            potStation.contentsHeight = 0.205f;
            potStation.contentsRadius = 0.12f;
            // The glowing target ring hovers at the rim, so the pot body
            // cannot hide the far half of it.
            potStation.SetMarkerHeight(0.21f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Pot Body";
            body.transform.SetParent(potStation.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            body.transform.localScale = new Vector3(0.34f, 0.10f, 0.34f);
            RemoveCollider(body);
            ApplyMaterial(body, CookingMaterialFactory.Lit(
                new Color(0.24f, 0.26f, 0.28f, 1f), 0.15f, 0.5f));

            // A darker inset disc reads as the open mouth of the pot; the
            // ingredients are drawn just above it, so they never float over
            // the board outside the pot.
            GameObject mouth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mouth.name = "Pot Mouth";
            mouth.transform.SetParent(potStation.transform, false);
            mouth.transform.localPosition = new Vector3(0f, 0.201f, 0f);
            mouth.transform.localScale = new Vector3(0.29f, 0.004f, 0.29f);
            RemoveCollider(mouth);
            ApplyMaterial(mouth, CookingMaterialFactory.Lit(
                new Color(0.10f, 0.11f, 0.12f, 1f), 0f, 0.25f));

            // Two small handles make the cylinder read as a pot from above.
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.name = "Pot Handle";
                handle.transform.SetParent(potStation.transform, false);
                handle.transform.localPosition = new Vector3(i * 0.19f, 0.13f, 0f);
                handle.transform.localScale = new Vector3(0.05f, 0.025f, 0.06f);
                RemoveCollider(handle);
                ApplyMaterial(handle, CookingMaterialFactory.Lit(
                    new Color(0.18f, 0.19f, 0.2f, 1f), 0.15f, 0.45f));
            }

            CreateStationAnchor(potStation, 0.27f);
            AddStationLabel(potStation, Vector3.up * 0.36f);
            AddPotSteam(potStation.transform, 0.23f, 0.15f);

            Debug.Log("[Cooking] Pot station is built on the prep board.");
        }

        private void AddPotSteam(Transform parent, float height, float radius)
        {
            GameObject steamObject = new GameObject("Pot Steam");
            steamObject.transform.SetParent(parent, false);
            steamObject.transform.localPosition = new Vector3(0f, height, 0f);
            SteamEmitter steam = steamObject.AddComponent<SteamEmitter>();
            steam.radius = Mathf.Max(0.08f, radius * 0.6f);
            steam.interval = 0.42f;
        }

        /// <summary>
        /// The microwave stand-in on the prep board. The tofu recipe heats
        /// its food on the dark tray on top of it, and the heat step is a
        /// "hold your open hand over it" timer.
        /// </summary>
        private void BuildMicrowaveStation()
        {
            // Same as the pot: the kitchen microwave is no longer used. It
            // stands at the edge of the camera image, which made the heat
            // step need a hand at the very border of the webcam view. This
            // stand-in sits on the prep board, in the same reachable area as
            // every other station.
            Vector3 position = BoardPoint(0.74f, 0.06f);
            microwaveStation = CreateStation("microwave", "Microwave",
                position, 0.20f);
            microwaveStation.transform.rotation = _boardRotation;
            microwaveStation.contentsHeight = 0.216f;
            microwaveStation.contentsRadius = 0.09f;
            // The target ring sits above the body, ringing the tray.
            microwaveStation.SetMarkerHeight(0.213f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Microwave Body";
            body.transform.SetParent(microwaveStation.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            body.transform.localScale = new Vector3(0.30f, 0.20f, 0.24f);
            RemoveCollider(body);
            ApplyMaterial(body, CookingMaterialFactory.Lit(
                new Color(0.76f, 0.77f, 0.79f, 1f), 0.05f, 0.55f));

            // The dark tray on top is where the food sits. Seen from this
            // high camera it reads as the inside of the microwave, and the
            // pieces stay visible instead of being hidden by the body.
            GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "Microwave Tray";
            tray.transform.SetParent(microwaveStation.transform, false);
            tray.transform.localPosition = new Vector3(0f, 0.206f, 0f);
            tray.transform.localScale = new Vector3(0.24f, 0.012f, 0.18f);
            RemoveCollider(tray);
            ApplyMaterial(tray, CookingMaterialFactory.Lit(
                new Color(0.14f, 0.17f, 0.2f, 1f), 0.1f, 0.7f));

            // A dark door and a control panel on the face toward the player
            // make the box recognisable as a microwave. Local -Z points back
            // toward the player: the board's local +Z is the camera forward
            // direction, i.e. deeper into the kitchen.
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Microwave Door";
            door.transform.SetParent(microwaveStation.transform, false);
            door.transform.localPosition = new Vector3(-0.035f, 0.095f, -0.122f);
            door.transform.localScale = new Vector3(0.19f, 0.13f, 0.01f);
            RemoveCollider(door);
            ApplyMaterial(door, CookingMaterialFactory.Lit(
                new Color(0.07f, 0.09f, 0.12f, 1f), 0.05f, 0.8f));

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Microwave Panel";
            panel.transform.SetParent(microwaveStation.transform, false);
            panel.transform.localPosition = new Vector3(0.11f, 0.095f, -0.122f);
            panel.transform.localScale = new Vector3(0.05f, 0.13f, 0.01f);
            RemoveCollider(panel);
            ApplyMaterial(panel, CookingMaterialFactory.Lit(
                new Color(0.35f, 0.36f, 0.38f, 1f), 0.1f, 0.5f));

            CreateStationAnchor(microwaveStation, 0.28f);
            AddStationLabel(microwaveStation, Vector3.up * 0.38f);

            Debug.Log("[Cooking] Microwave station is built on the prep board.");
        }

        private CookingStation CreateStation(string id, string displayName,
            Vector3 worldPosition, float radius)
        {
            GameObject go = new GameObject(id + " Station");
            go.transform.SetParent(prepRoot, false);
            go.transform.position = worldPosition;

            CookingStation station = go.AddComponent<CookingStation>();
            station.Initialize(id, displayName, radius);
            station.viewportRadius = EstimateViewportRadius(worldPosition, radius);
            return station;
        }

        private static void CreateStationAnchor(CookingStation station, float height)
        {
            GameObject anchor = new GameObject("Anchor");
            anchor.transform.SetParent(station.transform, false);
            anchor.transform.localPosition = new Vector3(0f, height, 0f);
            station.anchor = anchor.transform;
        }

        private void AddStationLabel(CookingStation station, Vector3 offset)
        {
            if (labelCanvas == null || station == null)
                return;

            WorldLabel label = WorldLabel.Create(labelCanvas, sceneCamera,
                station.transform, station.displayName, offset,
                CookingStation.LabelFontSize, CookingStation.LabelColor);
            if (label != null)
                _stationLabels.Add(label);
        }

        public void SetVisible(bool visible)
        {
            if (prepRoot != null)
                prepRoot.gameObject.SetActive(visible);

            for (int i = 0; i < _stationLabels.Count; i++)
            {
                if (_stationLabels[i] != null)
                    _stationLabels[i].SetVisible(visible);
            }

            if (_finishedLabel != null)
                _finishedLabel.SetVisible(visible);

            if (!visible)
                HidePlacementHint();
        }

        /// <summary>
        /// Emphasises the single ring of one station while an ingredient is
        /// carried. Called after a successful grab and at the start of stir
        /// steps. No extra world-space markers are added.
        /// </summary>
        public void ShowPlacementHint(CookingStation station)
        {
            HidePlacementHint();
            if (station != null)
                station.SetPlacementHint(true);
        }

        public void HidePlacementHint()
        {
            if (potStation != null)
                potStation.SetPlacementHint(false);
            if (bowlStation != null)
                bowlStation.SetPlacementHint(false);
            if (plateStation != null)
                plateStation.SetPlacementHint(false);
            if (boardStation != null)
                boardStation.SetPlacementHint(false);
            if (microwaveStation != null)
                microwaveStation.SetPlacementHint(false);
        }

        /// <summary>
        /// A pot that is off screen, behind the camera or tucked in a corner of
        /// the kitchen cannot be reached with a webcam hand, so pot steps would
        /// always fail. In that case the world builds the simulated pot on the
        /// prep board instead.
        /// </summary>
        private bool IsPotReachable(Vector3 worldPoint)
        {
            if (sceneCamera == null)
                return true;

            Vector3 viewport = sceneCamera.WorldToViewportPoint(worldPoint);
            return viewport.z > 0.25f &&
                   viewport.x > 0.10f && viewport.x < 0.94f &&
                   viewport.y > 0.08f && viewport.y < 0.90f;
        }

        /// <summary>
        /// Pulls a world point toward the centre of the screen until its
        /// viewport position fits inside the given limits. Used for a station
        /// whose real appliance sits too close to the edge of a narrow game
        /// view, so the interaction ring stays visible and reachable.
        /// </summary>
        private Vector3 ClampToViewport(Vector3 worldPoint, float maxX, float maxY)
        {
            if (sceneCamera == null)
                return worldPoint;

            Vector3 viewport = sceneCamera.WorldToViewportPoint(worldPoint);
            float x = Mathf.Clamp(viewport.x, 0.10f, maxX);
            float y = Mathf.Clamp(viewport.y, 0.10f, maxY);
            if (Mathf.Approximately(x, viewport.x) &&
                Mathf.Approximately(y, viewport.y))
            {
                return worldPoint;
            }

            return sceneCamera.ViewportToWorldPoint(
                new Vector3(x, y, viewport.z));
        }

        private float EstimateViewportRadius(Vector3 center, float radius)
        {
            if (sceneCamera == null)
                return Mathf.Max(0.075f, radius);

            Vector3 centerViewport = sceneCamera.WorldToViewportPoint(center);
            Vector3 edgeViewport = sceneCamera.WorldToViewportPoint(
                center + sceneCamera.transform.right * radius);

            return Mathf.Clamp(Vector2.Distance(
                new Vector2(centerViewport.x, centerViewport.y),
                new Vector2(edgeViewport.x, edgeViewport.y)), 0.075f, 0.16f);
        }

        private Vector3 BoardPoint(float localX, float localZ)
        {
            Vector3 point = _boardCenter + _flatRight * localX + _flatForward * localZ;
            point.y = _boardTopY;
            return point;
        }

        public CookingStation GetStation(string stationId)
        {
            if (stationId == "bowl")
                return bowlStation;
            if (stationId == "plate")
                return plateStation;
            if (stationId == "board")
                return boardStation;
            if (stationId == "microwave")
                return microwaveStation != null ? microwaveStation : potStation;
            return potStation;
        }

        public CookingIngredient FindIngredient(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < _ingredients.Count; i++)
            {
                CookingIngredient ingredient = _ingredients[i];
                if (ingredient != null && !ingredient.isConsumed &&
                    ingredient.ingredientId == id)
                {
                    return ingredient;
                }
            }

            return null;
        }

        public void PopulateRecipe(RecipeDefinition recipe)
        {
            ClearRecipe();
            if (recipe == null)
                return;

            List<string> ids = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < recipe.steps.Count; i++)
            {
                RecipeStep step = recipe.steps[i];
                if (!step.NeedsIngredient || string.IsNullOrEmpty(step.ingredientId))
                    continue;

                // Tools and the serve dish are created by the world itself.
                if (step.ingredientId == "knife" || step.ingredientId == "dish")
                    continue;

                if (seen.Add(step.ingredientId))
                    ids.Add(step.ingredientId);
            }

            for (int i = 0; i < ids.Count; i++)
                CreateIngredient(ids[i], i, ids.Count);

            CreateKnife();
        }

        private void CreateIngredient(string id, int index, int total)
        {
            Vector2 slot = SlotOffset(index, total);
            Vector3 position = BoardPoint(slot.x, slot.y);
            position.y = _boardTopY + 0.006f;

            CreateIngredientAt(id, position,
                _boardRotation * Quaternion.Euler(0f, index % 2 == 0 ? -12f : 12f, 0f),
                index, null);
        }

        private CookingIngredient CreateIngredientAt(string id, Vector3 position,
            Quaternion rotation, int slot, string labelOverride)
        {
            IngredientDefinition definition = IngredientCatalog.Get(id);
            if (definition == null)
                return null;

            GameObject root = new GameObject("Ingredient_" + id);
            root.transform.SetParent(ingredientRoot, false);
            root.transform.position = position;
            root.transform.rotation = rotation;

            GameObject highlightRoot;
            GameObject visual = IngredientVisualFactory.Create(definition, out highlightRoot);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            CookingIngredient ingredient = root.AddComponent<CookingIngredient>();
            ingredient.Initialize(id, slot, visual, highlightRoot);

            if (labelCanvas != null)
            {
                ingredient.label = WorldLabel.Create(labelCanvas, sceneCamera,
                    root.transform,
                    string.IsNullOrEmpty(labelOverride) ? definition.nameEn : labelOverride,
                    Vector3.up * 0.22f, 20, new Color(1f, 0.96f, 0.89f, 1f));
            }

            _ingredients.Add(ingredient);
            return ingredient;
        }

        private void CreateKnife()
        {
            Vector3 position = BoardPoint(0.15f, 0.05f);
            position.y = _boardTopY + 0.038f;
            _knife = CreateIngredientAt("knife", position,
                _boardRotation * Quaternion.Euler(0f, -38f, 0f), 90, "Knife");
        }

        /// <summary>World position of the top of the wooden cutting board.</summary>
        public Vector3 CuttingBoardSurface
        {
            get
            {
                if (boardStation != null)
                    return boardStation.transform.position + Vector3.up * 0.038f;
                return BoardPoint(0f, 0.04f);
            }
        }

        /// <summary>
        /// Puts an ingredient neatly in the middle of the cutting board and
        /// makes that its home, so the next step can find it there.
        /// </summary>
        public void PlaceOnCuttingBoard(CookingIngredient ingredient)
        {
            if (ingredient == null)
                return;

            Vector3 position = CuttingBoardSurface + _flatForward * 0.03f;
            ingredient.SnapTo(position,
                _boardRotation * Quaternion.Euler(0f, -8f, 0f));
        }

        public void SnapIngredientToCuttingBoard(string id)
        {
            PlaceOnCuttingBoard(FindIngredient(id));
        }

        /// <summary>
        /// Lifts a ready dish out of the pot (or bowl) for the Serve step: a
        /// new prop appears above the source station that the player grabs and
        /// carries over to the plate.
        /// </summary>
        public CookingIngredient SpawnServeProp(RecipeDefinition recipe,
            CookingStation source)
        {
            if (_serveDish != null && !_serveDish.isConsumed)
                return _serveDish;

            CookingStation from = source != null ? source : potStation;
            if (from == null)
                return null;

            Vector3 position = from.AnchorPosition + Vector3.up * 0.07f;

            // The dish must appear where a webcam hand can reach it. The pot
            // rim sits near the left edge of the image and its rim anchor
            // projects past it, so the old spawn point left the finished
            // dish off screen: it could not be seen or grabbed. Pull the
            // spawn point into the reachable part of the image, keeping its
            // depth so it still reads as "above the station".
            if (sceneCamera != null)
            {
                Vector3 viewport = sceneCamera.WorldToViewportPoint(position);
                if (viewport.z > 0f)
                {
                    float x = Mathf.Clamp(viewport.x, 0.14f, 0.86f);
                    float y = Mathf.Clamp(viewport.y, 0.16f, 0.80f);
                    if (!Mathf.Approximately(x, viewport.x) ||
                        !Mathf.Approximately(y, viewport.y))
                    {
                        position = sceneCamera.ViewportToWorldPoint(
                            new Vector3(x, y, viewport.z));
                    }
                }
            }

            _serveDish = CreateIngredientAt("dish", position,
                _boardRotation, 91, "Dish");
            CookingEffects.Burst(position, CookingTheme.Success, 10, 0.6f);
            return _serveDish;
        }

        private static Vector2 SlotOffset(int index, int count)
        {
            // The cutting board now occupies the middle of the prep board, so
            // the ingredient props line up along the back edge instead of
            // covering the chopping area.
            const float spacing = 0.19f;
            float x = (index - (count - 1) * 0.5f) * spacing;
            return new Vector2(x, -0.22f);
        }

        public void SetActiveStep(RecipeStep step)
        {
            CookingStation active = step != null ? GetStation(step.stationId) : null;
            SetStationHighlighted(active);

            // Chop steps highlight the knife; Serve steps highlight the dish
            // that was just lifted out of the pot or bowl. Sprinkle steps
            // highlight only the station ring: seasoning is sprinkled by
            // circling an open hand over it, so the shaker prop is never
            // grabbed and must not invite a pinch.
            string wanted = null;
            if (step != null && step.NeedsIngredient &&
                step.action != CookingActionType.Sprinkle)
            {
                if (step.action == CookingActionType.Chop)
                    wanted = "knife";
                else if (step.action == CookingActionType.Serve)
                    wanted = "dish";
                else
                    wanted = step.ingredientId;
            }

            for (int i = 0; i < _ingredients.Count; i++)
            {
                CookingIngredient ingredient = _ingredients[i];
                if (ingredient == null)
                    continue;

                bool match = !string.IsNullOrEmpty(wanted) &&
                             ingredient.ingredientId == wanted &&
                             !ingredient.isConsumed;
                ingredient.SetHighlighted(match);
            }
        }

        public void ClearHighlights()
        {
            HidePlacementHint();
            SetStationHighlighted(null);
            for (int i = 0; i < _ingredients.Count; i++)
            {
                if (_ingredients[i] != null)
                    _ingredients[i].SetHighlighted(false);
            }
        }

        private void SetStationHighlighted(CookingStation active)
        {
            if (potStation != null)
                potStation.SetHighlighted(potStation == active);
            if (bowlStation != null)
                bowlStation.SetHighlighted(bowlStation == active);
            if (plateStation != null)
                plateStation.SetHighlighted(plateStation == active);
            if (boardStation != null)
                boardStation.SetHighlighted(boardStation == active);
            if (microwaveStation != null)
                microwaveStation.SetHighlighted(microwaveStation == active);
        }

        public void ClearRecipe()
        {
            EndPour();
            HidePlacementHint();
            HideFinishedDish();

            if (potStation != null)
                potStation.ClearContents();
            if (bowlStation != null)
                bowlStation.ClearContents();
            if (plateStation != null)
                plateStation.ClearContents();
            if (boardStation != null)
                boardStation.ClearContents();
            if (microwaveStation != null)
                microwaveStation.ClearContents();

            _knife = null;
            _serveDish = null;

            for (int i = 0; i < _ingredients.Count; i++)
            {
                CookingIngredient ingredient = _ingredients[i];
                if (ingredient == null)
                    continue;

                if (ingredient.label != null)
                    Destroy(ingredient.label.gameObject);
                Destroy(ingredient.gameObject);
            }

            _ingredients.Clear();
        }

        // ------------------------------------------------------------------
        // Finished dish
        // ------------------------------------------------------------------

        /// <summary>
        /// Serves the finished dish on the plate station: a mound coloured by
        /// the recipe ingredients, sauce, sprinkles, garnish, steam and a
        /// floating label. Shown for a few seconds before the results panel.
        /// </summary>
        public void ShowFinishedDish(RecipeDefinition recipe)
        {
            // A Serve step may already have put the dish on the plate; in that
            // case FinishRecipe must not rebuild it and make it flicker.
            if (_finishedDishRoot != null)
                return;

            HideFinishedDish();
            if (recipe == null || prepRoot == null)
                return;

            // The finished dish now lives on the plate. Remove the chopped
            // pieces, sprinkles and pour layers left in the pot, bowl and
            // cutting board, so the place where the food was prepared no
            // longer shows a second helping behind the served dish.
            if (potStation != null)
                potStation.ClearContents();
            if (bowlStation != null)
                bowlStation.ClearContents();
            if (plateStation != null)
                plateStation.ClearContents();
            if (boardStation != null)
                boardStation.ClearContents();
            if (microwaveStation != null)
                microwaveStation.ClearContents();

            Transform parent = plateStation != null
                ? plateStation.transform
                : prepRoot;

            GameObject root = new GameObject("Finished Dish");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f,
                (plateStation != null ? plateStation.contentsHeight : 0.035f) -
                (DishMoundBaseY - DishMoundHeight) + 0.001f, 0f);
            root.transform.localRotation = Quaternion.identity;
            _finishedDishRoot = root;

            List<IngredientDefinition> definitions = CollectDishIngredients(recipe);
            Color moundColor = BlendDishColor(definitions);

            GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mound.name = "Dish";
            mound.transform.SetParent(root.transform, false);
            mound.transform.localPosition = new Vector3(0f, DishMoundBaseY, 0f);
            mound.transform.localScale = new Vector3(DishMoundRadius * 2f,
                DishMoundHeight * 2f, DishMoundRadius * 2f);
            RemoveCollider(mound);
            ApplyMaterial(mound, CookingMaterialFactory.Lit(moundColor, 0.02f, 0.52f));

            BuildDishGarnish(root.transform, definitions);

            GameObject steamObject = new GameObject("Dish Steam");
            steamObject.transform.SetParent(root.transform, false);
            steamObject.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            SteamEmitter steam = steamObject.AddComponent<SteamEmitter>();
            steam.radius = 0.12f;
            steam.interval = 0.35f;

            if (labelCanvas != null)
            {
                _finishedLabel = WorldLabel.Create(labelCanvas, sceneCamera,
                    root.transform, "DISH READY!",
                    new Vector3(0f, 0.36f, 0f), 30,
                    new Color(1f, 0.87f, 0.35f, 1f));
            }

            Vector3 burst = root.transform.position + Vector3.up * 0.17f;
            CookingEffects.Burst(burst, recipe.themeColor, 26, 1.15f);
            CookingEffects.Burst(burst, new Color(1f, 0.86f, 0.35f, 1f), 14, 0.75f);
        }

        public void HideFinishedDish()
        {
            if (_finishedLabel != null)
            {
                Destroy(_finishedLabel.gameObject);
                _finishedLabel = null;
            }

            if (_finishedDishRoot != null)
            {
                Destroy(_finishedDishRoot);
                _finishedDishRoot = null;
            }
        }

        private static List<IngredientDefinition> CollectDishIngredients(
            RecipeDefinition recipe)
        {
            List<IngredientDefinition> definitions =
                new List<IngredientDefinition>();
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < recipe.steps.Count; i++)
            {
                RecipeStep step = recipe.steps[i];
                if (!step.NeedsIngredient || string.IsNullOrEmpty(step.ingredientId))
                    continue;
                if (step.ingredientId == "dish" || step.ingredientId == "knife")
                    continue;
                if (!seen.Add(step.ingredientId))
                    continue;

                IngredientDefinition definition = IngredientCatalog.Get(step.ingredientId);
                if (definition != null)
                    definitions.Add(definition);
            }

            return definitions;
        }

        private static Color BlendDishColor(List<IngredientDefinition> definitions)
        {
            Color color = Color.black;
            int count = 0;

            for (int i = 0; i < definitions.Count; i++)
            {
                IngredientDefinition definition = definitions[i];
                if (definition == null || definition.shape == IngredientShape.Bottle)
                    continue;
                color += definition.color;
                count++;
            }

            if (count == 0)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (definitions[i] == null)
                        continue;
                    color += definitions[i].color;
                    count++;
                }
            }

            if (count == 0)
                return new Color(0.8f, 0.72f, 0.55f, 1f);

            color /= count;
            color.a = 1f;
            return color;
        }

        private void BuildDishGarnish(Transform parent,
            List<IngredientDefinition> definitions)
        {
            int solidIndex = 0;
            int sauceLayers = 0;
            int sprinkleLayers = 0;

            for (int i = 0; i < definitions.Count; i++)
            {
                IngredientDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                Material main = CookingMaterialFactory.Lit(
                    definition.color, 0.02f, 0.5f);
                Material accent = CookingMaterialFactory.Lit(
                    definition.accentColor, 0.02f, 0.55f);

                if (definition.shape == IngredientShape.Bottle)
                {
                    float radius = Mathf.Max(0.055f, 0.14f - sauceLayers * 0.03f);
                    float y = DishSurfaceY(0f) + 0.006f + sauceLayers * 0.006f;
                    DishPrimitive(PrimitiveType.Cylinder, parent,
                        new Vector3(0f, y, 0f),
                        new Vector3(radius * 2f, 0.0028f, radius * 2f),
                        CookingMaterialFactory.Lit(definition.color, 0.05f, 0.78f));
                    sauceLayers++;
                    continue;
                }

                if (definition.shape == IngredientShape.Shaker)
                {
                    float lift = sprinkleLayers * 0.03f;
                    for (int s = 0; s < 14; s++)
                    {
                        float angle = s * Mathf.PI * 2f / 14f + sprinkleLayers * 0.7f;
                        float distance = 0.02f + (s % 4) * 0.032f;
                        float size = Random.Range(0.009f, 0.017f);
                        DishPrimitive(PrimitiveType.Sphere, parent,
                            new Vector3(Mathf.Cos(angle) * distance,
                                DishSurfaceY(distance) + 0.008f + lift,
                                Mathf.Sin(angle) * distance),
                            Vector3.one * size, s % 3 == 0 ? accent : main);
                    }
                    sprinkleLayers++;
                    continue;
                }

                float spreadAngle = solidIndex * 2.399f;
                solidIndex++;

                if (definition.shape == IngredientShape.Bowl)
                {
                    float distance = 0.075f;
                    float size = 0.105f;
                    Vector3 position = new Vector3(
                        Mathf.Cos(spreadAngle) * distance,
                        DishSurfaceY(distance) + size * 0.12f,
                        Mathf.Sin(spreadAngle) * distance);
                    DishPrimitive(PrimitiveType.Sphere, parent, position,
                        new Vector3(size, size * 0.32f, size), main);
                    DishPrimitive(PrimitiveType.Sphere, parent,
                        position + new Vector3(size * 0.18f, size * 0.16f,
                            -size * 0.1f),
                        new Vector3(size * 0.4f, size * 0.18f, size * 0.4f),
                        accent);
                    continue;
                }

                int pieces = definition.shape == IngredientShape.Slice ||
                             definition.shape == IngredientShape.Bunch ? 3 : 2;
                for (int p = 0; p < pieces; p++)
                {
                    float angle = spreadAngle + p * 1.9f;
                    float distance = 0.06f + (p % 3) * 0.042f;
                    float size = Random.Range(0.042f, 0.068f);
                    DishPrimitive(PrimitiveType.Cube, parent,
                        new Vector3(Mathf.Cos(angle) * distance,
                            DishSurfaceY(distance) + size * 0.35f,
                            Mathf.Sin(angle) * distance),
                        Vector3.one * size, p == 0 ? main : accent,
                        Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                }
            }
        }

        private static float DishSurfaceY(float distance)
        {
            float t = Mathf.Clamp01(distance / DishMoundRadius);
            return DishMoundBaseY + DishMoundHeight * Mathf.Sqrt(1f - t * t);
        }

        private static GameObject DishPrimitive(PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material,
            Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = "Dish " + type;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation.HasValue
                ? rotation.Value
                : Quaternion.identity;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return go;
        }

        public LiquidStream BeginPour(Color color)
        {
            EndPour();

            GameObject streamObject = new GameObject("Pour Stream");
            streamObject.transform.SetParent(prepRoot, false);

            _pourStream = streamObject.AddComponent<LiquidStream>();
            _pourStream.Initialize(color);
            return _pourStream;
        }

        public void EndPour()
        {
            if (_pourStream == null)
                return;

            Destroy(_pourStream.gameObject);
            _pourStream = null;
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static void HideKitchenObject(GameObject target)
        {
            if (target != null)
                target.SetActive(false);
        }

        private static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds();
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static GameObject FindPot()
        {
            GameObject pot = GameObject.Find("SM_Pot03");
            if (pot == null)
                pot = GameObject.Find("SM_Pot02");
            if (pot != null)
                return pot;

            Transform[] transforms = Object.FindObjectsByType<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                string name = transforms[i].name;
                if (name.StartsWith("SM_Pot") || name == "Pot03" || name == "Pot02")
                    return transforms[i].gameObject;
            }

            return null;
        }

        private static GameObject FindMicrowave()
        {
            GameObject microwave = GameObject.Find("SM_Microwave");
            if (microwave != null)
                return microwave;

            Transform[] transforms = Object.FindObjectsByType<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                string name = transforms[i].name;
                if (name.StartsWith("SM_Microwave") || name == "Microwave")
                    return transforms[i].gameObject;
            }

            return null;
        }
    }
}
