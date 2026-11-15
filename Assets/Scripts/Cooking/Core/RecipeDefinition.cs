using System.Collections.Generic;
using UnityEngine;

namespace CookingRehab
{
    [CreateAssetMenu(fileName = "Recipe", menuName = "Cooking Rehab/Recipe")]
    public class RecipeDefinition : ScriptableObject
    {
        public string recipeId;
        public string displayName;
        public string displayNameEn;
        [TextArea(2, 5)] public string description;
        public string rehabFocus;
        public RecipeDifficulty difficulty;
        public int estimatedMinutes = 8;
        public Color themeColor = new Color(0.97f, 0.66f, 0.31f, 1f);
        public List<RecipeStep> steps = new List<RecipeStep>();
    }

    public static class RecipeCatalog
    {
        private static List<RecipeDefinition> _all;

        public static IReadOnlyList<RecipeDefinition> All
        {
            get
            {
                // With "Enter Play Mode Options" the domain is not reloaded
                // between Play sessions, so this static list survives while
                // the ScriptableObjects created for the previous session are
                // destroyed when Play stops. Unity reports destroyed objects
                // as null through its overloaded operator, so rebuild the
                // catalog instead of returning dead recipe cards that can no
                // longer be selected.
                if (_all == null || _all.Count == 0 || _all[0] == null)
                    _all = BuildAll();
                return _all;
            }
        }

        public static List<RecipeDefinition> GetByDifficulty(RecipeDifficulty difficulty)
        {
            List<RecipeDefinition> result = new List<RecipeDefinition>();
            IReadOnlyList<RecipeDefinition> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].difficulty == difficulty)
                    result.Add(all[i]);
            }
            return result;
        }

        private static List<RecipeDefinition> BuildAll()
        {
            List<RecipeDefinition> recipes = new List<RecipeDefinition>();

            recipes.Add(Make(
                "cucumber_salad",
                "Cucumber Salad",
                "Cucumber Salad",
                RecipeDifficulty.Easy,
                6,
                "Chop · place · sprinkle · pour · stir · serve",
                new Color(0.42f, 0.76f, 0.45f, 1f),
                "A fresh starter: chop the cucumber on the cutting board, mix it with garlic, salt and vinegar, then serve it on the plate.",
                AddStep("Place the cucumber on the board",
                    "Pinch the cucumber slices and release them in the middle of the cutting board.",
                    "cucumber", "board"),
                ChopStep("Chop the cucumber",
                    "Pinch the knife, then move it up and down over the cutting board 4 times.",
                    "cucumber", 4),
                AddStep("Add the cucumber to the bowl",
                    "Pinch the chopped cucumber and release it over the mixing bowl.",
                    "cucumber", "bowl"),
                SprinkleStep("Add the garlic",
                    "Circle your open hand over the mixing bowl 3 times to sprinkle the garlic.",
                    "garlic", "bowl"),
                SprinkleStep("Season with salt",
                    "Circle your open hand over the mixing bowl 3 times to sprinkle the salt.",
                    "salt", "bowl"),
                PourStep("Pour in the vinegar",
                    "Pinch the vinegar bottle, move over the mixing bowl, and tilt your palm outward for 1.5 s.",
                    "vinegar", "bowl", 1.5f),
                StirStep("Mix the salad",
                    "Move your hand over the mixing bowl and stir in slow circles for 2 turns.",
                    "bowl", 2),
                ServeStep("Serve the salad",
                    "Pinch the finished dish above the mixing bowl, carry it to the plate and release.",
                    "bowl")
            ));

            recipes.Add(Make(
                "ethiopian_salata",
                "Ethiopian Salata",
                "Ethiopian Salata",
                RecipeDifficulty.Easy,
                9,
                "Chopping every vegetable · reach · pour · stir · serve",
                new Color(0.9f, 0.4f, 0.32f, 1f),
                "Tomato, onion and jalapeno: chop every vegetable on the cutting board, then toss it with the dressing and serve.",
                AddStep("Place the tomato on the board",
                    "Pinch the tomato and release it in the middle of the cutting board.",
                    "tomato", "board"),
                ChopStep("Chop the tomato",
                    "Pinch the knife, then move it up and down over the cutting board 4 times.",
                    "tomato", 4),
                AddStep("Add the tomato to the bowl",
                    "Pinch the chopped tomato and release it over the mixing bowl.",
                    "tomato", "bowl"),
                AddStep("Place the onion on the board",
                    "Pinch the onion and release it in the middle of the cutting board.",
                    "onion", "board"),
                ChopStep("Chop the onion",
                    "Pinch the knife, then move it up and down over the cutting board 3 times.",
                    "onion", 3),
                AddStep("Add the onion to the bowl",
                    "Pinch the chopped onion and release it over the mixing bowl.",
                    "onion", "bowl"),
                AddStep("Place the jalapeno on the board",
                    "Pinch the jalapeno and release it in the middle of the cutting board.",
                    "jalapeno", "board"),
                ChopStep("Chop the jalapeno",
                    "Pinch the knife, then move it up and down over the cutting board 3 times.",
                    "jalapeno", 3),
                AddStep("Add the jalapeno to the bowl",
                    "Pinch the chopped jalapeno and release it over the mixing bowl.",
                    "jalapeno", "bowl"),
                PourStep("Drizzle the dressing",
                    "Pinch the lemon dressing and tilt your palm over the mixing bowl for 1.5 s.",
                    "lemon_oil", "bowl", 1.5f),
                StirStep("Toss gently",
                    "Move your hand over the mixing bowl and stir in circles for 2 turns.",
                    "bowl", 2),
                ServeStep("Serve the salata",
                    "Pinch the finished dish above the mixing bowl, carry it to the plate and release.",
                    "bowl")
            ));

            recipes.Add(Make(
                "tomato_egg",
                "Tomato and Egg Stir-fry",
                "Tomato and Egg Stir-fry",
                RecipeDifficulty.Medium,
                9,
                "Chop · precise pour · continuous stir · serve",
                new Color(0.96f, 0.48f, 0.22f, 1f),
                "A classic home dish: chop the tomato, heat the oil, add egg, tomato and spring onion, season, stir-fry and serve.",
                AddStep("Place the tomato on the board",
                    "Pinch the tomato and release it in the middle of the cutting board.",
                    "tomato", "board"),
                ChopStep("Chop the tomato",
                    "Pinch the knife, then move it up and down over the cutting board 5 times.",
                    "tomato", 5),
                PourStep("Heat the oil",
                    "Pinch the cooking oil and tilt your palm over the pot for 1.5 s.",
                    "cooking_oil", "pot", 1.5f),
                AddStep("Pour in the egg",
                    "Pinch the bowl of beaten egg, move it over the pot, and release to pour it in.",
                    "egg", "pot"),
                AddStep("Add the chopped tomato",
                    "Pinch the chopped tomato on the cutting board and place it in the pot.",
                    "tomato", "pot"),
                AddStep("Place the spring onion on the board",
                    "Pinch the spring onion and release it in the middle of the cutting board.",
                    "green_onion", "board"),
                ChopStep("Chop the spring onion",
                    "Pinch the knife, then move it up and down over the cutting board 3 times.",
                    "green_onion", 3),
                AddStep("Add the spring onion",
                    "Pinch the chopped spring onion and place it in the pot.",
                    "green_onion", "pot"),
                SprinkleStep("Season with salt",
                    "Circle your open hand over the pot 3 times to sprinkle the salt.",
                    "salt", "pot"),
                StirStep("Stir-fry",
                    "Move your hand over the pot and stir in circles for 4 turns.",
                    "pot", 4),
                HeatStep("Let the stir-fry finish",
                    "Rest your open hand over the pot while the stir-fry cooks for 2 s.",
                    "pot", 2f),
                ServeStep("Serve the stir-fry",
                    "Pinch the finished dish above the pot, carry it to the plate and release.",
                    "pot")
            ));

            recipes.Add(Make(
                "steamed_tofu",
                "Steamed Tofu with Soy Sauce",
                "Steamed Tofu with Soy Sauce",
                RecipeDifficulty.Medium,
                8,
                "Chopping tofu and spring onion · microwave heating · slow pours",
                new Color(0.48f, 0.79f, 0.69f, 1f),
                "Cut the tofu into cubes, heat it in the microwave, then top it with spring onion, soy sauce and sesame oil.",
                AddStep("Place the tofu on the board",
                    "Pinch the tofu and release it in the middle of the cutting board.",
                    "tofu", "board"),
                ChopStep("Cut the tofu into cubes",
                    "Pinch the knife, then move it up and down over the cutting board 4 times.",
                    "tofu", 4),
                AddStep("Put the tofu in the microwave",
                    "Pinch the cubed tofu and release it over the microwave.",
                    "tofu", "microwave"),
                HeatStep("Heat the tofu",
                    "Hold your open hand over the microwave and keep it there for 3 s.",
                    "microwave", 3f),
                AddStep("Place the spring onion on the board",
                    "Pinch the spring onion and release it in the middle of the cutting board.",
                    "green_onion", "board"),
                ChopStep("Chop the spring onion",
                    "Pinch the knife, then move it up and down over the cutting board 3 times.",
                    "green_onion", 3),
                // The tofu is already in the microwave, so the spring onion
                // and both sauces go in there with it, and only then is the
                // finished dish carried to the plate: all the food first,
                // seasoning afterwards.
                AddStep("Add spring onion",
                    "Pinch the chopped spring onion and release it over the microwave, on top of the tofu.",
                    "green_onion", "microwave"),
                PourStep("Drizzle soy sauce",
                    "Pinch the soy sauce and tilt your palm over the microwave for 2 s.",
                    "soy_sauce", "microwave", 2f),
                PourStep("Add sesame oil",
                    "Pinch the sesame oil and tilt your palm over the microwave for 1.5 s.",
                    "sesame_oil", "microwave", 1.5f),
                ServeStep("Serve the steamed tofu",
                    "Pinch the heated tofu above the microwave, carry it to the plate and release.",
                    "microwave")
            ));

            recipes.Add(Make(
                "shiro_wat",
                "Shiro Wat",
                "Shiro Wat",
                RecipeDifficulty.Hard,
                10,
                "Chop · sprinkle · pour · continuous stir · serve",
                new Color(0.86f, 0.62f, 0.27f, 1f),
                "A longer stew: chop the onion, sprinkle the chickpea flour evenly, keep stirring to avoid lumps and serve from the pot.",
                AddStep("Place the onion on the board",
                    "Pinch the onion and release it in the middle of the cutting board.",
                    "onion", "board"),
                ChopStep("Chop the onion",
                    "Pinch the knife, then move it up and down over the cutting board 5 times.",
                    "onion", 5),
                AddStep("Add the chopped onion",
                    "Pinch the chopped onion and place it in the pot.",
                    "onion", "pot"),
                AddStep("Add the garlic",
                    "Pinch the garlic and place it in the pot.",
                    "garlic", "pot"),
                SprinkleStep("Sprinkle the chickpea flour",
                    "Circle your open hand over the pot 4 times to sprinkle the chickpea flour.",
                    "shiro", "pot", HandUsage.Any, 4),
                PourStep("Add water",
                    "Pinch the water and tilt your palm over the pot for 2 s.",
                    "water", "pot", 2f),
                PourStep("Add cooking oil",
                    "Pinch the cooking oil and tilt your palm over the pot for 1.5 s.",
                    "cooking_oil", "pot", 1.5f),
                StirStep("Stir to prevent lumps",
                    "Move your hand over the pot and stir in circles for 5 turns.",
                    "pot", 5),
                HeatStep("Simmer the stew",
                    "Hold your open hand over the pot while the stew simmers for 2.5 s.",
                    "pot", 2.5f),
                ServeStep("Serve the shiro",
                    "Pinch the finished dish above the pot, carry it to the plate and release.",
                    "pot")
            ));

            recipes.Add(Make(
                "doro_wat",
                "Doro Wat",
                "Doro Wat",
                RecipeDifficulty.Hard,
                14,
                "Alternating hands · chopping every ingredient · precise sprinkling · serve",
                new Color(0.87f, 0.32f, 0.16f, 1f),
                "An advanced recipe: alternate hands to place, chop and add the onion and chicken, pour water, stir continuously and serve the stew.",
                AddStep("Left hand: place the red onion",
                    "Use the hand on the left of the screen to pinch the red onion and release it on the cutting board.",
                    "red_onion", "board", HandUsage.Left),
                ChopStep("Right hand: chop the onion",
                    "Use the hand on the right of the screen to grab the knife and chop 4 times.",
                    "red_onion", 4, HandUsage.Right),
                AddStep("Left hand: add the chopped onion",
                    "Use the hand on the left of the screen to pinch the chopped onion and place it in the pot.",
                    "red_onion", "pot", HandUsage.Left),
                AddStep("Right hand: place the chicken on the board",
                    "Use the hand on the right of the screen to pinch the chicken and release it on the cutting board.",
                    "chicken", "board", HandUsage.Right),
                ChopStep("Right hand: chop the chicken",
                    "Use the hand on the right of the screen to grab the knife and chop the chicken 3 times.",
                    "chicken", 3, HandUsage.Right),
                AddStep("Right hand: add the chicken",
                    "Use the hand on the right of the screen to pinch the chopped chicken and place it in the pot.",
                    "chicken", "pot", HandUsage.Right),
                SprinkleStep("Left hand: sprinkle berbere",
                    "Use the hand on the left of the screen and circle it over the pot 3 times to sprinkle the berbere.",
                    "berbere", "pot", HandUsage.Left),
                AddStep("Right hand: add niter kibbeh",
                    "Use the hand on the right of the screen to pinch the spiced butter and place it in the pot.",
                    "niter_kibbeh", "pot", HandUsage.Right),
                PourStep("Left hand: add water",
                    "Use the hand on the left of the screen to pinch the water and tilt your palm for 2.5 s.",
                    "water", "pot", 2.5f, HandUsage.Left),
                StirStep("Right hand: stir",
                    "Use the hand on the right of the screen to stir in circles over the pot for 4 turns.",
                    "pot", 4, HandUsage.Right),
                StirStep("Left hand: keep stirring",
                    "Switch to the hand on the left of the screen and stir for 3 more turns.",
                    "pot", 3, HandUsage.Left),
                ServeStep("Serve the doro wat",
                    "Pinch the finished dish above the pot, carry it to the plate and release.",
                    "pot")
            ));

            return recipes;
        }

        private static RecipeStep AddStep(string title, string instruction,
            string ingredientId, string stationId, HandUsage hand = HandUsage.Any)
        {
            return new RecipeStep
            {
                title = title,
                instruction = instruction,
                action = CookingActionType.AddIngredient,
                ingredientId = ingredientId,
                stationId = stationId,
                requiredHand = hand
            };
        }

        private static RecipeStep HeatStep(string title, string instruction,
            string stationId, float seconds, HandUsage hand = HandUsage.Any)
        {
            return new RecipeStep
            {
                title = title,
                instruction = instruction,
                action = CookingActionType.Heat,
                stationId = stationId,
                requiredHeatSeconds = seconds,
                requiredHand = hand
            };
        }

        private static RecipeStep SprinkleStep(string title, string instruction,
            string ingredientId, string stationId, HandUsage hand = HandUsage.Any,
            int turns = 3)
        {
            return new RecipeStep
            {
                title = title,
                instruction = instruction,
                action = CookingActionType.Sprinkle,
                ingredientId = ingredientId,
                stationId = stationId,
                requiredSprinkleTurns = turns,
                requiredHand = hand
            };
        }

        private static RecipeStep PourStep(string title, string instruction,
            string ingredientId, string stationId, float seconds, HandUsage hand = HandUsage.Any)
        {
            return new RecipeStep
            {
                title = title,
                instruction = instruction,
                action = CookingActionType.Pour,
                ingredientId = ingredientId,
                stationId = stationId,
                requiredPourSeconds = seconds,
                requiredHand = hand
            };
        }

        private static RecipeStep StirStep(string title, string instruction,
            string stationId, int revolutions, HandUsage hand = HandUsage.Any)
        {
            return new RecipeStep
            {
                title = title,
                instruction = instruction,
                action = CookingActionType.Stir,
                stationId = stationId,
                requiredRevolutions = revolutions,
                requiredHand = hand
            };
        }

        private static RecipeStep ChopStep(string title, string instruction,
            string ingredientId, int chops, HandUsage hand = HandUsage.Any)
        {
            return new RecipeStep
            {
                title = title,
                instruction = instruction,
                action = CookingActionType.Chop,
                ingredientId = ingredientId,
                stationId = "board",
                requiredChops = chops,
                requiredHand = hand
            };
        }

        private static RecipeStep ServeStep(string title, string instruction,
            string sourceStationId, HandUsage hand = HandUsage.Any)
        {
            return new RecipeStep
            {
                title = title,
                instruction = instruction,
                action = CookingActionType.Serve,
                ingredientId = "dish",
                stationId = "plate",
                sourceStationId = sourceStationId,
                requiredHand = hand
            };
        }

        private static RecipeDefinition Make(string id, string displayName,
            string displayNameEn, RecipeDifficulty difficulty, int minutes,
            string rehabFocus, Color themeColor, string description,
            params RecipeStep[] steps)
        {
            RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            recipe.name = id;
            recipe.recipeId = id;
            recipe.displayName = displayName;
            recipe.displayNameEn = displayNameEn;
            recipe.description = description;
            recipe.rehabFocus = rehabFocus;
            recipe.difficulty = difficulty;
            recipe.estimatedMinutes = minutes;
            recipe.themeColor = themeColor;
            recipe.steps = new List<RecipeStep>(steps);
            return recipe;
        }
    }
}
