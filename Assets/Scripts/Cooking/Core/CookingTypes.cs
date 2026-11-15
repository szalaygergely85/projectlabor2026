using System;
using System.Collections.Generic;
using UnityEngine;

namespace CookingRehab
{
    public enum RecipeDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    public enum CookingActionType
    {
        AddIngredient = 0,
        Sprinkle = 1,
        Pour = 2,
        Stir = 3,
        Chop = 4,
        Serve = 5,
        Heat = 6
    }

    public enum HandUsage
    {
        Any = 0,
        Left = 1,
        Right = 2
    }

    public enum HandSide
    {
        Left = 0,
        Right = 1
    }

    public enum HandGesture
    {
        None = 0,
        OpenPalm = 1,
        Pinch = 2,
        Pour = 3,
        Fist = 4,
        Point = 5
    }

    public enum IngredientShape
    {
        Sphere = 0,
        Capsule = 1,
        Cube = 2,
        Slice = 3,
        Bowl = 4,
        Shaker = 5,
        Bottle = 6,
        Bunch = 7,
        Knife = 8,
        Dish = 9
    }

    [Serializable]
    public class RecipeStep
    {
        public string title;
        [TextArea(2, 4)] public string instruction;
        public CookingActionType action;
        public string ingredientId;
        public string stationId = "pot";
        // Where a Serve step takes the dish from (the plate is always the target).
        public string sourceStationId = "pot";
        public int requiredRevolutions;
        public int requiredChops;
        // Sprinkle steps are done by circling an open hand over the station,
        // like stirring: one full revolution counts as one turn.
        public int requiredSprinkleTurns;
        public float requiredPourSeconds;
        // How long an open hand must be held over the station for a Heat
        // step (the microwave or a simmering pot).
        public float requiredHeatSeconds;
        public HandUsage requiredHand = HandUsage.Any;

        public bool NeedsIngredient
        {
            get
            {
                return action == CookingActionType.AddIngredient ||
                       action == CookingActionType.Sprinkle ||
                       action == CookingActionType.Pour ||
                       action == CookingActionType.Chop ||
                       action == CookingActionType.Serve;
            }
        }
    }

    [Serializable]
    public class IngredientDefinition
    {
        public string id;
        public string nameZh;
        public string nameEn;
        public IngredientShape shape;
        public Color color;
        public Color accentColor;
        public float scale = 1f;
    }

    public static class IngredientCatalog
    {
        private static Dictionary<string, IngredientDefinition> _items;

        public static IngredientDefinition Get(string id)
        {
            EnsureBuilt();
            IngredientDefinition definition;
            if (!string.IsNullOrEmpty(id) && _items.TryGetValue(id, out definition))
                return definition;

            return new IngredientDefinition
            {
                id = string.IsNullOrEmpty(id) ? "unknown" : id,
                nameZh = string.IsNullOrEmpty(id) ? "食材" : id,
                nameEn = string.IsNullOrEmpty(id) ? "Ingredient" : id,
                shape = IngredientShape.Sphere,
                color = new Color(0.85f, 0.7f, 0.45f),
                accentColor = Color.white,
                scale = 1f
            };
        }

        private static void EnsureBuilt()
        {
            if (_items != null)
                return;

            _items = new Dictionary<string, IngredientDefinition>();

            Add("tomato", "番茄", "Tomato", IngredientShape.Sphere,
                new Color(0.88f, 0.16f, 0.11f), new Color(0.22f, 0.56f, 0.2f));
            Add("egg", "蛋液", "Beaten Egg", IngredientShape.Bowl,
                new Color(0.99f, 0.86f, 0.42f), new Color(0.99f, 0.97f, 0.9f));
            Add("green_onion", "葱花", "Spring Onion", IngredientShape.Bunch,
                new Color(0.2f, 0.66f, 0.26f), new Color(0.55f, 0.85f, 0.45f));
            Add("cucumber", "黄瓜片", "Cucumber", IngredientShape.Slice,
                new Color(0.36f, 0.72f, 0.3f), new Color(0.72f, 0.92f, 0.55f));
            Add("garlic", "蒜末", "Garlic", IngredientShape.Bowl,
                new Color(0.96f, 0.93f, 0.83f), new Color(0.85f, 0.8f, 0.65f));
            Add("salt", "盐", "Salt", IngredientShape.Shaker,
                new Color(0.98f, 0.98f, 1f), new Color(0.75f, 0.8f, 0.88f));
            Add("vinegar", "香醋", "Vinegar", IngredientShape.Bottle,
                new Color(0.42f, 0.2f, 0.09f), new Color(0.9f, 0.75f, 0.55f));
            Add("onion", "洋葱", "Onion", IngredientShape.Sphere,
                new Color(0.62f, 0.27f, 0.56f), new Color(0.85f, 0.65f, 0.82f));
            Add("jalapeno", "青辣椒", "Jalapeno", IngredientShape.Capsule,
                new Color(0.24f, 0.62f, 0.2f), new Color(0.65f, 0.88f, 0.4f));
            Add("lemon_oil", "柠檬油醋汁", "Lemon Dressing", IngredientShape.Bottle,
                new Color(0.95f, 0.83f, 0.25f), new Color(0.75f, 0.68f, 0.15f));
            Add("tofu", "豆腐", "Tofu", IngredientShape.Cube,
                new Color(0.97f, 0.96f, 0.87f), new Color(0.85f, 0.85f, 0.75f));
            Add("soy_sauce", "生抽", "Soy Sauce", IngredientShape.Bottle,
                new Color(0.2f, 0.1f, 0.04f), new Color(0.55f, 0.32f, 0.18f));
            Add("sesame_oil", "香油", "Sesame Oil", IngredientShape.Bottle,
                new Color(0.78f, 0.47f, 0.12f), new Color(0.98f, 0.85f, 0.5f));
            Add("cooking_oil", "食用油", "Cooking Oil", IngredientShape.Bottle,
                new Color(0.96f, 0.76f, 0.2f), new Color(0.75f, 0.6f, 0.15f));
            Add("shiro", "鹰嘴豆粉", "Shiro Powder", IngredientShape.Bowl,
                new Color(0.84f, 0.74f, 0.5f), new Color(0.65f, 0.52f, 0.32f));
            Add("water", "清水", "Water", IngredientShape.Bottle,
                new Color(0.55f, 0.8f, 0.96f), new Color(0.35f, 0.62f, 0.8f));
            Add("red_onion", "红洋葱", "Red Onion", IngredientShape.Sphere,
                new Color(0.55f, 0.1f, 0.34f), new Color(0.86f, 0.62f, 0.78f));
            Add("chicken", "鸡肉", "Chicken", IngredientShape.Capsule,
                new Color(0.76f, 0.43f, 0.26f), new Color(0.92f, 0.65f, 0.42f));
            Add("berbere", "柏柏尔香料", "Berbere Spice", IngredientShape.Shaker,
                new Color(0.86f, 0.24f, 0.07f), new Color(0.98f, 0.55f, 0.18f));
            Add("niter_kibbeh", "香料黄油", "Niter Kibbeh", IngredientShape.Bowl,
                new Color(0.99f, 0.8f, 0.19f), new Color(0.98f, 0.95f, 0.65f));

            // Tools and the served dish are props that the world spawns itself;
            // they never appear in recipe ingredient lists.
            Add("knife", "菜刀", "Knife", IngredientShape.Knife,
                new Color(0.76f, 0.79f, 0.82f), new Color(0.3f, 0.19f, 0.1f));
            Add("dish", "成品菜", "Dish", IngredientShape.Dish,
                new Color(0.82f, 0.57f, 0.3f), new Color(0.95f, 0.93f, 0.86f));
        }

        private static void Add(string id, string nameZh, string nameEn,
            IngredientShape shape, Color color, Color accent, float scale = 1f)
        {
            _items[id] = new IngredientDefinition
            {
                id = id,
                nameZh = nameZh,
                nameEn = nameEn,
                shape = shape,
                color = color,
                accentColor = accent,
                scale = scale
            };
        }
    }

    public static class CookingTheme
    {
        public static readonly Color Background = new Color(0.055f, 0.047f, 0.04f, 0.9f);
        public static readonly Color Panel = new Color(0.105f, 0.088f, 0.072f, 0.97f);
        public static readonly Color PanelSoft = new Color(0.15f, 0.125f, 0.1f, 0.96f);
        public static readonly Color Card = new Color(0.18f, 0.15f, 0.12f, 1f);
        public static readonly Color CardHover = new Color(0.24f, 0.2f, 0.16f, 1f);
        public static readonly Color Text = new Color(1f, 0.97f, 0.92f, 1f);
        public static readonly Color Muted = new Color(0.73f, 0.68f, 0.61f, 1f);
        public static readonly Color Accent = new Color(0.97f, 0.66f, 0.31f, 1f);
        public static readonly Color AccentDark = new Color(0.52f, 0.31f, 0.12f, 1f);
        public static readonly Color Success = new Color(0.46f, 0.85f, 0.51f, 1f);
        public static readonly Color Danger = new Color(0.91f, 0.35f, 0.3f, 1f);
        public static readonly Color LeftHand = new Color(0.36f, 0.8f, 0.96f, 1f);
        public static readonly Color RightHand = new Color(0.98f, 0.61f, 0.27f, 1f);
        public static readonly Color Wood = new Color(0.46f, 0.28f, 0.15f, 1f);
        public static readonly Color Metal = new Color(0.22f, 0.23f, 0.24f, 1f);
        public static readonly Color Ceramic = new Color(0.94f, 0.92f, 0.85f, 1f);
    }

    public static class CookingNames
    {
        public static string DifficultyZh(RecipeDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RecipeDifficulty.Easy: return "Easy";
                case RecipeDifficulty.Medium: return "Medium";
                default: return "Hard";
            }
        }

        public static Color DifficultyColor(RecipeDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RecipeDifficulty.Easy: return new Color(0.4f, 0.78f, 0.48f, 1f);
                case RecipeDifficulty.Medium: return new Color(0.95f, 0.72f, 0.25f, 1f);
                default: return new Color(0.9f, 0.38f, 0.3f, 1f);
            }
        }

        public static string StationZh(string stationId)
        {
            if (stationId == "bowl") return "Mixing Bowl";
            if (stationId == "plate") return "Plate";
            if (stationId == "board") return "Cutting Board";
            if (stationId == "microwave") return "Microwave";
            return "Pot";
        }

        public static string HandZh(HandUsage hand)
        {
            switch (hand)
            {
                case HandUsage.Left: return "the left hand";
                case HandUsage.Right: return "the right hand";
                default: return "either hand";
            }
        }

        public static string HandShortZh(HandSide side)
        {
            return side == HandSide.Left ? "Left hand" : "Right hand";
        }

        public static string GestureZh(HandGesture gesture)
        {
            switch (gesture)
            {
                case HandGesture.OpenPalm: return "Open palm";
                case HandGesture.Pinch: return "Pinch";
                case HandGesture.Pour: return "Pour";
                case HandGesture.Fist: return "Fist";
                case HandGesture.Point: return "Point";
                default: return "Idle";
            }
        }

        public static string ActionZh(CookingActionType action)
        {
            switch (action)
            {
                case CookingActionType.AddIngredient: return "Add ingredient";
                case CookingActionType.Sprinkle: return "Circle to sprinkle";
                case CookingActionType.Pour: return "Pour";
                case CookingActionType.Chop: return "Chop";
                case CookingActionType.Serve: return "Serve";
                case CookingActionType.Heat: return "Heat";
                default: return "Stir";
            }
        }
    }
}
