using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Расставляет город из моделей Kenney City Kit (CC0, см. ASSET_SOURCES.md).
    /// Модели набора имеют размер около 1 условной единицы, поэтому масштабируются
    /// до реальных габаритов застройки: этаж около 3 м, дом 9–25 м.
    /// Дома ставятся вплотную в ряд по периметру квартала, как городская
    /// застройка, а не отдельными объектами в поле.
    /// Расстановка детерминированная: одинаковый город при каждом запуске.
    /// </summary>
    public static class KenneyCityBuilder
    {
        private const string AssetRoot =
            "Assets/_Project/Art/External/Buildings/KenneyCityCommercial";

        private const string MaterialFolder = "Assets/_Project/Art/Prototype";

        /// <summary>
        /// Множитель масштаба моделей. Замерено: building-a имеет высоту
        /// 1.29 единицы, что ниже персонажа (1.8 м). При множителе 8 дом
        /// получается около 10 м — примерно три этажа.
        /// </summary>
        private const float ModelScale = 8f;

        /// <summary>
        /// Промежуток между соседними домами в метрах. Ширина участка берётся
        /// из габаритов конкретной модели: в наборе она различается втрое
        /// (0.84 … 2.32 единицы), поэтому фиксированный шаг не подходит.
        /// </summary>
        private const float GapBetweenBuildings = 1.2f;

        /// <summary>
        /// Ширина участка для отступа от перекрёстка. Соответствует самой
        /// широкой модели набора (building-n, 2.32 единицы).
        /// </summary>
        private const float LotWidth = 12.5f;

        /// <summary>Кэш ширины моделей: замер требует создания экземпляра.</summary>
        private static readonly Dictionary<string, float> WidthCache = new();

        /// <summary>Полуширина проезжей части.</summary>
        private const float RoadHalfWidth = 5f;

        /// <summary>Ширина тротуара между дорогой и домами.</summary>
        private const float SidewalkWidth = 3.5f;

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

        public static bool IsAvailable
            => AssetDatabase.IsValidFolder(AssetRoot) && LoadModel(BuildingNames[0]) != null;

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

            // Реестр входов заполняется заново: генератор может запускаться
            // повторно, и старые координаты стали бы неверными.
            EntranceLayout.Clear();

            BuildGroundAndRoads(root);
            BuildStreetFronts(root, random, material);
            BuildStreetProps(root, random);

            Debug.Log("[Город] Собран из моделей Kenney City Kit (CC0).");
        }

        // ------------------------------------------------------------------

        private static void BuildGroundAndRoads(Transform root)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, worldPositionStays: false);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            ground.GetComponent<Renderer>().sharedMaterial = CreateTextured(
                "Mat_GroundSidewalk", ProceduralTextureLibrary.GetSidewalk(),
                new Vector2(30f, 30f), 0.12f);
            ground.isStatic = true;

            // Plane даёт коллайдер нулевой толщины: на быстром движении или
            // при старте внутри геометрии персонаж способен его пробить.
            // Толстая невидимая плита под землёй ловит такие случаи.
            var safetyFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            safetyFloor.name = "SafetyFloor";
            safetyFloor.transform.SetParent(root, worldPositionStays: false);
            safetyFloor.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            safetyFloor.transform.localScale = new Vector3(200f, 3f, 200f);
            Object.DestroyImmediate(safetyFloor.GetComponent<MeshRenderer>());
            safetyFloor.isStatic = true;

            var roads = new GameObject("Roads");
            roads.transform.SetParent(root, worldPositionStays: false);

            var asphalt = CreateTextured("Mat_RoadAsphalt",
                ProceduralTextureLibrary.GetAsphalt(), new Vector2(4f, 24f), 0.2f);
            var marking = CreateTextured("Mat_RoadLine",
                ProceduralTextureLibrary.GetRoadMarking(), Vector2.one, 0.3f);

            const float length = 120f;
            var width = RoadHalfWidth * 2f;

            CreateSlab(roads.transform, "Road_NS", asphalt,
                new Vector3(0f, 0.02f, 0f), new Vector3(width, 0.04f, length));
            CreateSlab(roads.transform, "Road_EW", asphalt,
                new Vector3(0f, 0.02f, 0f), new Vector3(length, 0.04f, width));

            // Прерывистая осевая линия, кроме зоны перекрёстка.
            for (var t = -length / 2f + 4f; t < length / 2f; t += 7f)
            {
                if (Mathf.Abs(t) > RoadHalfWidth + 1.5f)
                {
                    CreateSlab(roads.transform, "Line_NS", marking,
                        new Vector3(0f, 0.05f, t), new Vector3(0.3f, 0.02f, 3f));
                    CreateSlab(roads.transform, "Line_EW", marking,
                        new Vector3(t, 0.05f, 0f), new Vector3(3f, 0.02f, 0.3f));
                }
            }

            // Бордюры отделяют тротуар от проезжей части.
            var curb = CreateFlat("Mat_Curb", new Color(0.66f, 0.65f, 0.62f), 0.16f);
            var edge = RoadHalfWidth + 0.2f;

            CreateSlab(roads.transform, "Curb_W", curb,
                new Vector3(-edge, 0.1f, 0f), new Vector3(0.4f, 0.2f, length));
            CreateSlab(roads.transform, "Curb_E", curb,
                new Vector3(edge, 0.1f, 0f), new Vector3(0.4f, 0.2f, length));
            CreateSlab(roads.transform, "Curb_S", curb,
                new Vector3(0f, 0.1f, -edge), new Vector3(length, 0.2f, 0.4f));
            CreateSlab(roads.transform, "Curb_N", curb,
                new Vector3(0f, 0.1f, edge), new Vector3(length, 0.2f, 0.4f));
        }

        /// <summary>
        /// Застройка по фронту улиц: дома стоят вплотную в ряд, фасадами
        /// к дороге. Так возникает ощущение улицы, а не поля с объектами.
        /// </summary>
        private static void BuildStreetFronts(Transform root, System.Random random,
            Material material)
        {
            var district = new GameObject("Buildings");
            district.transform.SetParent(root, worldPositionStays: false);

            // Занятые участки в плане. Общий список на весь район: угловые
            // дома соседних фронтов проверяются друг против друга.
            var occupied = new List<Bounds>();

            // Отступ фасада от центра дороги: проезжая часть плюс тротуар.
            var frontOffset = RoadHalfWidth + SidewalkWidth;

            // Четыре фронта вдоль обеих улиц, по обе стороны.
            // yaw задаёт направление фасада: дом смотрит на дорогу.
            // Фронты вдоль улицы «север-юг» отступают глубже, чем вдоль
            // «восток-запад»: так ряды не пересекаются на углах перекрёстка.
            var deepOffset = frontOffset + LotWidth;

            const float frontLength = 88f;
            const int maxPerFront = 14;

            var fronts = new (Vector3 Origin, Vector3 Step, float Yaw, int Count, float Tall,
                float Length)[]
            {
                // Улица «север-юг», западная сторона: фасады смотрят на восток.
                (new Vector3(-frontOffset, 0f, -44f), Vector3.forward, 90f, maxPerFront, 0.2f,
                    frontLength),
                // Восточная сторона: фасады на запад.
                (new Vector3(frontOffset, 0f, -44f), Vector3.forward, -90f, maxPerFront, 0.35f,
                    frontLength),
                // Улица «восток-запад», южная сторона: фасады на север.
                (new Vector3(-44f, 0f, -deepOffset), Vector3.right, 0f, maxPerFront, 0.15f,
                    frontLength),
                // Северная сторона: фасады на юг.
                (new Vector3(-44f, 0f, deepOffset), Vector3.right, 180f, maxPerFront, 0.3f,
                    frontLength)
            };

            foreach (var front in fronts)
            {
                var frontRoot = new GameObject($"Front_{front.Yaw:0}");
                frontRoot.transform.SetParent(district.transform, worldPositionStays: false);

                // Дома ставятся вплотную по фактической ширине каждой модели,
                // а не по фиксированному шагу: ширина в наборе различается
                // втрое (0.84 … 2.32 единицы), и единый шаг либо оставлял бы
                // дыры, либо приводил к наложению.
                var direction = front.Step.normalized;
                var travelled = 0f;

                for (var i = 0; i < front.Count; i++)
                {
                    var isTall = random.NextDouble() < front.Tall;
                    var pool = isTall ? SkyscraperNames : BuildingNames;
                    var modelName = pool[random.Next(pool.Length)];

                    var width = GetModelWidth(modelName) * ModelScale;

                    // Центр участка: половина ширины от текущей границы.
                    var position = front.Origin + direction * (travelled + width / 2f);
                    travelled += width + GapBetweenBuildings;

                    if (travelled > front.Length)
                        break;

                    // Участок у перекрёстка оставляем под проезд.
                    var clearance = frontOffset + 1f;
                    if (Mathf.Abs(position.x) < clearance && Mathf.Abs(position.z) < clearance)
                        continue;

                    // Проверяем фактическое пересечение с уже поставленными
                    // домами: отступ «на глаз» либо оставляет наложения,
                    // либо выедает половину улицы.
                    var footprint = new Bounds(
                        new Vector3(position.x, 0f, position.z),
                        new Vector3(width, 1f, width));

                    if (Overlaps(occupied, footprint))
                        continue;

                    occupied.Add(footprint);

                    var heightVariation = Mathf.Lerp(0.85f, 1.3f, (float)random.NextDouble());

                    var building = PlaceBuilding(frontRoot.transform, modelName, position,
                        front.Yaw, heightVariation, material);

                    // Фасад смотрит на дорогу: вход выносим перед ним по
                    // фактическим габаритам поставленного дома.
                    EntranceLayout.Register(building, front.Yaw, modelName);
                }
            }
        }

        private static GameObject PlaceBuilding(Transform parent, string modelName,
            Vector3 position, float yaw, float heightVariation, Material material)
        {
            var detailed = LoadModel(modelName);
            if (detailed == null)
                return null;

            var container = new GameObject($"Building_{modelName}");
            container.transform.SetParent(parent, worldPositionStays: false);
            container.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            // Масштаб приводит модель к реальным габаритам застройки.
            container.transform.localScale = new Vector3(
                ModelScale, ModelScale * heightVariation, ModelScale);

            var near = InstantiateModel(detailed, container.transform, material);
            near.name = "LOD0";

            var lods = new List<LOD>();
            var simplified = LoadModel($"low-detail-{modelName}");

            if (simplified != null)
            {
                var far = InstantiateModel(simplified, container.transform, material);
                far.name = "LOD1";

                lods.Add(new LOD(0.35f, near.GetComponentsInChildren<Renderer>()));
                lods.Add(new LOD(0.06f, far.GetComponentsInChildren<Renderer>()));
            }
            else
            {
                lods.Add(new LOD(0.06f, near.GetComponentsInChildren<Renderer>()));
            }

            var group = container.AddComponent<LODGroup>();
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();

            AddCollider(container, near);
            container.isStatic = true;

            return container;
        }

        /// <summary>
        /// Коллайдер по габаритам меша в локальных координатах контейнера,
        /// чтобы масштаб контейнера применился к нему автоматически.
        /// </summary>
        private static void AddCollider(GameObject container, GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                return;

            var localBounds = new Bounds();
            var initialized = false;

            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var meshBounds = filter.sharedMesh.bounds;

                if (!initialized)
                {
                    localBounds = meshBounds;
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(meshBounds);
                }
            }

            if (!initialized)
                return;

            var collider = container.AddComponent<BoxCollider>();
            collider.center = localBounds.center;
            collider.size = localBounds.size;
        }

        /// <summary>Фонари и деревья вдоль тротуаров.</summary>
        private static void BuildStreetProps(Transform root, System.Random random)
        {
            var props = new GameObject("StreetProps");
            props.transform.SetParent(root, worldPositionStays: false);

            var metal = CreateFlat("Mat_LampPole", new Color(0.24f, 0.25f, 0.28f), 0.55f, 0.7f);
            var glass = CreateFlat("Mat_LampGlass", new Color(1f, 0.92f, 0.72f), 0.9f,
                emission: new Color(1f, 0.85f, 0.55f) * 2.2f);
            var foliage = CreateFlat("Mat_Foliage", new Color(0.21f, 0.4f, 0.19f), 0.1f);
            var trunk = CreateFlat("Mat_Trunk", new Color(0.28f, 0.21f, 0.15f), 0.08f);

            var lampOffset = RoadHalfWidth + 1.2f;

            for (var t = -38f; t <= 38f; t += 13f)
            {
                if (Mathf.Abs(t) < RoadHalfWidth + 4f)
                    continue;

                CreateLamp(props.transform, new Vector3(-lampOffset, 0f, t), metal, glass);
                CreateLamp(props.transform, new Vector3(lampOffset, 0f, t), metal, glass);
                CreateLamp(props.transform, new Vector3(t, 0f, -lampOffset), metal, glass);
                CreateLamp(props.transform, new Vector3(t, 0f, lampOffset), metal, glass);
            }

            for (var t = -34f; t <= 34f; t += 17f)
            {
                if (Mathf.Abs(t) < RoadHalfWidth + 5f)
                    continue;

                CreateTree(props.transform, new Vector3(-lampOffset - 1.2f, 0f, t + 4f),
                    trunk, foliage, random);
                CreateTree(props.transform, new Vector3(t + 4f, 0f, lampOffset + 1.2f),
                    trunk, foliage, random);
            }
        }

        private static void CreateLamp(Transform parent, Vector3 position, Material metal,
            Material glass)
        {
            var lamp = new GameObject("StreetLamp");
            lamp.transform.SetParent(parent, worldPositionStays: false);
            lamp.transform.position = position;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(lamp.transform, worldPositionStays: false);
            pole.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            pole.transform.localScale = new Vector3(0.11f, 2.3f, 0.11f);
            pole.GetComponent<Renderer>().sharedMaterial = metal;

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(lamp.transform, worldPositionStays: false);
            head.transform.localPosition = new Vector3(0f, 4.7f, 0f);
            head.transform.localScale = new Vector3(0.45f, 0.16f, 0.45f);
            head.GetComponent<Renderer>().sharedMaterial = glass;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(lamp.transform, worldPositionStays: false);
            lightObject.transform.localPosition = new Vector3(0f, 4.5f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.shadows = LightShadows.None;

            lamp.isStatic = true;
        }

        private static void CreateTree(Transform parent, Vector3 position, Material trunkMaterial,
            Material foliageMaterial, System.Random random)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, worldPositionStays: false);
            tree.transform.position = position;

            var scale = Mathf.Lerp(0.9f, 1.3f, (float)random.NextDouble());

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, worldPositionStays: false);
            trunk.transform.localPosition = new Vector3(0f, 1.5f * scale, 0f);
            trunk.transform.localScale = new Vector3(0.2f, 1.5f * scale, 0.2f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMaterial;

            for (var i = 0; i < 2; i++)
            {
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = $"Crown_{i}";
                crown.transform.SetParent(tree.transform, worldPositionStays: false);
                crown.transform.localPosition = new Vector3(
                    (float)(random.NextDouble() - 0.5) * 0.5f,
                    (3.2f + i * 0.9f) * scale,
                    (float)(random.NextDouble() - 0.5) * 0.5f);
                crown.transform.localScale = Vector3.one * (2.5f - i * 0.6f) * scale;
                crown.GetComponent<Renderer>().sharedMaterial = foliageMaterial;
                Object.DestroyImmediate(crown.GetComponent<Collider>());
            }

            tree.isStatic = true;
        }

        // ------------------------------------------------------------------

        private static GameObject InstantiateModel(GameObject prefab, Transform parent,
            Material material)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (material != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = material;
            }

            foreach (var child in instance.GetComponentsInChildren<Transform>())
                child.gameObject.isStatic = true;

            return instance;
        }

        /// <summary>
        /// Пересекается ли участок с уже занятыми. Сравнение только в плане:
        /// высота домов различается, но занимают они землю одинаково.
        /// </summary>
        private static bool Overlaps(List<Bounds> occupied, Bounds candidate)
        {
            foreach (var existing in occupied)
            {
                if (existing.Intersects(candidate))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Козырёк над входом из набора Kenney. Возвращает null, если модели
        /// нет — тогда генератор ставит вывеску-заглушку.
        /// </summary>
        public static GameObject TryCreateAwning(Transform parent, Color accent)
        {
            var prefab = LoadModel("detail-awning");
            if (prefab == null)
                return null;

            var awning = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            awning.name = "Awning";
            awning.transform.localPosition = new Vector3(0f, 0f, 0f);
            awning.transform.localRotation = Quaternion.identity;
            awning.transform.localScale = Vector3.one * ModelScale;

            // Цвет козырька различает заведения: игрок узнаёт магазин и кафе
            // по виду, а не только по подсказке.
            var material = CreateFlat($"Mat_Awning_{ColorKey(accent)}", accent, 0.2f);

            foreach (var renderer in awning.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            foreach (var child in awning.GetComponentsInChildren<Transform>())
                child.gameObject.isStatic = true;

            return awning;
        }

        private static string ColorKey(Color color)
            => $"{Mathf.RoundToInt(color.r * 255)}_{Mathf.RoundToInt(color.g * 255)}_" +
               $"{Mathf.RoundToInt(color.b * 255)}";

        private static GameObject LoadModel(string modelName)
            => AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetRoot}/{modelName}.fbx");

        /// <summary>
        /// Ширина модели в единицах ассета — максимум из габаритов по X и Z,
        /// потому что дом может быть повёрнут фасадом в любую сторону.
        /// Результат кэшируется: замер требует создания и удаления экземпляра.
        /// </summary>
        private static float GetModelWidth(string modelName)
        {
            if (WidthCache.TryGetValue(modelName, out var cached))
                return cached;

            var prefab = LoadModel(modelName);
            if (prefab == null)
                return 1f;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();

            var width = 1f;
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                width = Mathf.Max(bounds.size.x, bounds.size.z);
            }

            Object.DestroyImmediate(instance);

            WidthCache[modelName] = width;
            return width;
        }

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
            material.SetFloat("_Smoothness", 0.15f);
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

        private static Material CreateTextured(string assetName, Texture2D texture,
            Vector2 tiling, float smoothness)
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
