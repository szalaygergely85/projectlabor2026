using UnityEngine;

namespace CookingRehab
{
    public static class IngredientVisualFactory
    {
        public static GameObject Create(IngredientDefinition definition, out GameObject highlightRoot)
        {
            if (definition == null)
                definition = IngredientCatalog.Get("unknown");

            float scale = Mathf.Max(0.4f, definition.scale);
            GameObject root = new GameObject("Visual_" + definition.id);
            Material main = CookingMaterialFactory.Lit(definition.color, 0.02f, 0.42f);
            Material accent = CookingMaterialFactory.Lit(definition.accentColor, 0.02f, 0.5f);
            Material dark = CookingMaterialFactory.Lit(new Color(0.12f, 0.1f, 0.08f, 1f), 0f, 0.2f);
            Material metal = CookingMaterialFactory.Lit(
                new Color(0.74f, 0.77f, 0.8f, 1f), 0.85f, 0.82f);

            switch (definition.shape)
            {
                case IngredientShape.Capsule:
                    Primitive(PrimitiveType.Capsule, root.transform,
                        new Vector3(0f, 0.052f * scale, 0f),
                        new Vector3(0.1f * scale, 0.09f * scale, 0.1f * scale),
                        main, Quaternion.Euler(0f, 0f, 78f));
                    Primitive(PrimitiveType.Sphere, root.transform,
                        new Vector3(0.085f * scale, 0.052f * scale, 0f),
                        new Vector3(0.055f, 0.045f, 0.055f) * scale,
                        accent);
                    break;

                case IngredientShape.Cube:
                    Primitive(PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 0.078f * scale, 0f),
                        new Vector3(0.155f, 0.155f, 0.155f) * scale,
                        main);
                    Primitive(PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 0.16f * scale, 0f),
                        new Vector3(0.13f, 0.006f, 0.13f) * scale,
                        accent);
                    break;

                case IngredientShape.Slice:
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.012f + i * 0.018f;
                        float rot = i * 24f;
                        Primitive(PrimitiveType.Cylinder, root.transform,
                            new Vector3(0.008f * (i - 1), y * scale, 0.006f * i),
                            new Vector3(0.078f, 0.009f, 0.078f) * scale,
                            main, Quaternion.Euler(0f, rot, 0f));
                        Primitive(PrimitiveType.Cylinder, root.transform,
                            new Vector3(0.008f * (i - 1), (y + 0.009f) * scale, 0.006f * i),
                            new Vector3(0.058f, 0.0085f, 0.058f) * scale,
                            accent, Quaternion.Euler(0f, rot, 0f));
                    }
                    break;

                case IngredientShape.Bowl:
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.034f * scale, 0f),
                        new Vector3(0.108f, 0.034f, 0.108f) * scale,
                        main);
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.071f * scale, 0f),
                        new Vector3(0.092f, 0.016f, 0.092f) * scale,
                        accent);
                    Primitive(PrimitiveType.Sphere, root.transform,
                        new Vector3(0f, 0.085f * scale, 0f),
                        new Vector3(0.145f, 0.055f, 0.145f) * scale,
                        accent);
                    break;

                case IngredientShape.Shaker:
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.062f * scale, 0f),
                        new Vector3(0.04f, 0.062f, 0.04f) * scale,
                        main);
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.132f * scale, 0f),
                        new Vector3(0.046f, 0.009f, 0.046f) * scale,
                        accent);
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i * 72f * Mathf.Deg2Rad;
                        Primitive(PrimitiveType.Sphere, root.transform,
                            new Vector3(Mathf.Cos(angle) * 0.022f, 0.142f * scale,
                                Mathf.Sin(angle) * 0.022f) * scale,
                            new Vector3(0.008f, 0.008f, 0.008f) * scale,
                            dark);
                    }
                    break;

                case IngredientShape.Bottle:
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.078f * scale, 0f),
                        new Vector3(0.04f, 0.078f, 0.04f) * scale,
                        main);
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.08f * scale, 0f),
                        new Vector3(0.043f, 0.036f, 0.043f) * scale,
                        accent);
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.17f * scale, 0f),
                        new Vector3(0.019f, 0.022f, 0.019f) * scale,
                        main);
                    Primitive(PrimitiveType.Sphere, root.transform,
                        new Vector3(0f, 0.198f * scale, 0f),
                        new Vector3(0.032f, 0.022f, 0.032f) * scale,
                        dark);
                    break;

                case IngredientShape.Bunch:
                    for (int i = 0; i < 3; i++)
                    {
                        Primitive(PrimitiveType.Capsule, root.transform,
                            new Vector3((i - 1) * 0.026f * scale, 0.1f * scale, 0f),
                            new Vector3(0.015f, 0.1f, 0.015f) * scale,
                            main,
                            Quaternion.Euler(0f, 0f, (i - 1) * 12f));
                    }
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.045f * scale, 0f),
                        new Vector3(0.042f, 0.016f, 0.042f) * scale,
                        accent);
                    break;

                case IngredientShape.Knife:
                    // A knife lying flat: dark handle at the back, long metal
                    // blade toward the front (+Z), so it reads as a tool even
                    // when the hand rotates it.
                    Primitive(PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 0.026f * scale, -0.09f * scale),
                        new Vector3(0.034f, 0.036f, 0.13f) * scale,
                        dark);
                    Primitive(PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 0.03f * scale, 0.065f * scale),
                        new Vector3(0.012f, 0.05f, 0.21f) * scale,
                        metal);
                    Primitive(PrimitiveType.Sphere, root.transform,
                        new Vector3(0f, 0.03f * scale, 0.17f * scale),
                        new Vector3(0.012f, 0.05f, 0.05f) * scale,
                        metal);
                    break;

                case IngredientShape.Dish:
                    Primitive(PrimitiveType.Cylinder, root.transform,
                        new Vector3(0f, 0.012f * scale, 0f),
                        new Vector3(0.21f, 0.012f, 0.21f) * scale,
                        accent);
                    Primitive(PrimitiveType.Sphere, root.transform,
                        new Vector3(0f, 0.055f * scale, 0f),
                        new Vector3(0.2f, 0.1f, 0.2f) * scale,
                        main);
                    Primitive(PrimitiveType.Sphere, root.transform,
                        new Vector3(0.032f * scale, 0.095f * scale, -0.018f * scale),
                        new Vector3(0.075f, 0.04f, 0.075f) * scale,
                        accent);
                    break;

                default:
                    Primitive(PrimitiveType.Sphere, root.transform,
                        new Vector3(0f, 0.088f * scale, 0f),
                        new Vector3(0.175f, 0.17f, 0.175f) * scale,
                        main);
                    Primitive(PrimitiveType.Capsule, root.transform,
                        new Vector3(0.015f, 0.175f * scale, 0f),
                        new Vector3(0.028f, 0.022f, 0.028f) * scale,
                        accent, Quaternion.Euler(0f, 0f, 28f));
                    break;
            }

            Primitive(PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.003f, 0f),
                new Vector3(0.1f, 0.0025f, 0.1f) * scale,
                dark);

            highlightRoot = CreateHighlight(root.transform, scale);
            highlightRoot.SetActive(false);
            return root;
        }

        /// <summary>
        /// Visual for an ingredient after it has been chopped on the cutting
        /// board: a small heap of differently coloured pieces. Used to replace
        /// the whole-ingredient prop in place.
        /// </summary>
        public static GameObject CreateChopped(IngredientDefinition definition,
            out GameObject highlightRoot)
        {
            if (definition == null)
                definition = IngredientCatalog.Get("unknown");

            float scale = Mathf.Max(0.4f, definition.scale);
            GameObject root = new GameObject("Visual_chopped_" + definition.id);
            Material main = CookingMaterialFactory.Lit(definition.color, 0.02f, 0.48f);
            Material accent = CookingMaterialFactory.Lit(definition.accentColor, 0.02f, 0.55f);

            for (int i = 0; i < 11; i++)
            {
                float angle = i * 2.39996f;
                float distance = 0.012f + (i % 4) * 0.018f;
                float size = (0.026f + (i % 3) * 0.007f) * scale;

                Primitive(PrimitiveType.Cube, root.transform,
                    new Vector3(Mathf.Cos(angle) * distance,
                        (0.014f + (i % 3) * 0.011f) * scale,
                        Mathf.Sin(angle) * distance),
                    new Vector3(size, size * 0.62f, size),
                    i % 3 == 0 ? accent : main,
                    Quaternion.Euler(0f, i * 37f, 0f));
            }

            highlightRoot = CreateHighlight(root.transform, scale);
            highlightRoot.SetActive(false);
            return root;
        }

        private static GameObject CreateHighlight(Transform parent, float scale)
        {
            GameObject root = new GameObject("Highlight");
            root.transform.SetParent(parent, false);
            Material glow = CookingMaterialFactory.Emissive(
                new Color(1f, 0.79f, 0.28f, 1f), 3.2f, 0.2f);

            int count = 12;
            float radius = 0.135f * scale;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Primitive(PrimitiveType.Sphere, root.transform,
                    new Vector3(Mathf.Cos(angle) * radius, 0.006f,
                        Mathf.Sin(angle) * radius),
                    new Vector3(0.014f, 0.009f, 0.014f),
                    glow);
            }

            return root;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material,
            Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation.HasValue ? rotation.Value : Quaternion.identity;
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
