using System.IO;
using QonaevLife.Bootstrap;
using QonaevLife.Content;
using QonaevLife.Player;
using QonaevLife.UI;
using QonaevLife.World;
using TMPro;
using UnityEngine.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Собирает играбельную тестовую сцену прототипа: серый блок-аут района,
    /// персонаж от третьего лица, камера, точки интереса и запущенную сессию.
    /// Сцену строит сам Unity, поэтому все ссылки и GUID валидны.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/Prototype_Qonaev.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Qonaev Life/Собрать тестовую сцену", priority = 11)]
        public static void BuildMenuCommand()
        {
            if (!EditorUtility.DisplayDialog(
                    "Собрать тестовую сцену",
                    "Будет создан контент прототипа и сцена Prototype_Qonaev.\n\n" +
                    "Существующая тестовая сцена будет перезаписана. Продолжить?",
                    "Собрать", "Отмена"))
            {
                return;
            }

            // Контент создаётся до сцены, но ссылки берём после NewScene:
            // создание новой сцены обнуляет ранее полученные ссылки на ассеты,
            // и они молча сериализуются как fileID: 0.
            PrototypeContentBuilder.Build();
            PrototypeContentBuilder.EnsureSessionConfig();

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var database = PrototypeContentBuilder.LoadDatabase();
            var config = PrototypeContentBuilder.LoadSessionConfig();

            BuildLighting();
            BuildGround();
            var player = BuildPlayer();
            BuildCamera(player);
            BuildLocationMarkers();
            BuildUserInterface();
            BuildBootstrap(config, database);

            if (!Directory.Exists(SceneFolder))
                Directory.CreateDirectory(SceneFolder);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Прототип] Сцена собрана: {ScenePath}. " +
                      "Нажмите Play, чтобы проверить перемещение и взаимодействие.");
        }

        // ------------------------------------------------------------------

        private static void BuildLighting()
        {
            var sun = new GameObject("Directional Light");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, 35f, 0f);
        }

        private static void BuildGround()
        {
            var root = new GameObject("World");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform);
            ground.transform.localScale = new Vector3(8f, 1f, 8f); // 80 × 80 м
            ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial(
                "Mat_Ground", new Color(0.32f, 0.33f, 0.35f));

            // Несколько кварталов серого блок-аута, чтобы камера и коллизии
            // проверялись в узких местах, а не на пустой плоскости.
            var blocks = new (Vector3 Position, Vector3 Size)[]
            {
                (new Vector3(-10f, 3f, 20f), new Vector3(14f, 6f, 10f)),
                (new Vector3(12f, 4f, 18f), new Vector3(10f, 8f, 12f)),
                (new Vector3(24f, 3f, -4f), new Vector3(8f, 6f, 18f)),
                (new Vector3(-22f, 4f, -6f), new Vector3(10f, 8f, 14f)),
                (new Vector3(2f, 2.5f, -26f), new Vector3(18f, 5f, 8f))
            };

            var wallMaterial = CreateMaterial("Mat_Building", new Color(0.55f, 0.54f, 0.5f));

            for (var i = 0; i < blocks.Length; i++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Building_{i:D2}";
                block.transform.SetParent(root.transform);
                block.transform.position = blocks[i].Position;
                block.transform.localScale = blocks[i].Size;
                block.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                block.isStatic = true;
            }
        }

        private static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1.1f, 0f);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;

            // Видимое тело: капсула без коллайдера, иначе он будет спорить
            // с CharacterController.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = CreateMaterial(
                "Mat_Player", new Color(0.2f, 0.55f, 0.85f));

            // Маркер направления взгляда — иначе на капсуле не видно поворот.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            nose.transform.SetParent(player.transform);
            nose.transform.localPosition = new Vector3(0f, 1.3f, 0.35f);
            nose.transform.localScale = new Vector3(0.16f, 0.16f, 0.3f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<Renderer>().sharedMaterial = CreateMaterial(
                "Mat_PlayerFacing", new Color(0.95f, 0.75f, 0.2f));

            var motor = player.AddComponent<PlayerMotor>();
            var detector = player.AddComponent<InteractionDetector>();
            var input = player.AddComponent<PlayerInputBridge>();
            var binder = player.AddComponent<PlayerSessionBinder>();

            AssignInputActions(input, detector);
            AssignPrivateField(binder, "interactionDetector", detector);
            AssignPrivateField(binder, "motor", motor);

            return player;
        }

        /// <summary>
        /// Назначает действия из готового ассета шаблона: Move, Sprint, Interact.
        /// </summary>
        private static void AssignInputActions(PlayerInputBridge input,
            InteractionDetector detector)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (asset == null)
            {
                Debug.LogWarning(
                    $"[Прототип] Не найден {InputActionsPath}. " +
                    "Назначьте действия ввода на объекте Player вручную.");
            }
            else
            {
                AssignActionReference(input, "moveAction", asset, "Player/Move");
                AssignActionReference(input, "sprintAction", asset, "Player/Sprint");
                AssignActionReference(input, "interactAction", asset, "Player/Interact");
            }

            AssignPrivateField(input, "interactionDetector", detector);
        }

        /// <summary>
        /// Находит подобъект InputActionReference внутри ассета действий и
        /// назначает его в поле. Создавать ссылку через
        /// InputActionReference.Create нельзя: получится объект вне ассета,
        /// и после перезапуска редактора ссылка в сцене окажется битой.
        /// </summary>
        private static void AssignActionReference(Object target, string field,
            InputActionAsset asset, string actionPath)
        {
            var action = asset.FindAction(actionPath);
            if (action == null)
            {
                Debug.LogWarning($"[Прототип] Действие '{actionPath}' не найдено.");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            var reference = FindPersistedReference(assetPath, action.id);

            if (reference == null)
            {
                Debug.LogWarning(
                    $"[Прототип] В ассете нет сохранённой ссылки на '{actionPath}'. " +
                    $"Назначьте поле '{field}' вручную на объекте Player.");
                return;
            }

            AssignPrivateField(target, field, reference);
        }

        private static InputActionReference FindPersistedReference(string assetPath,
            System.Guid actionId)
        {
            foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (candidate is InputActionReference reference
                    && reference.action != null
                    && reference.action.id == actionId)
                {
                    return reference;
                }
            }

            return null;
        }

        private static void BuildCamera(GameObject player)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;

            cameraObject.AddComponent<CinemachineBrain>();
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 6f, -7f), Quaternion.Euler(22f, 0f, 0f));

            // Цель слежения на уровне груди: камера смотрит не в ноги.
            var lookTarget = new GameObject("CameraTarget");
            lookTarget.transform.SetParent(player.transform);
            lookTarget.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            var virtualCamera = new GameObject("ThirdPersonCamera")
                .AddComponent<CinemachineCamera>();
            virtualCamera.Follow = lookTarget.transform;
            virtualCamera.LookAt = lookTarget.transform;

            var follow = virtualCamera.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 2.6f, -5.5f);

            virtualCamera.gameObject.AddComponent<CinemachineRotationComposer>();

            // Камера не проходит сквозь стены (FR-011).
            var deoccluder = virtualCamera.gameObject.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = ~0;
            deoccluder.MinimumDistanceFromTarget = 0.5f;
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.CameraRadius = 0.3f;
            deoccluder.AvoidObstacles.DistanceLimit = 8f;

            AssignPrivateField(
                player.GetComponent<PlayerInputBridge>(),
                "cameraTransform",
                cameraObject.transform);
        }

        private static void BuildLocationMarkers()
        {
            var root = new GameObject("Locations");
            var material = CreateMaterial("Mat_Interactable", new Color(0.25f, 0.75f, 0.4f));

            var kinds = new[]
            {
                (PrototypeContentBuilder.ApartmentLocationId, InteractionKind.Door,
                    "prompt.enter_home"),
                (PrototypeContentBuilder.CourierHubLocationId, InteractionKind.Terminal,
                    "prompt.take_shift"),
                (PrototypeContentBuilder.ShopLocationId, InteractionKind.Shop,
                    "prompt.open_shop"),
                (PrototypeContentBuilder.CafeLocationId, InteractionKind.Npc,
                    "prompt.deliver_order")
            };

            foreach (var row in PrototypeContentBuilder.LocationTable)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"Interactable_{row.Id}";
                marker.transform.SetParent(root.transform);
                marker.transform.position = row.Position + new Vector3(0f, 0.9f, 0f);
                marker.transform.localScale = new Vector3(1.4f, 1.8f, 1.4f);
                marker.GetComponent<Renderer>().sharedMaterial = material;

                var interactable = marker.AddComponent<LocationInteractable>();

                var kind = InteractionKind.Terminal;
                var prompt = "prompt.interact";
                foreach (var candidate in kinds)
                {
                    if (candidate.Item1 != row.Id)
                        continue;

                    kind = candidate.Item2;
                    prompt = candidate.Item3;
                    break;
                }

                interactable.Configure(row.Id, kind, prompt);
            }
        }

        /// <summary>
        /// Собирает HUD и подсказку взаимодействия (FR-090, FR-012).
        /// Масштабируется по разрешению, чтобы текст оставался читаемым (FR-095).
        /// </summary>
        private static void BuildUserInterface()
        {
            var canvasObject = new GameObject("HUD_Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var clockLabel = CreateLabel(canvasObject.transform, "ClockLabel",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f), offset: new Vector2(32f, -28f),
                size: new Vector2(560f, 44f), fontSize: 30f,
                alignment: TextAlignmentOptions.TopLeft);

            var moneyLabel = CreateLabel(canvasObject.transform, "MoneyLabel",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f), offset: new Vector2(32f, -76f),
                size: new Vector2(560f, 44f), fontSize: 30f,
                alignment: TextAlignmentOptions.TopLeft);

            var objectiveLabel = CreateLabel(canvasObject.transform, "ObjectiveLabel",
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(1f, 1f), offset: new Vector2(-32f, -28f),
                size: new Vector2(760f, 96f), fontSize: 26f,
                alignment: TextAlignmentOptions.TopRight);

            var notificationLabel = CreateLabel(canvasObject.transform, "NotificationLabel",
                anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f), offset: new Vector2(0f, -150f),
                size: new Vector2(1000f, 56f), fontSize: 32f,
                alignment: TextAlignmentOptions.Center);
            notificationLabel.color = new Color(1f, 0.9f, 0.45f);

            var hud = canvasObject.AddComponent<HudView>();
            hud.Configure(clockLabel, moneyLabel, objectiveLabel, notificationLabel);

            // Подсказка взаимодействия: отдельный корень, чтобы её можно было
            // скрывать целиком, не трогая остальной HUD.
            var promptRoot = new GameObject("InteractionPrompt");
            promptRoot.transform.SetParent(canvasObject.transform, worldPositionStays: false);

            var promptRect = promptRoot.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 130f);
            promptRect.sizeDelta = new Vector2(900f, 56f);

            var promptLabel = CreateLabel(promptRoot.transform, "PromptLabel",
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f), offset: Vector2.zero,
                size: Vector2.zero, fontSize: 30f,
                alignment: TextAlignmentOptions.Center,
                stretch: true);

            var prompt = canvasObject.AddComponent<InteractionPromptView>();
            prompt.Configure(promptRoot, promptLabel);

            promptRoot.SetActive(false);

            BuildDialogueWindow(canvasObject);
        }

        /// <summary>
        /// Окно диалога: реплика, перевод, варианты ответа и словарные слова
        /// (FR-033, FR-040 — FR-042).
        /// </summary>
        private static void BuildDialogueWindow(GameObject canvasObject)
        {
            var window = new GameObject("DialogueWindow");
            window.transform.SetParent(canvasObject.transform, worldPositionStays: false);

            var panel = window.AddComponent<Image>();
            panel.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);

            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(1400f, 460f);

            var speaker = CreateLabel(window.transform, "SpeakerLabel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -20f), new Vector2(520f, 44f), 30f,
                TextAlignmentOptions.TopLeft);
            speaker.color = new Color(0.55f, 0.85f, 1f);

            var mode = CreateLabel(window.transform, "ModeLabel",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -20f), new Vector2(560f, 40f), 22f,
                TextAlignmentOptions.TopRight);
            mode.color = new Color(0.75f, 0.75f, 0.8f);

            var primary = CreateLabel(window.transform, "PrimaryLabel",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -78f), new Vector2(-56f, 76f), 30f,
                TextAlignmentOptions.TopLeft);

            var translation = CreateLabel(window.transform, "TranslationLabel",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -158f), new Vector2(-56f, 60f), 24f,
                TextAlignmentOptions.TopLeft);
            translation.color = new Color(0.72f, 0.78f, 0.72f);

            // Контейнер словарных слов — горизонтальный ряд кнопок.
            var wordRow = CreateRow(window.transform, "WordRow",
                anchoredPosition: new Vector2(28f, -224f), height: 44f, horizontal: true);

            var wordTemplate = CreateButtonTemplate(wordRow, "WordButtonTemplate",
                new Vector2(220f, 40f), fontSize: 20f,
                background: new Color(0.16f, 0.3f, 0.22f, 0.95f));

            // Варианты ответа — вертикальный список.
            var choiceColumn = CreateRow(window.transform, "ChoiceColumn",
                anchoredPosition: new Vector2(28f, -284f), height: 160f, horizontal: false);

            var choiceTemplate = CreateButtonTemplate(choiceColumn, "ChoiceButtonTemplate",
                new Vector2(1340f, 40f), fontSize: 24f,
                background: new Color(0.14f, 0.16f, 0.24f, 0.95f));

            var hint = CreateLabel(window.transform, "HintLabel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(1200f, 34f), 20f,
                TextAlignmentOptions.Center);
            hint.color = new Color(0.6f, 0.6f, 0.66f);
            hint.text = "1–4 — выбрать ответ    T — режим перевода    Esc — закрыть";

            var view = canvasObject.AddComponent<DialogueView>();
            view.Configure(window, speaker, primary, translation,
                choiceColumn, choiceTemplate, wordRow, wordTemplate, mode);

            canvasObject.AddComponent<DialogueInputGate>();

            window.SetActive(false);
        }

        /// <summary>Контейнер с автоматической раскладкой для кнопок.</summary>
        private static Transform CreateRow(Transform parent, string name,
            Vector2 anchoredPosition, float height, bool horizontal)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, worldPositionStays: false);

            var rect = row.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(1340f, height);

            if (horizontal)
            {
                var layout = row.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.UpperLeft;
            }
            else
            {
                var layout = row.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 8f;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.UpperLeft;
            }

            return row.transform;
        }

        /// <summary>
        /// Шаблон кнопки. Остаётся выключенным на сцене: представление клонирует
        /// его по мере надобности, а сам шаблон никогда не показывается.
        /// </summary>
        private static Button CreateButtonTemplate(Transform parent, string name,
            Vector2 size, float fontSize, Color background)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, worldPositionStays: false);

            var image = buttonObject.AddComponent<Image>();
            image.color = background;

            var rect = image.rectTransform;
            rect.sizeDelta = size;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, worldPositionStays: false);

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Left;
            label.color = Color.white;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;

            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 2f);
            labelRect.offsetMax = new Vector2(-14f, -2f);

            buttonObject.SetActive(false);
            return button;
        }

        private static TMP_Text CreateLabel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset,
            Vector2 size, float fontSize, TextAlignmentOptions alignment,
            bool stretch = false)
        {
            // Подложка — родитель текста: так она гарантированно рисуется
            // раньше и не перекрывает буквы.
            var backdropObject = new GameObject($"{name}_Backdrop");
            backdropObject.transform.SetParent(parent, worldPositionStays: false);

            var backdrop = backdropObject.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.45f);
            backdrop.raycastTarget = false;

            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(backdropObject.transform, worldPositionStays: false);

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.text = string.Empty;
            label.enableWordWrapping = true;

            // Позиционируется подложка; текст растягивается внутри неё.
            var backdropRect = backdrop.rectTransform;
            backdropRect.anchorMin = anchorMin;
            backdropRect.anchorMax = anchorMax;
            backdropRect.pivot = pivot;

            if (stretch)
            {
                backdropRect.offsetMin = Vector2.zero;
                backdropRect.offsetMax = Vector2.zero;
            }
            else
            {
                backdropRect.anchoredPosition = offset;
                backdropRect.sizeDelta = size;
            }

            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);

            return label;
        }

        private static void BuildBootstrap(GameSessionConfig config, ContentDatabase database)
        {
            var bootstrap = new GameObject("GameBootstrap");
            var component = bootstrap.AddComponent<GameBootstrap>();

            AssignPrivateField(component, "config", config);
            AssignPrivateField(component, "content", database);
            AssignPrivateField(component, "startNewGameOnAwake", true);

            // Ассет должен быть уже записан на диск: ссылка на объект в памяти
            // без пути превратится в fileID: 0 при сохранении сцены.
            VerifyAssigned(component, "config", nameof(config));
            VerifyAssigned(component, "content", nameof(database));
        }

        /// <summary>
        /// Убеждается, что ссылка действительно записана. Молчаливое
        /// fileID: 0 иначе всплывёт только при Play как неработающая сессия.
        /// </summary>
        private static void VerifyAssigned(Object target, string field, string label)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);

            if (property != null && property.objectReferenceValue != null)
                return;

            Debug.LogError(
                $"[Прототип] Поле '{field}' ({label}) осталось пустым в " +
                $"{target.GetType().Name}. Назначьте ассет вручную в инспекторе.");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Материал URP. Создаётся один раз и переиспользуется, чтобы сцена
        /// не тянула десяток одинаковых ассетов.
        /// </summary>
        private static Material CreateMaterial(string assetName, Color color)
        {
            const string folder = "Assets/_Project/Art/Prototype";
            var path = $"{folder}/{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");

            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Prototype");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Прототип] Шейдер URP/Lit не найден, взят стандартный.");
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = assetName };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            // Возвращаем ссылку, перечитанную по пути: только она надёжно
            // сериализуется в сцену.
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>
        /// Пишет в приватное [SerializeField]-поле через SerializedObject:
        /// поля намеренно закрыты, а сцену собирает редактор.
        /// </summary>
        private static void AssignPrivateField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning(
                    $"[Прототип] Поле '{field}' не найдено в {target.GetType().Name}.");
                return;
            }

            switch (value)
            {
                case bool b:
                    property.boolValue = b;
                    break;
                case Object reference:
                    property.objectReferenceValue = reference;
                    break;
                default:
                    Debug.LogWarning(
                        $"[Прототип] Тип {value?.GetType().Name} не поддерживается " +
                        $"для поля '{field}'.");
                    return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
