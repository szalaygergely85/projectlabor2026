using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CookingRehab
{
    /// <summary>
    /// Runtime built English UI. Three screens (recipe menu, cooking HUD and
    /// results), all pressed with a mouse or a hand pinch.
    /// </summary>
    public class CookingUI : MonoBehaviour
    {
        private class TabButton
        {
            public Image image;
            public Text text;
            public RecipeDifficulty? filter;
        }

        private CookingGameController _controller;
        private Canvas _canvas;
        private Canvas _labelCanvas;

        private GameObject _menuRoot;
        private RectTransform _menuPanel;
        private GameObject _hudRoot;
        private GameObject _resultsRoot;
        private RectTransform _cardsRoot;
        private readonly List<TabButton> _tabs = new List<TabButton>();
        private readonly List<GameObject> _cards = new List<GameObject>();
        private RecipeDifficulty? _filter;

        private Text _menuTipText;
        private int _menuHandState = -1;
        private Image _pinchCursor;
        private Image _leftAimCursor;
        private Image _rightAimCursor;

        // Guards against a single physical click being handled twice: once by
        // our own hit test and once by the EventSystem.
        private Button _lastActivated;
        private float _lastActivatedAt = -10f;

        // One physical pinch can reach the menu twice: MediaPipeVRHand's own
        // pinch edge and the gesture recognizer's PinchStarted arrive a few
        // frames apart. Anything shorter than this is the same click.
        private float _lastUiActionAt = -10f;
        private float _menuShownAt = -10f;

        private Text _recipeTitleText;
        private Text _stepCounterText;
        private Text _timerText;
        private Text _stepTitleText;
        private Text _instructionText;
        private Text _targetText;
        private Text _progressText;
        private Image _progressFill;
        private CanvasGroup _toastGroup;
        private Text _toastText;
        private Coroutine _toastRoutine;

        private Text _leftHandText;
        private Text _rightHandText;
        private Image _leftHandDot;
        private Image _rightHandDot;

        private Text _resultsRecipeText;
        private readonly Text[] _resultStars = new Text[3];
        private Text _resultsLabels;
        private Text _resultsValues;

        public Canvas LabelCanvas
        {
            get { return _labelCanvas; }
        }

        public bool IsMenuVisible
        {
            get { return _menuRoot != null && _menuRoot.activeSelf; }
        }

        public bool IsResultsVisible
        {
            get { return _resultsRoot != null && _resultsRoot.activeSelf; }
        }

        public void Build(Camera camera, CookingGameController controller)
        {
            if (_canvas != null)
                return;

            _controller = controller;
            _canvas = UIFactory.CreateCanvas("Cooking UI", 100, true);
            _labelCanvas = UIFactory.CreateCanvas("Cooking World Labels", 90, false);

            BuildMenu();
            BuildHud();
            BuildResults();
            BuildPinchCursor();

            ShowMenu();
        }

        /// <summary>
        /// A small ring that follows the tracked hand while the recipe menu is
        /// open. It shows exactly where a pinch will land, so the menu never
        /// looks dead: the player can see the dot move onto a card.
        /// </summary>
        private void BuildPinchCursor()
        {
            _pinchCursor = UIFactory.CreateImage(_canvas.transform, "Pinch Cursor",
                UIFactory.RingSprite, new Color(1f, 0.78f, 0.35f, 0.55f),
                Image.Type.Simple);
            // Anchored to the bottom-left corner of the overlay canvas, so
            // its position is simply the screen point divided by the canvas
            // scale factor.
            UIFactory.Place(_pinchCursor.rectTransform, Vector2.zero,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
            _pinchCursor.gameObject.SetActive(false);

            _leftAimCursor = BuildAimCursor("Left Aim");
            _rightAimCursor = BuildAimCursor("Right Aim");
        }

        /// <summary>
        /// Small ring drawn at the exact screen point the cooking controller
        /// uses for grabbing and for station checks. The rendered hand can
        /// be much larger than an ingredient and hide it completely, so
        /// without this dot the player has no way to tell why a pinch that
        /// looks like it is on the food does not grab. Moving the dot onto
        /// the ingredient is the actual aiming task.
        /// </summary>
        private Image BuildAimCursor(string name)
        {
            Image cursor = UIFactory.CreateImage(_canvas.transform, name,
                UIFactory.RingSprite, new Color(1f, 0.9f, 0.7f, 0.5f),
                Image.Type.Simple);
            UIFactory.Place(cursor.rectTransform, Vector2.zero,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            cursor.gameObject.SetActive(false);
            return cursor;
        }

        // ------------------------------------------------------------------
        // Menu
        // ------------------------------------------------------------------

        private void BuildMenu()
        {
            Image background = UIFactory.CreatePanel(_canvas.transform, "Menu",
                new Color(0.035f, 0.028f, 0.022f, 0.95f), false, true);
            UIFactory.Fill(background.rectTransform);
            _menuRoot = background.gameObject;

            Image panel = UIFactory.CreatePanel(_menuRoot.transform, "Menu Panel",
                CookingTheme.Panel);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 940f));
            _menuPanel = panel.rectTransform;

            Image accent = UIFactory.CreatePanel(panel.transform, "Title Accent",
                CookingTheme.Accent, false, false);
            UIFactory.Place(accent.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(160f, 5f));

            Text title = UIFactory.CreateText(panel.transform, "Title", "Rehab Kitchen",
                54, CookingTheme.Text, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(1400f, 66f));

            Text subtitle = UIFactory.CreateText(panel.transform, "Subtitle",
                "Complete recipes with real hand gestures · grab · chop · cook · serve",
                22, CookingTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(1400f, 30f));

            _menuTipText = UIFactory.CreateText(panel.transform, "Tip",
                "Hold both hands in front of the camera · pinch to click · or use the mouse",
                18, new Color(0.97f, 0.76f, 0.48f, 1f), TextAnchor.MiddleCenter);
            UIFactory.Place(_menuTipText.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(1400f, 26f));

            BuildTabs(panel.transform);

            _cardsRoot = UIFactory.CreateRect(panel.transform, "Cards");
            UIFactory.Place(_cardsRoot, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 620f));

            Text footer = UIFactory.CreateText(panel.transform, "Footer",
                "Each recipe trains different movements. Your results are recorded when you finish.",
                17, CookingTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1400f, 28f));

            RefreshTabs();
            BuildCards();
        }

        private void BuildTabs(Transform panel)
        {
            CreateTab(panel, "All", null, -261f);
            CreateTab(panel, "Easy", RecipeDifficulty.Easy, -87f);
            CreateTab(panel, "Medium", RecipeDifficulty.Medium, 87f);
            CreateTab(panel, "Hard", RecipeDifficulty.Hard, 261f);
        }

        private void CreateTab(Transform panel, string label, RecipeDifficulty? filter,
            float x)
        {
            Button button = UIFactory.CreateButton(panel, "Tab_" + label, label, 20,
                CookingTheme.PanelSoft, CookingTheme.Muted, delegate { SetFilter(filter); });
            button.transition = Selectable.Transition.None;
            UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(x, -190f), new Vector2(160f, 52f));

            TabButton tab = new TabButton();
            tab.image = button.targetGraphic as Image;
            tab.text = button.GetComponentInChildren<Text>();
            tab.filter = filter;
            _tabs.Add(tab);
        }

        private void SetFilter(RecipeDifficulty? filter)
        {
            _filter = filter;
            BuildCards();
            RefreshTabs();
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                TabButton tab = _tabs[i];
                bool active = tab.filter == _filter;

                if (tab.image != null)
                    tab.image.color = active ? CookingTheme.Accent : CookingTheme.PanelSoft;
                if (tab.text != null)
                {
                    tab.text.color = active
                        ? new Color(0.16f, 0.1f, 0.04f, 1f)
                        : CookingTheme.Muted;
                    tab.text.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }

        private void BuildCards()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] != null)
                {
                    _cards[i].SetActive(false);
                    Destroy(_cards[i]);
                }
            }
            _cards.Clear();

            List<RecipeDefinition> recipes = new List<RecipeDefinition>();
            if (_filter.HasValue)
            {
                recipes.AddRange(RecipeCatalog.GetByDifficulty(_filter.Value));
            }
            else
            {
                IReadOnlyList<RecipeDefinition> all = RecipeCatalog.All;
                for (int i = 0; i < all.Count; i++)
                    recipes.Add(all[i]);
            }

            for (int i = 0; i < recipes.Count; i++)
            {
                int row = i / 3;
                int column = i % 3;
                int inRow = Mathf.Clamp(recipes.Count - row * 3, 1, 3);
                float x = (column - (inRow - 1) * 0.5f) * 485f;
                float y = row == 0 ? 100f : -170f;
                CreateRecipeCard(recipes[i], new Vector2(x, y));
            }
        }

        private void CreateRecipeCard(RecipeDefinition recipe, Vector2 position)
        {
            Image card = UIFactory.CreatePanel(_cardsRoot, "Card_" + recipe.recipeId,
                Color.white, true, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, new Vector2(440f, 250f));

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            ColorBlock colors = button.colors;
            colors.normalColor = CookingTheme.Card;
            colors.highlightedColor = CookingTheme.CardHover;
            colors.pressedColor = CookingTheme.AccentDark;
            colors.selectedColor = CookingTheme.Card;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            RecipeDefinition captured = recipe;
            button.onClick.AddListener(delegate
            {
                // A stale click from the previous screen (or a duplicate
                // activation in the same frame) must not start a recipe
                // silently while the menu is already gone.
                if (_menuRoot == null || !_menuRoot.activeSelf)
                    return;
                if (_controller != null)
                    _controller.StartRecipe(captured);
            });

            _cards.Add(card.gameObject);

            Image bar = UIFactory.CreatePanel(card.transform, "Accent",
                recipe.themeColor, false, false);
            UIFactory.Place(bar.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 250f));

            Text title = UIFactory.CreateText(card.transform, "Title",
                recipe.displayName, 30, CookingTheme.Text, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 30;
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(290f, 38f));

            Image difficultyPill = UIFactory.CreatePanel(card.transform, "Difficulty",
                CookingNames.DifficultyColor(recipe.difficulty), true, false);
            UIFactory.Place(difficultyPill.rectTransform, new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(88f, 28f));

            Text difficultyText = UIFactory.CreateText(difficultyPill.transform, "Text",
                CookingNames.DifficultyZh(recipe.difficulty), 16,
                new Color(0.14f, 0.1f, 0.05f, 1f), TextAnchor.MiddleCenter);
            difficultyText.fontStyle = FontStyle.Bold;
            UIFactory.Fill(difficultyText.rectTransform);

            Text description = UIFactory.CreateText(card.transform, "Description",
                recipe.description, 17, new Color(0.87f, 0.82f, 0.76f, 1f),
                TextAnchor.UpperLeft);
            UIFactory.Place(description.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(22f, -82f), new Vector2(396f, 64f));

            Text focus = UIFactory.CreateText(card.transform, "Focus",
                "Training focus:  " + recipe.rehabFocus, 15,
                new Color(0.97f, 0.72f, 0.45f, 1f), TextAnchor.MiddleLeft);
            UIFactory.Place(focus.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(22f, -148f), new Vector2(396f, 34f));

            Text meta = UIFactory.CreateText(card.transform, "Meta",
                recipe.steps.Count + (recipe.steps.Count == 1 ? " step" : " steps") +
                " · ~" + recipe.estimatedMinutes + " min",
                15, CookingTheme.Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(meta.rectTransform, new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(22f, 18f), new Vector2(240f, 28f));

            Text start = UIFactory.CreateText(card.transform, "Start",
                "Start training →", 18, CookingTheme.Accent, TextAnchor.MiddleRight);
            UIFactory.Place(start.rectTransform, new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-22f, 16f), new Vector2(180f, 30f));
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private void BuildHud()
        {
            RectTransform hud = UIFactory.CreateRect(_canvas.transform, "HUD");
            UIFactory.Fill(hud);
            _hudRoot = hud.gameObject;

            Image topBar = UIFactory.CreatePanel(hud, "Top Bar",
                new Color(0.07f, 0.058f, 0.048f, 0.94f), false, false);
            topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            topBar.rectTransform.anchoredPosition = Vector2.zero;
            topBar.rectTransform.sizeDelta = new Vector2(0f, 104f);

            Button back = UIFactory.CreateButton(topBar.transform, "Back", "‹  Recipes",
                21, CookingTheme.PanelSoft, CookingTheme.Text, delegate
                {
                    if (_controller != null)
                        _controller.ReturnToMenu();
                });
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(200f, 60f));

            _recipeTitleText = UIFactory.CreateText(topBar.transform, "Recipe Title",
                "", 28, CookingTheme.Text, TextAnchor.MiddleCenter);
            _recipeTitleText.fontStyle = FontStyle.Bold;
            UIFactory.Place(_recipeTitleText.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(760f, 38f));

            _stepCounterText = UIFactory.CreateText(topBar.transform, "Step Counter",
                "", 18, CookingTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Place(_stepCounterText.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(760f, 28f));

            _timerText = UIFactory.CreateText(topBar.transform, "Timer", "00:00",
                28, CookingTheme.Accent, TextAnchor.MiddleRight);
            UIFactory.Place(_timerText.rectTransform, new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(200f, 50f));

            Image banner = UIFactory.CreatePanel(hud, "Instruction Banner",
                new Color(0.105f, 0.088f, 0.072f, 0.95f));
            UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1040f, 138f));

            Image bannerAccent = UIFactory.CreatePanel(banner.transform, "Accent",
                CookingTheme.Accent, false, false);
            UIFactory.Place(bannerAccent.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(5f, 138f));

            _stepTitleText = UIFactory.CreateText(banner.transform, "Step Title",
                "", 26, CookingTheme.Accent, TextAnchor.MiddleLeft);
            _stepTitleText.fontStyle = FontStyle.Bold;
            UIFactory.Place(_stepTitleText.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(960f, 34f));

            _instructionText = UIFactory.CreateText(banner.transform, "Instruction",
                "", 20, CookingTheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Place(_instructionText.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(24f, -48f), new Vector2(970f, 46f));

            _targetText = UIFactory.CreateText(banner.transform, "Target",
                "", 16, CookingTheme.Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(_targetText.rectTransform, new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(24f, 12f), new Vector2(650f, 24f));

            _progressText = UIFactory.CreateText(banner.transform, "Progress Text",
                "", 16, CookingTheme.Accent, TextAnchor.MiddleRight);
            UIFactory.Place(_progressText.rectTransform, new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-24f, 12f), new Vector2(300f, 24f));

            Image barBackground = UIFactory.CreatePanel(hud, "Progress Bar",
                new Color(0.04f, 0.033f, 0.027f, 0.9f), false, false);
            UIFactory.Place(barBackground.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(1040f, 10f));

            _progressFill = UIFactory.CreateImage(barBackground.transform, "Fill",
                UIFactory.RoundedSprite, CookingTheme.Accent, Image.Type.Filled);
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = 0;
            _progressFill.fillAmount = 0f;
            UIFactory.Stretch(_progressFill.rectTransform, 1f, 1f, 1f, 1f);

            BuildHandBadges(hud);

            Text hint = UIFactory.CreateText(hud, "Gesture Hint",
                "Pinch to grab · chop with the knife · tilt to pour · open-hand circles to stir",
                16, new Color(0.75f, 0.7f, 0.64f, 0.9f), TextAnchor.MiddleCenter);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(700f, 26f));

            Image toast = UIFactory.CreatePanel(hud, "Toast",
                new Color(0.12f, 0.1f, 0.08f, 0.96f));
            UIFactory.Place(toast.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(700f, 62f));
            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.interactable = false;
            _toastGroup.blocksRaycasts = false;
            _toastText = UIFactory.CreateText(toast.transform, "Text", "",
                21, CookingTheme.Text, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_toastText.rectTransform, 12f, 6f, 12f, 6f);

            _hudRoot.SetActive(false);
        }

        private void BuildHandBadges(Transform hud)
        {
            Image left = UIFactory.CreatePanel(hud, "Left Hand Badge",
                new Color(0.08f, 0.075f, 0.065f, 0.9f));
            UIFactory.Place(left.rectTransform, new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(24f, 20f), new Vector2(330f, 78f));

            _leftHandDot = UIFactory.CreateImage(left.transform, "Dot",
                UIFactory.CircleSprite, CookingTheme.LeftHand, Image.Type.Simple);
            UIFactory.Place(_leftHandDot.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(16f, 16f));

            _leftHandText = UIFactory.CreateText(left.transform, "Text",
                "Left hand\nNot detected", 17, CookingTheme.Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(_leftHandText.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(270f, 64f));

            Image right = UIFactory.CreatePanel(hud, "Right Hand Badge",
                new Color(0.08f, 0.075f, 0.065f, 0.9f));
            UIFactory.Place(right.rectTransform, new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(330f, 78f));

            _rightHandDot = UIFactory.CreateImage(right.transform, "Dot",
                UIFactory.CircleSprite, CookingTheme.RightHand, Image.Type.Simple);
            UIFactory.Place(_rightHandDot.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(16f, 16f));

            _rightHandText = UIFactory.CreateText(right.transform, "Text",
                "Right hand\nNot detected", 17, CookingTheme.Muted, TextAnchor.MiddleLeft);
            UIFactory.Place(_rightHandText.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(270f, 64f));
        }

        // ------------------------------------------------------------------
        // Results
        // ------------------------------------------------------------------

        private void BuildResults()
        {
            RectTransform root = UIFactory.CreateRect(_canvas.transform, "Results");
            UIFactory.Fill(root);
            _resultsRoot = root.gameObject;

            Image dim = UIFactory.CreatePanel(root, "Dim",
                new Color(0.03f, 0.024f, 0.02f, 0.93f), false, true);
            UIFactory.Fill(dim.rectTransform);

            Image panel = UIFactory.CreatePanel(root, "Results Panel",
                CookingTheme.Panel);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 660f));

            Text title = UIFactory.CreateText(panel.transform, "Title", "Session Complete",
                50, CookingTheme.Text, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(900f, 64f));

            _resultsRecipeText = UIFactory.CreateText(panel.transform, "Recipe",
                "", 24, CookingTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Place(_resultsRecipeText.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(900f, 32f));

            for (int i = 0; i < _resultStars.Length; i++)
            {
                Text star = UIFactory.CreateText(panel.transform, "Star " + i,
                    "★", 64, new Color(0.32f, 0.27f, 0.22f, 1f), TextAnchor.MiddleCenter);
                UIFactory.Place(star.rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 92f, 150f),
                    new Vector2(88f, 80f));
                _resultStars[i] = star;
            }

            _resultsLabels = UIFactory.CreateText(panel.transform, "Labels",
                "Time\nGrabs\nStir turns\nPour time\nMistakes",
                20, CookingTheme.Muted, TextAnchor.MiddleLeft);
            _resultsLabels.lineSpacing = 1.5f;
            UIFactory.Place(_resultsLabels.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-160f, -10f), new Vector2(320f, 210f));

            _resultsValues = UIFactory.CreateText(panel.transform, "Values",
                "", 22, CookingTheme.Text, TextAnchor.MiddleRight);
            _resultsValues.lineSpacing = 1.36f;
            UIFactory.Place(_resultsValues.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(160f, -10f), new Vector2(320f, 210f));

            Button retry = UIFactory.CreateButton(panel.transform, "Retry", "Try Again",
                22, CookingTheme.Accent, new Color(0.16f, 0.1f, 0.04f, 1f), delegate
                {
                    if (_controller != null)
                        _controller.RestartRecipe();
                });
            UIFactory.Place(retry.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(-170f, 52f), new Vector2(300f, 64f));

            Button menu = UIFactory.CreateButton(panel.transform, "Menu", "Recipes",
                22, CookingTheme.PanelSoft, CookingTheme.Text, delegate
                {
                    if (_controller != null)
                        _controller.ReturnToMenu();
                });
            UIFactory.Place(menu.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(170f, 52f), new Vector2(300f, 64f));

            _resultsRoot.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        public void ShowMenu()
        {
            _menuRoot.SetActive(true);
            _hudRoot.SetActive(false);
            _resultsRoot.SetActive(false);
            _menuShownAt = Time.unscaledTime;
            ClearToast();
        }

        public void ShowHud(RecipeDefinition recipe)
        {
            _menuRoot.SetActive(false);
            _hudRoot.SetActive(true);
            _resultsRoot.SetActive(false);

            if (_recipeTitleText != null)
                _recipeTitleText.text = recipe != null ? recipe.displayName : "";
            if (_stepCounterText != null)
                _stepCounterText.text = "";
            if (_timerText != null)
                _timerText.text = "00:00";
            if (_progressFill != null)
                _progressFill.fillAmount = 0f;
            if (_progressText != null)
                _progressText.text = "";

            ClearToast();
        }

        public void SetStep(RecipeDefinition recipe, int index)
        {
            if (recipe == null || index < 0 || index >= recipe.steps.Count)
                return;

            RecipeStep step = recipe.steps[index];

            if (_stepCounterText != null)
            {
                _stepCounterText.text = "Step " + (index + 1) + " of " +
                                        recipe.steps.Count;
            }

            if (_stepTitleText != null)
                _stepTitleText.text = step.title;

            if (_instructionText != null)
                _instructionText.text = step.instruction;

            if (_targetText != null)
            {
                _targetText.text = "Target: " + CookingNames.StationZh(step.stationId) +
                                   "   ·   " + CookingNames.HandZh(step.requiredHand) +
                                   "   ·   " + CookingNames.ActionZh(step.action);
            }

            float fill = 0f;
            string label;
            switch (step.action)
            {
                case CookingActionType.Pour:
                    label = "0.0 / " + step.requiredPourSeconds.ToString("F1") + " s";
                    break;
                case CookingActionType.Stir:
                    label = "0 / " + step.requiredRevolutions + " turns";
                    break;
                case CookingActionType.Chop:
                    label = "0 / " + step.requiredChops + " chops";
                    break;
                case CookingActionType.Sprinkle:
                    label = "0 / " + step.requiredSprinkleTurns + " turns";
                    break;
                case CookingActionType.Heat:
                    label = "0.0 / " + step.requiredHeatSeconds.ToString("F1") + " s";
                    break;
                case CookingActionType.Serve:
                    label = "Grab the dish from the " +
                            CookingNames.StationZh(step.sourceStationId);
                    break;
                default:
                    label = "Grab the highlighted ingredient";
                    break;
            }

            SetProgress(fill, label);
        }

        public void SetProgress(float fill, string label)
        {
            if (_progressFill != null)
                _progressFill.fillAmount = Mathf.Clamp01(fill);
            if (_progressText != null)
                _progressText.text = label;
        }

        public void SetTimer(float seconds)
        {
            if (_timerText != null)
                _timerText.text = FormatTime(seconds);
        }

        public void ShowToast(string message, Color color)
        {
            if (string.IsNullOrEmpty(message) || _toastGroup == null)
                return;

            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);

            _toastText.text = message;
            _toastText.color = color;
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _toastGroup.alpha = 1f;
            yield return new WaitForSeconds(1.35f);

            float elapsed = 0f;
            const float fade = 0.35f;
            while (elapsed < fade)
            {
                elapsed += Time.deltaTime;
                _toastGroup.alpha = 1f - Mathf.Clamp01(elapsed / fade);
                yield return null;
            }

            _toastGroup.alpha = 0f;
            _toastRoutine = null;
        }

        private void ClearToast()
        {
            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
                _toastRoutine = null;
            }
            if (_toastGroup != null)
                _toastGroup.alpha = 0f;
        }

        public void RefreshHands(HandGestureState left, HandGestureState right)
        {
            UpdateBadge(_leftHandText, _leftHandDot, left, "Left hand", CookingTheme.LeftHand);
            UpdateBadge(_rightHandText, _rightHandDot, right, "Right hand", CookingTheme.RightHand);
            UpdateMenuTip(left, right);
            UpdatePinchCursor(left, right);
        }

        /// <summary>
        /// The menu pinch cursor is the aiming aid: it sits exactly where
        /// HandlePinchClick tests, so a player can move it onto a card before
        /// pinching instead of guessing.
        /// </summary>
        private void UpdatePinchCursor(HandGestureState left, HandGestureState right)
        {
            bool menuOpen = _menuRoot != null && _menuRoot.activeSelf;
            bool hudOpen = _hudRoot != null && _hudRoot.activeSelf;

            // Aiming rings for both hands while a recipe runs.
            UpdateAimCursor(_leftAimCursor, left, hudOpen, CookingTheme.LeftHand);
            UpdateAimCursor(_rightAimCursor, right, hudOpen, CookingTheme.RightHand);

            if (_pinchCursor == null)
                return;

            HandGestureState state;
            bool hasHand;
            if (!menuOpen)
            {
                hasHand = false;
                state = default(HandGestureState);
            }
            else if (left.isTracked && (left.isPinching || !right.isTracked))
            {
                hasHand = true;
                state = left;
            }
            else if (right.isTracked)
            {
                hasHand = true;
                state = right;
            }
            else
            {
                hasHand = false;
                state = default(HandGestureState);
            }

            if (!hasHand)
            {
                if (_pinchCursor.gameObject.activeSelf)
                    _pinchCursor.gameObject.SetActive(false);
                return;
            }

            if (!_pinchCursor.gameObject.activeSelf)
                _pinchCursor.gameObject.SetActive(true);

            Vector2 screen = new Vector2(
                state.viewportPosition.x * Screen.width,
                state.viewportPosition.y * Screen.height);
            float scale = Mathf.Max(0.0001f, _canvas.scaleFactor);
            _pinchCursor.rectTransform.anchoredPosition = screen / scale;

            _pinchCursor.color = state.isPinching
                ? new Color(1f, 0.62f, 0.28f, 0.95f)
                : new Color(1f, 0.78f, 0.35f, 0.55f);
        }

        private void UpdateAimCursor(Image cursor, HandGestureState state,
            bool visible, Color color)
        {
            if (cursor == null)
                return;

            bool show = visible && state.isTracked;
            if (cursor.gameObject.activeSelf != show)
                cursor.gameObject.SetActive(show);
            if (!show)
                return;

            Vector2 screen = new Vector2(
                state.viewportPosition.x * Screen.width,
                state.viewportPosition.y * Screen.height);
            float scale = Mathf.Max(0.0001f, _canvas.scaleFactor);
            cursor.rectTransform.anchoredPosition = screen / scale;

            cursor.color = state.isPinching
                ? new Color(color.r, color.g, color.b, 0.95f)
                : new Color(color.r, color.g, color.b, 0.45f);
            cursor.rectTransform.sizeDelta = state.isPinching
                ? new Vector2(34f, 34f)
                : new Vector2(24f, 24f);
        }

        /// <summary>
        /// The menu tip doubles as a live hand-tracking indicator. Without it
        /// a player whose hands are not detected sees a normal-looking menu
        /// where pinching silently does nothing.
        /// </summary>
        private void UpdateMenuTip(HandGestureState left, HandGestureState right)
        {
            if (_menuTipText == null)
                return;

            int state = (left.isTracked ? 1 : 0) | (right.isTracked ? 2 : 0);
            if (state == _menuHandState)
                return;

            _menuHandState = state;

            switch (state)
            {
                case 3:
                    _menuTipText.text =
                        "Both hands detected · pinch near a recipe — the ring shows your pinch point";
                    _menuTipText.color = new Color(0.66f, 0.9f, 0.6f, 1f);
                    break;
                case 1:
                case 2:
                    _menuTipText.text =
                        "One hand detected (" + (state == 1 ? "left" : "right") +
                        ") · pinch near a recipe, or click with the mouse";
                    _menuTipText.color = new Color(0.97f, 0.76f, 0.48f, 1f);
                    break;
                default:
                    _menuTipText.text =
                        "No hands detected · hold both hands in front of the camera · or click with the mouse";
                    _menuTipText.color = new Color(0.95f, 0.55f, 0.45f, 1f);
                    break;
            }
        }

        private void UpdateBadge(Text text, Image dot, HandGestureState state,
            string prefix, Color color)
        {
            if (text == null)
                return;

            if (!state.isTracked)
            {
                text.text = prefix + " · Not detected\nPlace your hand in view";
                text.color = new Color(0.62f, 0.56f, 0.5f, 1f);
                if (dot != null)
                    dot.color = new Color(0.36f, 0.32f, 0.29f, 1f);
                return;
            }

            string action;
            if (state.isPouring)
                action = "Pouring";
            else if (state.isStirring)
                action = "Stirring";
            else
                action = CookingNames.GestureZh(state.gesture);

            text.text = prefix + " · Tracked\n" + action;
            text.color = CookingTheme.Text;
            if (dot != null)
                dot.color = color;
        }

        public void ShowResults(RecipeDefinition recipe, float elapsedSeconds,
            int grabs, int stirRevolutions, float pourSeconds, int wrongDrops, int stars)
        {
            _menuRoot.SetActive(false);
            _hudRoot.SetActive(false);
            _resultsRoot.SetActive(true);
            ClearToast();

            if (_resultsRecipeText != null)
            {
                _resultsRecipeText.text = recipe != null
                    ? recipe.displayName
                    : "Session complete";
            }

            for (int i = 0; i < _resultStars.Length; i++)
            {
                _resultStars[i].color = i < stars
                    ? new Color(1f, 0.78f, 0.27f, 1f)
                    : new Color(0.32f, 0.27f, 0.22f, 1f);
            }

            if (_resultsValues != null)
            {
                _resultsValues.text =
                    FormatTime(elapsedSeconds) + "\n" +
                    grabs + "\n" +
                    stirRevolutions + "\n" +
                    pourSeconds.ToString("F1") + " s\n" +
                    wrongDrops;
            }
        }

        public bool HandlePinchClick(HandGestureState state)
        {
            if (!state.isTracked)
                return false;

            // The same physical pinch may be reported twice through two
            // different paths; execute it only once.
            if (Time.unscaledTime - _lastUiActionAt < 0.4f)
                return false;

            // A hand that is already pinched when the menu appears should
            // not immediately start a recipe.
            if (_menuRoot != null && _menuRoot.activeSelf &&
                Time.unscaledTime - _menuShownAt < 0.35f)
                return false;

            Vector2 screen = new Vector2(
                state.viewportPosition.x * Screen.width,
                state.viewportPosition.y * Screen.height);

            // Three independent paths, tried from most to least exact. None
            // of them needs a working input module, so a pinch still works
            // when the EventSystem is missing or its module is broken.
            Button button = FindButtonAt(screen);

            if (button == null && EventSystem.current != null)
                button = RaycastButton(screen);

            if (button == null && _menuRoot != null && _menuRoot.activeSelf)
            {
                // First allow a generous near-miss on any button (tabs and
                // cards), then fall back to the closest card at any distance.
                // Hand tracking is never pixel-exact for a patient, and a
                // menu that ignores a pinch feels broken.
                button = FindNearestButton(screen, NearMissRadius());
                if (button == null && _menuPanel != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        _menuPanel, screen, null))
                {
                    button = FindNearestCard(screen);
                }
            }
            else if (button == null && _resultsRoot != null &&
                     _resultsRoot.activeSelf)
            {
                button = FindNearestButton(screen, NearMissRadius());
            }

            if (button == null)
            {
                Debug.Log("[Cooking] Pinch at " + screen +
                          " hit no button.");
                return false;
            }

            Debug.Log("[Cooking] Pinch at " + screen + " -> " + button.name);
            Activate(button);
            return true;
        }

        /// <summary>
        /// Mouse clicks bypass the EventSystem as well. If the normal UI
        /// pipeline works, the duplicate activation is ignored; if it does
        /// not, the menu still answers.
        /// </summary>
        private void Update()
        {
            if (_canvas == null)
                return;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButtonDown(0))
                return;

            Vector2 mouse = Input.mousePosition;
            if (mouse.x < 0f || mouse.y < 0f ||
                mouse.x > Screen.width || mouse.y > Screen.height)
                return;

            // A mouse must hit the button exactly: clicking empty menu space
            // (for example to focus the Game view) should never start a
            // recipe. The generous nearest-card fallback is for pinches only.
            Button button = FindButtonAt(mouse);
            if (button != null)
            {
                Debug.Log("[Cooking] Mouse click -> " + button.name);
                Activate(button);
            }
#endif
        }

        private static bool IsClickable(Button button)
        {
            return button != null && button.IsActive() && button.IsInteractable();
        }

        private void Activate(Button button)
        {
            if (button == null)
                return;

            // The same physical click can reach both our direct path and the
            // EventSystem. Executing the same button twice in one instant
            // would restart a recipe, so ignore the duplicate.
            float now = Time.unscaledTime;
            if (button == _lastActivated && now - _lastActivatedAt < 0.5f)
                return;
            _lastActivated = button;
            _lastActivatedAt = now;
            _lastUiActionAt = now;

            if (_controller != null)
                _controller.PlayUiClick();

            // Invoke the button's own listener directly instead of
            // synthesising a pointer event: this is the part that must work
            // even when the EventSystem is not.
            button.onClick.Invoke();
        }

        private Button FindButtonAt(Vector2 screenPoint)
        {
            if (_canvas == null)
                return null;

            Button[] buttons = _canvas.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (!IsClickable(button))
                    continue;

                RectTransform rect = button.transform as RectTransform;
                if (rect == null)
                    continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(
                        rect, screenPoint, null))
                    return button;
            }

            return null;
        }

        private Button RaycastButton(Vector2 screenPoint)
        {
            PointerEventData pointer = new PointerEventData(EventSystem.current);
            pointer.position = screenPoint;
            pointer.pressPosition = screenPoint;
            pointer.clickCount = 1;
            pointer.button = PointerEventData.InputButton.Left;

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);

            for (int i = 0; i < results.Count; i++)
            {
                Button button = results[i].gameObject.GetComponentInParent<Button>();
                if (IsClickable(button))
                    return button;
            }

            return null;
        }

        private float NearMissRadius()
        {
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            return Mathf.Max(90f, 120f * scale);
        }

        private Button FindNearestCard(Vector2 screenPoint)
        {
            Button best = null;
            float bestDistance = float.MaxValue;
            Vector3[] corners = new Vector3[4];

            for (int i = 0; i < _cards.Count; i++)
            {
                GameObject card = _cards[i];
                if (card == null || !card.activeInHierarchy)
                    continue;

                Button button = card.GetComponent<Button>();
                if (!IsClickable(button))
                    continue;

                RectTransform rect = card.transform as RectTransform;
                if (rect == null)
                    continue;

                float distance = DistanceToRect(rect, screenPoint, corners);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = button;
                }
            }

            return best;
        }

        private Button FindNearestButton(Vector2 screenPoint, float maxDistance)
        {
            if (_canvas == null)
                return null;

            Button[] buttons = _canvas.GetComponentsInChildren<Button>(false);
            Button best = null;
            float bestDistance = maxDistance;
            Vector3[] corners = new Vector3[4];

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (!IsClickable(button))
                    continue;

                RectTransform rect = button.transform as RectTransform;
                if (rect == null)
                    continue;

                float distance = DistanceToRect(rect, screenPoint, corners);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = button;
                }
            }

            return best;
        }

        private static float DistanceToRect(RectTransform rect, Vector2 screenPoint,
            Vector3[] corners)
        {
            rect.GetWorldCorners(corners);

            // The UI canvas is ScreenSpaceOverlay, so world coordinates
            // are already screen pixels and the rect can be tested
            // directly against the pinch position.
            Vector2 center = ((Vector2)corners[0] + (Vector2)corners[2]) * 0.5f;
            Vector2 halfSize = new Vector2(
                Mathf.Abs(corners[2].x - corners[0].x) * 0.5f,
                Mathf.Abs(corners[2].y - corners[0].y) * 0.5f);
            Vector2 delta = screenPoint - center;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(delta.x, -halfSize.x, halfSize.x),
                Mathf.Clamp(delta.y, -halfSize.y, halfSize.y));
            return (delta - clamped).magnitude;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = total / 60;
            int remainder = total % 60;
            return minutes.ToString("00") + ":" + remainder.ToString("00");
        }
    }
}
