using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Расставляет город из моделей Kenney City Kit (CC0, см. ASSET_SOURCES.md).
    /// Использует готовые упрощённые версии моделей как дальний уровень
    /// детализации, что прямо требуется п. 8.4 ТЗ для крупных объектов.
    /// Расстановка детерминированная: одинаковый город при каждом запуске,
    /// иначе QA не воспроизведёт найденный дефект.
    /// </summary>
    public static class KenneyCityBuilder
    {
        private const string AssetRoot =
            "Assets/_Project/Art/External/Buildings/KenneyCityCommercial";

        private const string MaterialFolder = "Assets/_Project/Art/Prototype";

        /// <summary>Модели зданий обычной детализации.</summary>
        private static readonly string[] BuildingNames =
        {
            "building-a", "building-b", "building-c", "building-d", "building-e",
            "building-f", "building-g", "building-h", "building-i", "building-j",
            "building-k", "building-l", "building-m", "building-n"
        };

        private static readonly string[] SkyscraperNames =
        {
            "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c",
            "building-skyscraper-d", "building-skyscraper-e"
        };

        /// <summary>
        /// Доступны ли модели в проекте. Если нет — генератор сцены
        /// использует процедурный блок-аут.
        /// </summary>
        public static bool IsAvailable
            => AssetDatabase.IsValidFolder(AssetRoot)
               && LoadModel(BuildingNames[0]) != null;

        public static void Build(Transform root)
        {
            if (!IsAvailable)
            {
                Debug.LogWarning(
                    "[Город] Модели Kenney не найдены — использую процедурный блок-аут.");
                CityBlockoutBuilder.Build(root);
                return;
            }

            var random = new System.Random(20260728);
            var material = LoadKenneyMaterial();

            BuildGroundAndRoads(root, material);
            BuildDistrict(root, random, material);

            Debug.Log("[Город] Собран из моделей Kenney City Kit (CC0).");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Земля и дороги остаются процедурными: в наборе Commercial дорожных
        /// модулей нет, они в отдельном City Kit (Roads).
        /// </summary>
        private static void BuildGroundAndRoads(Transform root, Material kenneyMaterial)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, worldPositionStays: false);
            ground.transform.localScale = new Vector3(16f, 1f, 16f); // 160 × 160 м
            ground.GetComponent<Renderer>().sharedMaterial = CreateFlat(
                "Mat_GroundKenney", new Color(0.42f, 0.43f, 0.44f), 0.12f);
            ground.isStatic = true;

            var roads = new GameObject("Roads");
            roads.transform.SetParent(root, worldPositionStays: false);

            var asphalt = CreateFlat("Mat_AsphaltFlat", new Color(0.16f, 0.16f, 0.18f), 0.2f);
            var marking = CreateFlat("Mat_MarkingFlat", new Color(0.85f, 0.84f, 0.75f), 0.3f);

            const float roadWidth = 14f;
            const float length = 160f;

            CreateSlab(roads.transform, "Road_NS", asphalt,
                new Vector3(0f, 0.02f, 0f), new Vector3(roadWidth, 0.04f, length));
            CreateSlab(roads.transform, "Road_EW", asphalt,
                new Vector3(0f, 0.02f, 0f), new Vector3(length, 0.04f, roadWidth));

            for (var z = -length / 2f + 5f; z < length / 2f; z += 9f)
            {
                if (Mathf.Abs(z) < roadWidth / 2f + 2f)
                    continue;

                CreateSlab(roads.transform, "Marking", marking,
                    new Vector3(0f, 0.05f, z), new Vector3(0.4f, 0.02f, 4f));
            }

            for (var x = -length / 2f + 5f; x < length / 2f; x += 9f)
            {
                if (Mathf.Abs(x) < roadWidth / 2f + 2f)
                    continue;

                CreateSlab(roads.transform, "Marking", marking,
                    new Vector3(x, 0.05f, 0f), new Vector3(4f, 0.02f, 0.4f));
            }
        }

        /// <summary>
        /// Кварталы вокруг перекрёстка. Здания ставятся рядами лицом к улице,
        /// небоскрёбы — в центре, малоэтажки — по краям района.
        /// </summary>
        private static void BuildDistrict(Transform root, System.Random random,
            Material material)
        {
            var district = new GameObject("Buildings");
            district.transform.SetParent(root, worldPositionStays: false);

            // Кварталы: центр, размер, поворот фасада к дороге, доля небоскрёбов.
            var blocks = new[]
            {
                (Center: new Vector2(-30f, 30f), Rows: 3, Cols: 3, Yaw: 135f, Tall: 0.15f),
                (Center: new Vector2(30f, 30f), Rows: 3, Cols: 3, Yaw: -135f, Tall: 0.45f),
                (Center: new Vector2(-30f, -30f), Rows: 3, Cols: 3, Yaw: 45f, Tall: 0.1f),
                (Center: new Vector2(30f, -30f), Rows: 3, Cols: 3, Yaw: -45f, Tall: 0.3f),
                (Center: new Vector2(-58f, 0f), Rows: 4, Cols: 2, Yaw: 90f, Tall: 0f),
                (Center: new Vector2(58f, 0f), Rows: 4, Cols: 2, Yaw: -90f, Tall: 0f)
            };

            const float spacing = 13f;

            foreach (var block in blocks)
            {
                var blockRoot = new GameObject(
                    $"Block_{block.Center.x:0}_{block.Center.y:0}");
                blockRoot.transform.SetParent(district.transform, worldPositionStays: false);

                for (var row = 0; row < block.Rows; row++)
                {
                    for (var col = 0; col < block.Cols; col++)
                    {
                        var offsetX = (col - (block.Cols - 1) / 2f) * spacing;
                        var offsetZ = (row - (block.Rows - 1) / 2f) * spacing;

                        var position = new Vector3(
                            block.Center.x + offsetX, 0f, block.Center.y + offsetZ);

                        var isTall = random.NextDouble() < block.Tall;
                        var pool = isTall ? SkyscraperNames : BuildingNames;
                        var modelName = pool[random.Next(pool.Length)];

                        // Небольшой разброс поворота: ряд не выглядит штампованным.
                        var yaw = block.Yaw + (float)(random.NextDouble() - 0.5) * 8f;

                        PlaceBuilding(blockRoot.transform, modelName, position, yaw, material);
                    }
                }
            }
        }

        /// <summary>
        /// Ставит здание с LOD-группой: вблизи детальная модель, вдали —
        /// упрощённая, дальше — не отрисовывается (п. 8.4 ТЗ).
        /// </summary>
        private static void PlaceBuilding(Transform parent, string modelName,
            Vector3 position, float yaw, Material material)
        {
            var detailed = LoadModel(modelName);
            if (detailed == null)
                return;

            var container = new GameObject($"Building_{modelName}");
            container.transform.SetParent(parent, worldPositionStays: false);
            container.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var near = InstantiateModel(detailed, container.transform, material);
            near.name = "LOD0";

            var lods = new List<LOD>();

            // В наборе есть готовая упрощённая версия каждой модели —
            // используем её вместо генерации LOD, качество силуэта выше.
            var simplified = LoadModel($"low-detail-{modelName}");

            if (simplified != null)
            {
                var far = InstantiateModel(simplified, container.transform, material);
                far.name = "LOD1";
                far.SetActive(true);

                lods.Add(new LOD(0.35f, near.GetComponentsInChildren<Renderer>()));
                lods.Add(new LOD(0.08f, far.GetComponentsInChildren<Renderer>()));
            }
            else
            {
                lods.Add(new LOD(0.08f, near.GetComponentsInChildren<Renderer>()));
            }

            var group = container.AddComponent<LODGroup>();
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();

            // Коллайдер по габаритам меша: игрок не проходит сквозь дом.
            var bounds = CalculateLocalBounds(near);
            var collider = container.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;

            container.isStatic = true;
        }

        private static GameObject InstantiateModel(GameObject prefab, Transform parent,
            Material material)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Единый материал на весь набор: модели Kenney используют общую
            // текстуру-палитру, поэтому лишние материалы только дробят батчинг.
            if (material != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = material;
            }

            foreach (var child in instance.GetComponentsInChildren<Transform>())
                child.gameObject.isStatic = true;

            return instance;
        }

        private static Bounds CalculateLocalBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(new Vector3(0f, 5f, 0f), new Vector3(8f, 10f, 8f));

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Из мировых координат в локальные координаты контейнера.
            bounds.center -= instance.transform.parent.position;
            return bounds;
        }

        // ------------------------------------------------------------------

        private static GameObject LoadModel(string modelName)
            => AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetRoot}/{modelName}.fbx");

        /// <summary>
        /// Материал с текстурой-палитрой Kenney. Модели раскрашены развёрткой
        /// по цветовому атласу, поэтому одного материала хватает на весь набор.
        /// </summary>
        private static Material LoadKenneyMaterial()
        {
            var path = $"{MaterialFolder}/Mat_KenneyCity.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{AssetRoot}/Textures/variation-a.png");

            if (texture == null)
            {
                Debug.LogWarning("[Город] Текстура-палитра Kenney не найдена.");
                return null;
            }

            var material = new Material(FindLitShader()) { name = "Mat_KenneyCity" };
            material.mainTexture = texture;
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

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

        private static Material CreateFlat(string assetName, Color color, float smoothness)
        {
            var path = $"{MaterialFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(FindLitShader()) { name = assetName };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);

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
