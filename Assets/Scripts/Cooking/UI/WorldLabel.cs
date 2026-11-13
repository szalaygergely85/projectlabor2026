using UnityEngine;
using UnityEngine.UI;

namespace CookingRehab
{
    /// <summary>
    /// A small screen-space label pinned to a world transform. Used to show the
    /// name of every ingredient floating above the prep board.
    /// </summary>
    public class WorldLabel : MonoBehaviour
    {
        private RectTransform _rect;
        private Text _text;
        private CanvasGroup _group;
        private Canvas _canvas;
        private Camera _camera;
        private Transform _target;
        private Vector3 _worldOffset;
        private bool _visible = true;

        public static WorldLabel Create(Canvas canvas, Camera camera, Transform target,
            string content, Vector3 worldOffset, int fontSize, Color color)
        {
            if (canvas == null || camera == null || target == null)
                return null;

            RectTransform rect = UIFactory.CreateRect(canvas.transform,
                "WorldLabel_" + content);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(260f, 36f);

            WorldLabel label = rect.gameObject.AddComponent<WorldLabel>();
            label._rect = rect;
            label._canvas = canvas;
            label._camera = camera;
            label._target = target;
            label._worldOffset = worldOffset;

            label._group = rect.gameObject.AddComponent<CanvasGroup>();
            label._group.alpha = 1f;
            label._group.interactable = false;
            label._group.blocksRaycasts = false;

            label._text = UIFactory.CreateText(rect, "Text", content, fontSize,
                color, TextAnchor.MiddleCenter);
            UIFactory.Stretch(label._text.rectTransform, 4f, 0f, 4f, 2f);

            Outline outline = label._text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.03f, 0.02f, 0.92f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);
            outline.useGraphicAlpha = true;

            return label;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_group != null)
                _group.alpha = visible ? 1f : 0f;
        }

        public void SetText(string content)
        {
            if (_text != null)
                _text.text = content;
        }

        public void SetFontSize(int size)
        {
            if (_text != null)
                _text.fontSize = size;
        }

        public void SetColor(Color color)
        {
            if (_text != null)
                _text.color = color;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!_visible || _camera == null || _canvas == null)
                return;

            Vector3 world = _target.position + _worldOffset;
            Vector3 viewport = _camera.WorldToViewportPoint(world);

            if (viewport.z <= 0f ||
                viewport.x < -0.1f || viewport.x > 1.1f ||
                viewport.y < -0.1f || viewport.y > 1.1f)
            {
                if (_group != null)
                    _group.alpha = 0f;
                return;
            }

            if (_group != null)
                _group.alpha = 1f;

            Vector2 screen = new Vector2(
                viewport.x * Screen.width,
                viewport.y * Screen.height);

            RectTransform canvasRect = _canvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screen, null, out local))
            {
                _rect.anchoredPosition = local;
            }
        }
    }
}
