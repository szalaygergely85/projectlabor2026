using UnityEngine;

namespace CookingRehab
{
    /// <summary>
    /// A place ingredients can be dropped: the pot, the mixing bowl or the plate.
    /// Draws a soft ring marker and pulses while it is the active target. When a
    /// step completes, the processed ingredient stays here, so the dish visibly
    /// builds up as the recipe goes on.
    /// </summary>
    public class CookingStation : MonoBehaviour
    {
        public string stationId = "pot";
        public string displayName = "Pot";
        public float radius = 0.22f;
        public float viewportRadius = 0.085f;
        public Transform anchor;

        // Local height and area where placed / cooked pieces accumulate.
        public float contentsHeight = 0.12f;
        public float contentsRadius = 0.12f;

        public Color idleColor = new Color(0.55f, 0.45f, 0.32f, 1f);
        public Color activeColor = new Color(1f, 0.72f, 0.3f, 1f);

        public const int LabelFontSize = 18;
        public static readonly Color LabelColor = new Color(1f, 0.85f, 0.6f, 1f);

        private Transform _markerRoot;
        private Material _markerMaterial;
        private Vector3 _markerBaseScale = Vector3.one;
        private bool _highlighted;
        private bool _hintActive;
        private float _pulse;

        private Transform _contentsRoot;
        private int _pourLayers;
        private float _contentsTop;
        private float _stirSpeed;

        public Vector3 AnchorPosition
        {
            get { return anchor != null ? anchor.position : transform.position; }
        }

        public void Initialize(string id, string labelText, float stationRadius)
        {
            stationId = id;
            displayName = labelText;
            radius = stationRadius;

            if (anchor == null)
            {
                GameObject anchorObject = new GameObject("Anchor");
                anchorObject.transform.SetParent(transform, false);
                anchor = anchorObject.transform;
            }

            BuildMarker();
            SetHighlighted(false);
        }

        private void BuildMarker()
        {
            if (_markerRoot != null)
                return;

            GameObject marker = new GameObject("Marker");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            _markerRoot = marker.transform;

            Material source = CookingMaterialFactory.Emissive(idleColor, 0.9f, 0.25f);
            _markerMaterial = new Material(source);
            _markerMaterial.name = "StationMarker_" + stationId;

            const int beadCount = 26;
            for (int i = 0; i < beadCount; i++)
            {
                float angle = i * Mathf.PI * 2f / beadCount;
                GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bead.name = "Ring";
                bead.transform.SetParent(_markerRoot, false);
                bead.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                bead.transform.localScale = new Vector3(0.022f, 0.008f, 0.022f);

                Collider collider = bead.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);

                Renderer renderer = bead.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = _markerMaterial;
            }

            _markerBaseScale = _markerRoot.localScale;
        }

        public bool Contains(Vector3 worldPosition)
        {
            return (worldPosition - AnchorPosition).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// Lifts the ring marker above a raised station surface, such as the
        /// cutting board, so the beads are not buried inside the board mesh.
        /// </summary>
        public void SetMarkerHeight(float height)
        {
            if (_markerRoot != null)
                _markerRoot.localPosition = new Vector3(0f, height, 0f);
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            if (!highlighted)
                _hintActive = false;

            ApplyMarkerColor(highlighted ? activeColor : idleColor,
                highlighted ? (_hintActive ? 3.6f : 2.2f) : 0.55f);

            if (!highlighted && _markerRoot != null)
                _markerRoot.localScale = _markerBaseScale;
        }

        /// <summary>
        /// Emphasises the single ring marker while an ingredient is carried.
        /// No extra rings or text are added: one circle is the whole hint.
        /// </summary>
        public void SetPlacementHint(bool active)
        {
            _hintActive = active && _highlighted;
            ApplyMarkerColor(_highlighted ? activeColor : idleColor,
                _highlighted ? (_hintActive ? 3.6f : 2.2f) : 0.55f);
        }

        // ------------------------------------------------------------------
        // Cooked contents
        // ------------------------------------------------------------------

        /// <summary>
        /// Keeps a visual trace of a completed step at this station. The pieces
        /// stay until the recipe ends, so every ingredient visibly joins the
        /// dish instead of disappearing.
        /// </summary>
        public void AddProcessed(CookingIngredient ingredient,
            CookingActionType action)
        {
            if (ingredient == null || ingredient.definition == null)
                return;

            EnsureContentsRoot();

            switch (action)
            {
                case CookingActionType.Pour:
                    AddPourLayer(ingredient.definition);
                    break;
                case CookingActionType.Sprinkle:
                    AddSprinkles(ingredient.definition);
                    break;
                default:
                    AddAddedPieces(ingredient.definition);
                    break;
            }
        }

        private void EnsureContentsRoot()
        {
            if (_contentsRoot != null)
                return;

            GameObject root = new GameObject("Cooked Contents");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            _contentsRoot = root.transform;
            _contentsTop = contentsHeight;
            _pourLayers = 0;
        }

        private void AddAddedPieces(IngredientDefinition definition)
        {
            if (definition.shape == IngredientShape.Bottle)
            {
                AddPuddle(definition.color, contentsRadius * 0.8f);
                return;
            }

            if (definition.shape == IngredientShape.Bowl)
            {
                AddDollop(definition);
                return;
            }

            Material main = CookingMaterialFactory.Lit(
                definition.color, 0.02f, 0.5f);
            Material accent = CookingMaterialFactory.Lit(
                definition.accentColor, 0.02f, 0.55f);

            bool onPlate = stationId == "plate";
            int count = onPlate ? 4 : 7;
            float spread = contentsRadius * (onPlate ? 0.5f : 0.6f);
            float baseSize = Mathf.Clamp(
                contentsRadius * (onPlate ? 0.42f : 0.3f), 0.03f, 0.075f);

            for (int i = 0; i < count; i++)
            {
                Vector3 offset;
                if (onPlate)
                {
                    float gap = Mathf.Max(0.03f, contentsRadius * 0.28f);
                    offset = new Vector3(
                        ((i % 2) - 0.5f) * gap, 0f,
                        ((i / 2) - 0.5f) * gap);
                }
                else
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float distance = Mathf.Sqrt(Random.value) * spread;
                    offset = new Vector3(
                        Mathf.Cos(angle) * distance, 0f,
                        Mathf.Sin(angle) * distance);
                }

                float size = baseSize * Random.Range(0.8f, 1.2f);
                float baseY = onPlate
                    ? Mathf.Max(contentsHeight, _contentsTop - 0.005f)
                    : contentsHeight;
                float lift = onPlate ? 0f : Random.Range(0f, size * 0.8f);
                Vector3 position = new Vector3(
                    offset.x, baseY + size * 0.5f + lift, offset.z);
                Quaternion rotation = Quaternion.Euler(
                    0f, Random.Range(0f, 360f),
                    onPlate ? 0f : Random.Range(-12f, 12f));

                Primitive(PrimitiveType.Cube, _contentsRoot, position,
                    Vector3.one * size, i == 0 ? accent : main, rotation);

                _contentsTop = Mathf.Max(_contentsTop, position.y + size * 0.5f);
            }
        }

        private void AddDollop(IngredientDefinition definition)
        {
            Material main = CookingMaterialFactory.Lit(
                definition.color, 0.02f, 0.5f);
            Material accent = CookingMaterialFactory.Lit(
                definition.accentColor, 0.02f, 0.55f);

            float size = Mathf.Clamp(contentsRadius * 0.9f, 0.08f, 0.18f);
            float baseY = Mathf.Max(contentsHeight, _contentsTop - 0.005f);

            Primitive(PrimitiveType.Sphere, _contentsRoot,
                new Vector3(0f, baseY + size * 0.18f, 0f),
                new Vector3(size, size * 0.38f, size), main);
            Primitive(PrimitiveType.Sphere, _contentsRoot,
                new Vector3(size * 0.16f, baseY + size * 0.3f, -size * 0.1f),
                new Vector3(size * 0.4f, size * 0.22f, size * 0.4f), accent);

            _contentsTop = Mathf.Max(_contentsTop, baseY + size * 0.4f);
        }

        private void AddPuddle(Color color, float puddleRadius)
        {
            Material material = CookingMaterialFactory.Lit(color, 0.02f, 0.75f);
            const float height = 0.008f;

            Primitive(PrimitiveType.Cylinder, _contentsRoot,
                new Vector3(0f, contentsHeight + height * 0.5f, 0f),
                new Vector3(puddleRadius * 2f, height * 0.5f, puddleRadius * 2f),
                material);

            _contentsTop = Mathf.Max(_contentsTop, contentsHeight + height);
        }

        private void AddSprinkles(IngredientDefinition definition)
        {
            Material main = CookingMaterialFactory.Lit(
                definition.color, 0.02f, 0.35f);
            Material accent = CookingMaterialFactory.Lit(
                definition.accentColor, 0.02f, 0.4f);

            int count = Mathf.Clamp(
                Mathf.RoundToInt(contentsRadius * 150f), 16, 32);
            float spread = contentsRadius * 0.85f;
            float baseY = Mathf.Max(contentsHeight, _contentsTop) + 0.004f;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Mathf.Sqrt(Random.value) * spread;
                float size = Random.Range(0.007f, 0.016f);

                Primitive(PrimitiveType.Sphere, _contentsRoot,
                    new Vector3(Mathf.Cos(angle) * distance,
                        baseY + Random.Range(0f, 0.012f),
                        Mathf.Sin(angle) * distance),
                    new Vector3(size, size * 0.6f, size),
                    i % 4 == 0 ? accent : main);
            }

            _contentsTop = Mathf.Max(_contentsTop, baseY + 0.012f);
        }

        private void AddPourLayer(IngredientDefinition definition)
        {
            float layerRadius = Mathf.Max(0.06f, contentsRadius * 0.86f);
            const float height = 0.007f;
            float y = contentsHeight + 0.004f + _pourLayers * 0.006f;

            Material material = CookingMaterialFactory.Lit(
                definition.color, 0.05f, 0.7f);

            Primitive(PrimitiveType.Cylinder, _contentsRoot,
                new Vector3(0f, y, 0f),
                new Vector3(layerRadius * 2f, height * 0.5f, layerRadius * 2f),
                material);

            _pourLayers++;
            _contentsTop = Mathf.Max(_contentsTop, y + height);
        }

        /// <summary>Gives the cooked pieces a short swirl after a stir turn.</summary>
        public void StirContents()
        {
            _stirSpeed = Mathf.Min(420f, _stirSpeed + 140f);
        }

        /// <summary>Removes everything placed in this station (new recipe).</summary>
        public void ClearContents()
        {
            if (_contentsRoot != null)
            {
                Destroy(_contentsRoot.gameObject);
                _contentsRoot = null;
            }

            _pourLayers = 0;
            _contentsTop = contentsHeight;
            _stirSpeed = 0f;
            SetPlacementHint(false);
        }

        private void ApplyMarkerColor(Color color, float emission)
        {
            if (_markerMaterial == null)
                return;

            if (_markerMaterial.HasProperty("_BaseColor"))
                _markerMaterial.SetColor("_BaseColor", color);
            if (_markerMaterial.HasProperty("_Color"))
                _markerMaterial.SetColor("_Color", color);

            Color emissive = color * emission;
            if (_markerMaterial.HasProperty("_EmissiveColor"))
                _markerMaterial.SetColor("_EmissiveColor", emissive);
            if (_markerMaterial.HasProperty("_EmissionColor"))
                _markerMaterial.SetColor("_EmissionColor", emissive);
        }

        public void PulseSuccess(Color color)
        {
            CookingEffects.Burst(AnchorPosition + Vector3.up * 0.05f, color, 18, 1.05f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_markerRoot != null && _highlighted)
            {
                _pulse += dt;
                float speed = _hintActive ? 42f : 30f;
                _markerRoot.Rotate(0f, speed * dt, 0f, Space.Self);

                float scale = _hintActive
                    ? 1.14f + Mathf.Sin(_pulse * 4.2f) * 0.05f
                    : 1f + Mathf.Sin(_pulse * 3.4f) * 0.06f;
                _markerRoot.localScale = _markerBaseScale * scale;
            }

            if (_contentsRoot != null && _stirSpeed > 0.5f)
            {
                _contentsRoot.Rotate(0f, _stirSpeed * dt, 0f, Space.Self);
                _stirSpeed *= Mathf.Exp(-1.4f * dt);
            }
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material,
            Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation.HasValue
                ? rotation.Value
                : Quaternion.identity;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return go;
        }
    }
}
