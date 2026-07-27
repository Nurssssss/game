using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Строит городской блок-аут: кварталы разновысотных домов с текстурными
    /// фасадами, дороги с разметкой, тротуары с бордюрами, фонари и озеленение.
    /// Геометрия остаётся примитивной — это блок-аут этапа P1, а не финальный
    /// арт (п. 13 ТЗ). Задача — дать читаемый масштаб и связную городскую среду.
    /// </summary>
    public static class CityBlockoutBuilder
    {
        private const string MaterialFolder = "Assets/_Project/Art/Prototype";

        /// <summary>Описание квартала: где стоит и какого размера.</summary>
        private readonly struct Block
        {
            public Block(Vector2 center, Vector2 size, int buildings, float minHeight,
                float maxHeight)
            {
                Center = center;
                Size = size;
                Buildings = buildings;
                MinHeight = minHeight;
                MaxHeight = maxHeight;
            }

            public Vector2 Center { get; }
            public Vector2 Size { get; }
            public int Buildings { get; }
            public float MinHeight { get; }
            public float MaxHeight { get; }
        }

        public static void Build(Transform root)
        {
            // Детерминированный генератор: одна и та же сцена при каждом запуске,
            // иначе QA не сможет воспроизвести найденный дефект.
            var random = new System.Random(20260727);

            var materials = CreateMaterials();

            BuildGround(root, materials);
            BuildRoads(root, materials);

            var blocks = new[]
            {
                new Block(new Vector2(-26f, 22f), new Vector2(26f, 18f), 4, 9f, 22f),
                new Block(new Vector2(22f, 26f), new Vector2(22f, 16f), 3, 12f, 26f),
                new Block(new Vector2(-30f, -18f), new Vector2(20f, 22f), 4, 8f, 18f),
                new Block(new Vector2(30f, -20f), new Vector2(22f, 20f), 3, 10f, 24f),
                new Block(new Vector2(-4f, -40f), new Vector2(30f, 14f), 4, 7f, 15f)
            };

            var blockRoot = new GameObject("Buildings");
            blockRoot.transform.SetParent(root, worldPositionStays: false);

            foreach (var block in blocks)
                BuildBlock(blockRoot.transform, block, materials, random);

            BuildStreetProps(root, materials, random);
        }

        // ------------------------------------------------------------------

        private sealed class MaterialSet
        {
            public Material Asphalt;
            public Material Sidewalk;
            public Material RoadMarking;
            public Material Concrete;
            public Material Brick;
            public Material Plaster;
            public Material Facade;
            public Material FacadeLit;
            public Material Curb;
            public Material Metal;
            public Material Foliage;
            public Material Trunk;
            public Material LampGlass;
        }

        private static MaterialSet CreateMaterials() => new()
        {
            Asphalt = CreateTextured("Mat_Asphalt", ProceduralTextureLibrary.GetAsphalt(),
                new Vector2(8f, 8f), smoothness: 0.18f),
            Sidewalk = CreateTextured("Mat_Sidewalk", ProceduralTextureLibrary.GetSidewalk(),
                new Vector2(4f, 4f), smoothness: 0.14f),
            RoadMarking = CreateTextured("Mat_RoadMarking",
                ProceduralTextureLibrary.GetRoadMarking(), Vector2.one, smoothness: 0.3f),
            Concrete = CreateTextured("Mat_Concrete", ProceduralTextureLibrary.GetConcrete(),
                new Vector2(2f, 2f), smoothness: 0.1f),
            Brick = CreateTextured("Mat_Brick", ProceduralTextureLibrary.GetBrick(),
                new Vector2(2f, 3f), smoothness: 0.08f),
            Plaster = CreateTextured("Mat_Plaster", ProceduralTextureLibrary.GetPlaster(),
                new Vector2(2f, 2f), smoothness: 0.12f),
            Facade = CreateTextured("Mat_Facade", ProceduralTextureLibrary.GetFacade(false),
                Vector2.one, smoothness: 0.35f),

            // Светящиеся окна: эмиссия делает ночной город живым без
            // отдельного источника света на каждое окно (п. 8.4 ТЗ).
            FacadeLit = CreateTextured("Mat_FacadeLit",
                ProceduralTextureLibrary.GetFacade(true), Vector2.one, smoothness: 0.35f,
                emissionTexture: ProceduralTextureLibrary.GetFacade(true),
                emissionColor: new Color(1f, 0.82f, 0.5f) * 0.9f),

            Curb = CreateFlat("Mat_Curb", new Color(0.68f, 0.67f, 0.64f), smoothness: 0.16f),
            Metal = CreateFlat("Mat_Metal", new Color(0.26f, 0.27f, 0.30f),
                smoothness: 0.55f, metallic: 0.7f),
            Foliage = CreateFlat("Mat_Foliage", new Color(0.22f, 0.42f, 0.20f), smoothness: 0.1f),
            Trunk = CreateFlat("Mat_Trunk", new Color(0.29f, 0.22f, 0.16f), smoothness: 0.08f),
            LampGlass = CreateFlat("Mat_LampGlass", new Color(1f, 0.92f, 0.72f),
                smoothness: 0.9f, emission: new Color(1f, 0.85f, 0.55f) * 2.2f)
        };

        private static void BuildGround(Transform root, MaterialSet materials)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Sidewalk";
            ground.transform.SetParent(root, worldPositionStays: false);
            ground.transform.localScale = new Vector3(12f, 1f, 12f); // 120 × 120 м
            ground.GetComponent<Renderer>().sharedMaterial = materials.Sidewalk;
            ground.isStatic = true;
        }

        /// <summary>Две пересекающиеся улицы с разметкой и бордюрами.</summary>
        private static void BuildRoads(Transform root, MaterialSet materials)
        {
            var roads = new GameObject("Roads");
            roads.transform.SetParent(root, worldPositionStays: false);

            const float roadWidth = 12f;
            const float length = 120f;

            CreateSlab(roads.transform, "Road_NorthSouth", materials.Asphalt,
                new Vector3(0f, 0.02f, 0f), new Vector3(roadWidth, 0.04f, length));

            CreateSlab(roads.transform, "Road_EastWest", materials.Asphalt,
                new Vector3(0f, 0.02f, 0f), new Vector3(length, 0.04f, roadWidth));

            // Прерывистая разметка: короткие полосы вдоль оси дороги.
            for (var z = -length / 2f + 4f; z < length / 2f; z += 8f)
            {
                // Внутри перекрёстка разметку не рисуем.
                if (Mathf.Abs(z) < roadWidth / 2f + 1f)
                    continue;

                CreateSlab(roads.transform, "Marking_NS", materials.RoadMarking,
                    new Vector3(0f, 0.05f, z), new Vector3(0.35f, 0.02f, 3.5f));
            }

            for (var x = -length / 2f + 4f; x < length / 2f; x += 8f)
            {
                if (Mathf.Abs(x) < roadWidth / 2f + 1f)
                    continue;

                CreateSlab(roads.transform, "Marking_EW", materials.RoadMarking,
                    new Vector3(x, 0.05f, 0f), new Vector3(3.5f, 0.02f, 0.35f));
            }

            // Бордюры вдоль дорог: дают вертикальный масштаб и отделяют тротуар.
            var curbOffset = roadWidth / 2f + 0.25f;
            CreateSlab(roads.transform, "Curb_West", materials.Curb,
                new Vector3(-curbOffset, 0.09f, 0f), new Vector3(0.5f, 0.18f, length));
            CreateSlab(roads.transform, "Curb_East", materials.Curb,
                new Vector3(curbOffset, 0.09f, 0f), new Vector3(0.5f, 0.18f, length));
            CreateSlab(roads.transform, "Curb_South", materials.Curb,
                new Vector3(0f, 0.09f, -curbOffset), new Vector3(length, 0.18f, 0.5f));
            CreateSlab(roads.transform, "Curb_North", materials.Curb,
                new Vector3(0f, 0.09f, curbOffset), new Vector3(length, 0.18f, 0.5f));
        }

        /// <summary>Квартал: ряд домов разной высоты с цоколем и парапетом.</summary>
        private static void BuildBlock(Transform parent, Block block, MaterialSet materials,
            System.Random random)
        {
            var blockRoot = new GameObject($"Block_{block.Center.x:0}_{block.Center.y:0}");
            blockRoot.transform.SetParent(parent, worldPositionStays: false);

            var facadeMaterials = new[] { materials.Facade, materials.FacadeLit };
            var wallMaterials = new[] { materials.Brick, materials.Plaster, materials.Concrete };

            var slotWidth = block.Size.x / block.Buildings;

            for (var i = 0; i < block.Buildings; i++)
            {
                var height = Mathf.Lerp(block.MinHeight, block.MaxHeight,
                    (float)random.NextDouble());

                var depth = block.Size.y * Mathf.Lerp(0.7f, 1f, (float)random.NextDouble());
                var width = slotWidth * Mathf.Lerp(0.78f, 0.95f, (float)random.NextDouble());

                var x = block.Center.x - block.Size.x / 2f + slotWidth * (i + 0.5f);
                var z = block.Center.y;

                var building = new GameObject($"Building_{i:D2}");
                building.transform.SetParent(blockRoot.transform, worldPositionStays: false);
                building.transform.position = new Vector3(x, 0f, z);

                // Цоколь другого материала: визуально «сажает» дом на землю.
                CreateSlab(building.transform, "Base", wallMaterials[random.Next(wallMaterials.Length)],
                    new Vector3(0f, 0.75f, 0f), new Vector3(width + 0.4f, 1.5f, depth + 0.4f));

                // Основной объём с фасадом. Тайлинг зависит от размера, иначе
                // окна растянулись бы на высоких домах.
                var body = CreateSlab(building.transform, "Body",
                    facadeMaterials[random.Next(facadeMaterials.Length)],
                    new Vector3(0f, 1.5f + height / 2f, 0f),
                    new Vector3(width, height, depth));

                ApplyFacadeTiling(body, width, height, depth);

                // Парапет по краю крыши.
                CreateSlab(building.transform, "Parapet", materials.Concrete,
                    new Vector3(0f, 1.5f + height + 0.3f, 0f),
                    new Vector3(width + 0.3f, 0.6f, depth + 0.3f));

                building.isStatic = true;
            }
        }

        /// <summary>
        /// Настраивает тайлинг фасада под размеры дома, чтобы окна оставались
        /// одинакового размера независимо от высоты и ширины.
        /// </summary>
        private static void ApplyFacadeTiling(GameObject building, float width, float height,
            float depth)
        {
            var renderer = building.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                return;

            // Копия материала на экземпляр: у домов разные размеры, а общий
            // материал один — тайлинг должен различаться.
            var instance = new Material(renderer.sharedMaterial);

            const float metresPerFloor = 3.2f;
            const float metresPerBay = 3.2f;

            instance.mainTextureScale = new Vector2(
                Mathf.Max(1f, Mathf.Round(width / metresPerBay)),
                Mathf.Max(1f, Mathf.Round(height / metresPerFloor)));

            renderer.sharedMaterial = instance;
        }

        /// <summary>Фонари, деревья и скамейки вдоль улиц.</summary>
        private static void BuildStreetProps(Transform root, MaterialSet materials,
            System.Random random)
        {
            var props = new GameObject("StreetProps");
            props.transform.SetParent(root, worldPositionStays: false);

            for (var i = 0; i < 12; i++)
            {
                var alongZ = i % 2 == 0;
                var offset = -44f + i * 8f;
                var side = random.Next(2) == 0 ? -8.5f : 8.5f;

                var position = alongZ
                    ? new Vector3(side, 0f, offset)
                    : new Vector3(offset, 0f, side);

                CreateStreetLamp(props.transform, position, materials);

                if (i % 3 == 0)
                {
                    var treeOffset = alongZ
                        ? new Vector3(side * 1.35f, 0f, offset + 3f)
                        : new Vector3(offset + 3f, 0f, side * 1.35f);

                    CreateTree(props.transform, treeOffset, materials, random);
                }
            }
        }

        private static void CreateStreetLamp(Transform parent, Vector3 position,
            MaterialSet materials)
        {
            var lamp = new GameObject("StreetLamp");
            lamp.transform.SetParent(parent, worldPositionStays: false);
            lamp.transform.position = position;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(lamp.transform, worldPositionStays: false);
            pole.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            pole.transform.localScale = new Vector3(0.12f, 2.2f, 0.12f);
            pole.GetComponent<Renderer>().sharedMaterial = materials.Metal;

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(lamp.transform, worldPositionStays: false);
            head.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            head.transform.localScale = new Vector3(0.5f, 0.18f, 0.5f);
            head.GetComponent<Renderer>().sharedMaterial = materials.LampGlass;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            // Точечный свет с малым радиусом: ночью улица не выглядит плоской,
            // но бюджет источников света остаётся под контролем (п. 8.4 ТЗ).
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(lamp.transform, worldPositionStays: false);
            lightObject.transform.localPosition = new Vector3(0f, 4.3f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 11f;
            light.intensity = 2.4f;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.shadows = LightShadows.None;

            lamp.isStatic = true;
        }

        private static void CreateTree(Transform parent, Vector3 position,
            MaterialSet materials, System.Random random)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, worldPositionStays: false);
            tree.transform.position = position;

            var scale = Mathf.Lerp(0.85f, 1.35f, (float)random.NextDouble());

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, worldPositionStays: false);
            trunk.transform.localPosition = new Vector3(0f, 1.4f * scale, 0f);
            trunk.transform.localScale = new Vector3(0.22f, 1.4f * scale, 0.22f);
            trunk.GetComponent<Renderer>().sharedMaterial = materials.Trunk;

            // Крона из двух сфер: силуэт живее одиночного шара.
            for (var i = 0; i < 2; i++)
            {
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = $"Crown_{i}";
                crown.transform.SetParent(tree.transform, worldPositionStays: false);
                crown.transform.localPosition = new Vector3(
                    (float)(random.NextDouble() - 0.5) * 0.6f,
                    (3f + i * 0.9f) * scale,
                    (float)(random.NextDouble() - 0.5) * 0.6f);

                var crownScale = (2.4f - i * 0.5f) * scale;
                crown.transform.localScale = Vector3.one * crownScale;
                crown.GetComponent<Renderer>().sharedMaterial = materials.Foliage;
                Object.DestroyImmediate(crown.GetComponent<Collider>());
            }

            tree.isStatic = true;
        }

        // ------------------------------------------------------------------

        private static GameObject CreateSlab(Transform parent, string name, Material material,
            Vector3 localPosition, Vector3 scale)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, worldPositionStays: false);
            slab.transform.localPosition = localPosition;
            slab.transform.localScale = scale;
            slab.GetComponent<Renderer>().sharedMaterial = material;
            slab.isStatic = true;

            return slab;
        }

        private static Material CreateTextured(string assetName, Texture2D texture,
            Vector2 tiling, float smoothness, Texture2D emissionTexture = null,
            Color emissionColor = default)
        {
            var path = $"{MaterialFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(FindLitShader()) { name = assetName };
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);

            if (emissionTexture != null)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", emissionTexture);
                material.SetColor("_EmissionColor", emissionColor);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Material CreateFlat(string assetName, Color color, float smoothness,
            float metallic = 0f, Color emission = default)
        {
            var path = $"{MaterialFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(FindLitShader()) { name = assetName };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);

            if (emission != default)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static Shader FindLitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
                return shader;

            Debug.LogWarning("[Город] Шейдер URP/Lit не найден, взят стандартный.");
            return Shader.Find("Standard");
        }
    }
}
