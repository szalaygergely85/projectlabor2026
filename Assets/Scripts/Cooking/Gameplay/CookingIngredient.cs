using UnityEngine;

namespace CookingRehab
{
    /// <summary>
    /// A single pickable ingredient on the prep board. Grabbing is handled by
    /// CookingGameController; this component owns highlighting, return-home
    /// animation and the consumed state.
    /// </summary>
    public class CookingIngredient : MonoBehaviour
    {
        public string ingredientId;
        public IngredientDefinition definition;
        public int slotIndex;
        public GameObject visualRoot;
        public GameObject highlightRoot;
        public WorldLabel label;

        public Vector3 homePosition;
        public Quaternion homeRotation = Quaternion.identity;
        public float homeScale = 1f;

        public bool isHeld;
        public bool isConsumed;
        public bool isChopped;

        private bool _returning;
        private const float ReturnSpeed = 9f;

        public string DisplayName
        {
            get { return definition != null ? definition.nameEn : ingredientId; }
        }

        public void Initialize(string id, int slot, GameObject visual, GameObject highlight)
        {
            ingredientId = id;
            definition = IngredientCatalog.Get(id);
            slotIndex = slot;
            visualRoot = visual;
            highlightRoot = highlight;
            CaptureHome();
            SetHighlighted(false);
        }

        public void CaptureHome()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
            homeScale = transform.localScale.x;
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightRoot != null)
                highlightRoot.SetActive(highlighted && !isConsumed && !isHeld);
        }

        public void BeginGrab()
        {
            isHeld = true;
            _returning = false;
            if (highlightRoot != null)
                highlightRoot.SetActive(false);
        }

        public void EndGrab()
        {
            isHeld = false;
        }

        public void ReturnHome()
        {
            if (isConsumed)
                return;

            isHeld = false;
            _returning = true;

            if (visualRoot != null)
                visualRoot.SetActive(true);
            if (label != null)
                label.SetVisible(true);
        }

        public void ResetToHome()
        {
            isConsumed = false;
            isHeld = false;
            _returning = false;

            transform.position = homePosition;
            transform.rotation = homeRotation;
            transform.localScale = Vector3.one * homeScale;

            if (visualRoot != null)
                visualRoot.SetActive(true);
            if (label != null)
                label.SetVisible(true);

            SetHighlighted(false);
        }

        /// <summary>
        /// Replaces the whole-ingredient visual with a heap of chopped pieces.
        /// The prop stays on the cutting board and can be picked up again for
        /// the next cooking step.
        /// </summary>
        public void MarkChopped()
        {
            if (isChopped || isConsumed || definition == null)
                return;

            isChopped = true;
            isHeld = false;
            _returning = false;

            if (visualRoot != null)
                Destroy(visualRoot);
            if (highlightRoot != null)
                Destroy(highlightRoot);

            GameObject newHighlight;
            visualRoot = IngredientVisualFactory.CreateChopped(definition,
                out newHighlight);
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;
            highlightRoot = newHighlight;

            transform.localScale = Vector3.one * homeScale;
            if (label != null)
                label.SetText("Chopped " + definition.nameEn);

            SetHighlighted(false);
        }

        /// <summary>
        /// Puts the prop down at a fixed spot (the cutting board) and makes
        /// that spot its new home, so a failed later grab returns it there.
        /// </summary>
        public void SnapTo(Vector3 position, Quaternion rotation)
        {
            isHeld = false;
            _returning = false;

            homePosition = position;
            homeRotation = rotation;

            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = Vector3.one * homeScale;

            if (visualRoot != null)
                visualRoot.SetActive(true);
            if (label != null)
                label.SetVisible(true);

            SetHighlighted(false);
        }

        public void MarkConsumed()
        {
            isConsumed = true;
            isHeld = false;
            _returning = false;

            if (highlightRoot != null)
                highlightRoot.SetActive(false);
            if (visualRoot != null)
                visualRoot.SetActive(false);
            if (label != null)
                label.SetVisible(false);
        }

        private void Update()
        {
            if (!_returning || isHeld)
                return;

            float t = 1f - Mathf.Exp(-ReturnSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, homePosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRotation, t);
            transform.localScale = Vector3.Lerp(transform.localScale,
                Vector3.one * homeScale, t);

            if ((transform.position - homePosition).sqrMagnitude < 0.000025f &&
                Quaternion.Angle(transform.rotation, homeRotation) < 2f)
            {
                transform.position = homePosition;
                transform.rotation = homeRotation;
                transform.localScale = Vector3.one * homeScale;
                _returning = false;
            }
        }
    }
}
