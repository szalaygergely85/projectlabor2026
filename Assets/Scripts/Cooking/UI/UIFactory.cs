using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CookingRehab
{
    /// <summary>
    /// Small runtime UGUI toolkit: a CJK-capable font, rounded sprites and
    /// helpers to lay out screens without any prefabs or imported assets.
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;
        private static Sprite _rounded;
        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _plain;

        public static Font UIFont
        {
            get
            {
                if (_font == null)
                    _font = ResolveFont();
                return _font;
            }
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null)
                    _rounded = BuildRoundedSprite(64, 20);
                return _rounded;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (_circle == null)
                    _circle = BuildCircleSprite(64);
                return _circle;
            }
        }

        public static Sprite RingSprite
        {
            get
            {
                if (_ring == null)
                    _ring = BuildRingSprite(96, 44f, 35f);
                return _ring;
            }
        }

        public static Sprite PlainSprite
        {
            get
            {
                if (_plain == null)
                    _plain = BuildPlainSprite();
                return _plain;
            }
        }

        private static Font ResolveFont()
        {
            string[] preferred =
            {
                "PingFang SC", "Heiti SC", "STHeiti", "Hiragino Sans GB",
                "Microsoft YaHei UI", "Microsoft YaHei", "SimHei",
                "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC",
                "WenQuanYi Micro Hei", "Arial Unicode MS"
            };

            string[] installed = null;
            try
            {
                installed = Font.GetOSInstalledFontNames();
            }
            catch (Exception)
            {
                installed = null;
            }

            if (installed != null)
            {
                for (int p = 0; p < preferred.Length; p++)
                {
                    for (int i = 0; i < installed.Length; i++)
                    {
                        if (installed[i] == null)
                            continue;

                        bool match = string.Equals(installed[i], preferred[p],
                            StringComparison.OrdinalIgnoreCase);
                        if (!match)
                            match = installed[i].IndexOf(preferred[p],
                                StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!match)
                            continue;

                        try
                        {
                            Font font = Font.CreateDynamicFontFromOSFont(installed[i], 32);
                            if (font != null)
                                return font;
                        }
                        catch (Exception)
                        {
                            // Try the next installed font.
                        }
                    }
                }
            }

            try
            {
                Font font = Font.CreateDynamicFontFromOSFont("PingFang SC", 32);
                if (font != null)
                    return font;
            }
            catch (Exception)
            {
                // Fall through to the built-in font.
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Canvas CreateCanvas(string name, int sortingOrder, bool interactive)
        {
            GameObject go = new GameObject(name,
                typeof(Canvas), typeof(CanvasScaler));

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (interactive)
            {
                go.AddComponent<GraphicRaycaster>();
                EnsureEventSystem();
            }

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
                return;

            GameObject go = new GameObject("EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            go.name = "Cooking EventSystem";
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreatePanel(Transform parent, string name, Color color,
            bool rounded = true, bool raycast = false, bool sliced = true)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded ? RoundedSprite : PlainSprite;
            image.type = rounded && sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image CreateImage(Transform parent, string name, Sprite sprite,
            Color color, Image.Type type)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string content,
            int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform rect = CreateRect(parent, name);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = UIFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1.05f;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label,
            int fontSize, Color background, Color foreground, UnityAction onClick)
        {
            Image image = CreatePanel(parent, name, Color.white, true, true);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
            colors.selectedColor = background;
            colors.disabledColor = new Color(background.r, background.g,
                background.b, background.a * 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text text = CreateText(image.transform, "Label", label, fontSize,
                foreground, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8f, 4f, 8f, 4f);

            if (onClick != null)
                button.onClick.AddListener(onClick);

            return button;
        }

        public static void Stretch(RectTransform rect, float left, float top,
            float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        public static void SetScreenPosition(RectTransform rect, Vector2 screenPosition)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPosition, null, out local))
            {
                rect.anchoredPosition = local;
            }
        }

        private static Sprite BuildRoundedSprite(int size, int radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CookingRounded";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            float inner = half - radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half) - inner;
                    float py = Mathf.Abs(y + 0.5f - half) - inner;
                    float outside = Mathf.Sqrt(
                        Mathf.Max(px, 0f) * Mathf.Max(px, 0f) +
                        Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
                    float inside = Mathf.Min(Mathf.Max(px, py), 0f);
                    float distance = outside + inside - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Vector4 border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite BuildCircleSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CookingCircle";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            float radius = half - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildRingSprite(int size, float outer, float inner)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CookingRing";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alphaOuter = Mathf.Clamp01(outer - distance + 0.5f);
                    float alphaInner = Mathf.Clamp01(inner - distance + 0.5f);
                    float alpha = Mathf.Clamp01(alphaOuter - alphaInner);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildPlainSprite()
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.name = "CookingPlain";
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
