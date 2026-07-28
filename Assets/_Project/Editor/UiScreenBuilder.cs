using System.Collections.Generic;
using QonaevLife.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Собирает экраны интерфейса: главное меню, слоты сохранений, настройки,
    /// телефон с картой (FR-001, FR-091 — FR-093, FR-095).
    /// Раскладка строится кодом, чтобы её можно было пересобрать одной
    /// командой и не держать бинарные префабы в репозитории.
    /// </summary>
    public static class UiScreenBuilder
    {
        private static readonly Color PanelColor = new(0.06f, 0.07f, 0.10f, 0.96f);
        private static readonly Color ButtonColor = new(0.15f, 0.17f, 0.24f, 1f);
        private static readonly Color AccentColor = new(0.55f, 0.85f, 1f);

        /// <summary>Собирает все экраны на переданном Canvas.</summary>
        public static BuiltScreens Build(GameObject canvas)
        {
            var result = new BuiltScreens
            {
                MainMenu = BuildMainMenu(canvas),
                SaveSlots = BuildSaveSlots(canvas),
                Settings = BuildSettings(canvas),
                Phone = BuildPhone(canvas),
                Lesson = BuildLesson(canvas)
            };

            return result;
        }

        /// <summary>Собранные представления — для связывания в Bootstrap.</summary>
        public sealed class BuiltScreens
        {
            public MainMenuView MainMenu;
            public SaveSlotsView SaveSlots;
            public SettingsView Settings;
            public PhoneView Phone;
            public LessonView Lesson;
        }

        // ------------------------------------------------------------------

        private static MainMenuView BuildMainMenu(GameObject canvas)
        {
            var root = CreateFullScreenPanel(canvas.transform, "Screen_MainMenu",
                new Color(0.04f, 0.05f, 0.08f, 1f));

            var title = CreateText(root.transform, "Title", "Qonaev Life", 64f,
                new Vector2(0.5f, 0.82f), new Vector2(900f, 90f), TextAlignmentOptions.Center);
            title.color = AccentColor;

            var hint = CreateText(root.transform, "Hint", string.Empty, 22f,
                new Vector2(0.5f, 0.74f), new Vector2(900f, 40f), TextAlignmentOptions.Center);
            hint.color = new Color(0.7f, 0.7f, 0.78f);

            // Кнопки в колонну по центру: порядок соответствует FR-001.
            var column = CreateColumn(root.transform, "Buttons",
                new Vector2(0.5f, 0.42f), new Vector2(420f, 400f), spacing: 12f);

            var newGame = CreateButton(column, "NewGameButton", "Новая игра");
            var @continue = CreateButton(column, "ContinueButton", "Продолжить");
            var load = CreateButton(column, "LoadButton", "Загрузить");
            var settings = CreateButton(column, "SettingsButton", "Настройки");
            var credits = CreateButton(column, "CreditsButton", "Титры");
            var quit = CreateButton(column, "QuitButton", "Выход");

            var version = CreateText(root.transform, "Version", string.Empty, 18f,
                new Vector2(0.5f, 0.06f), new Vector2(600f, 30f), TextAlignmentOptions.Center);
            version.color = new Color(0.5f, 0.5f, 0.56f);

            var view = canvas.AddComponent<MainMenuView>();
            view.ConfigureRoot(root);
            view.Configure(newGame, @continue, load, settings, credits, quit, title, version, hint);

            root.SetActive(false);
            return view;
        }

        private static SaveSlotsView BuildSaveSlots(GameObject canvas)
        {
            var root = CreateFullScreenPanel(canvas.transform, "Screen_SaveSlots", PanelColor);

            var title = CreateText(root.transform, "Title", "Сохранения", 44f,
                new Vector2(0.5f, 0.86f), new Vector2(900f, 70f), TextAlignmentOptions.Center);
            title.color = AccentColor;

            var column = CreateColumn(root.transform, "Slots",
                new Vector2(0.5f, 0.55f), new Vector2(1100f, 300f), spacing: 14f);

            var template = CreateButton(column, "SlotTemplate", "Слот", width: 1080f, height: 76f);
            template.gameObject.SetActive(false);

            var back = CreateButton(root.transform, "BackButton", "Назад",
                width: 260f, height: 56f);
            PlaceAt(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0.12f));

            var view = canvas.AddComponent<SaveSlotsView>();
            view.ConfigureRoot(root);
            view.Configure(column, template, title, back);

            root.SetActive(false);
            return view;
        }

        private static SettingsView BuildSettings(GameObject canvas)
        {
            var root = CreateFullScreenPanel(canvas.transform, "Screen_Settings", PanelColor);

            var title = CreateText(root.transform, "Title", "Настройки", 44f,
                new Vector2(0.5f, 0.93f), new Vector2(900f, 60f), TextAlignmentOptions.Center);
            title.color = AccentColor;

            // Две колонки: звук и графика слева, доступность справа.
            var left = CreateColumn(root.transform, "LeftColumn",
                new Vector2(0.28f, 0.52f), new Vector2(620f, 620f), spacing: 10f);

            var right = CreateColumn(root.transform, "RightColumn",
                new Vector2(0.72f, 0.52f), new Vector2(620f, 620f), spacing: 10f);

            CreateSectionLabel(left, "Звук");
            var master = CreateSlider(left, "MasterSlider", "Общая громкость");
            var music = CreateSlider(left, "MusicSlider", "Музыка");
            var sfx = CreateSlider(left, "SfxSlider", "Эффекты");
            var ambience = CreateSlider(left, "AmbienceSlider", "Атмосфера");

            CreateSectionLabel(left, "Графика");
            var quality = CreateSlider(left, "QualitySlider", "Качество (0–2)");
            var fullscreen = CreateToggle(left, "FullscreenToggle", "Полный экран");

            CreateSectionLabel(right, "Язык");
            var translationButton = CreateButton(right, "TranslationModeButton",
                "Режим перевода", width: 580f, height: 56f);
            var translationLabel = translationButton.GetComponentInChildren<TMP_Text>();

            CreateSectionLabel(right, "Доступность");
            var uiScale = CreateSlider(right, "UiScaleSlider", "Масштаб интерфейса");
            var subtitles = CreateToggle(right, "SubtitlesToggle", "Субтитры");
            var colorBlind = CreateToggle(right, "ColorBlindToggle",
                "Не полагаться на цвет");
            var reduceMotion = CreateToggle(right, "ReduceMotionToggle",
                "Меньше движения камеры");
            var forceTranslation = CreateToggle(right, "ForceTranslationToggle",
                "Всегда показывать перевод");

            var back = CreateButton(root.transform, "BackButton", "Назад",
                width: 260f, height: 56f);
            PlaceAt(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0.06f));

            var view = canvas.AddComponent<SettingsView>();
            view.ConfigureRoot(root);
            view.Configure(master, music, sfx, ambience, quality, uiScale, fullscreen,
                subtitles, colorBlind, reduceMotion, forceTranslation, translationButton,
                translationLabel, title, back);

            root.SetActive(false);
            return view;
        }

        private static PhoneView BuildPhone(GameObject canvas)
        {
            var root = CreateFullScreenPanel(canvas.transform, "Screen_Phone",
                new Color(0f, 0f, 0f, 0.6f));

            // Панель телефона в правой части экрана — как держат телефон в руке.
            var frame = CreatePanel(root.transform, "PhoneFrame", PanelColor,
                new Vector2(0.5f, 0.5f), new Vector2(1000f, 780f));

            var header = CreateText(frame.transform, "Header", string.Empty, 34f,
                new Vector2(0.5f, 0.94f), new Vector2(900f, 50f), TextAlignmentOptions.Center);
            header.color = AccentColor;

            var tabs = CreateRow(frame.transform, "Tabs",
                new Vector2(0.5f, 0.86f), new Vector2(960f, 48f), spacing: 6f);

            var tabTemplate = CreateButton(tabs, "TabTemplate", "Вкладка",
                width: 150f, height: 44f, fontSize: 18f);
            tabTemplate.gameObject.SetActive(false);

            var body = CreateText(frame.transform, "Body", string.Empty, 22f,
                new Vector2(0.5f, 0.44f), new Vector2(920f, 600f), TextAlignmentOptions.TopLeft);

            // Карта — отдельная панель на том же месте, что и текст.
            var mapPanel = CreatePanel(frame.transform, "MapPanel",
                new Color(0.10f, 0.12f, 0.15f, 1f),
                new Vector2(0.5f, 0.44f), new Vector2(880f, 580f));

            var mapArea = mapPanel.GetComponent<RectTransform>();

            var routeLine = CreateRouteLine(mapArea);
            var markerTemplate = CreateMapMarker(mapArea);

            var mapView = mapPanel.AddComponent<MapView>();
            mapView.Configure(mapArea, markerTemplate, routeLine);

            var back = CreateButton(frame.transform, "BackButton", "Закрыть",
                width: 240f, height: 52f);
            PlaceAt(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0.05f));

            var view = canvas.AddComponent<PhoneView>();
            view.ConfigureRoot(root);
            view.Configure(tabs, tabTemplate, header, body, back, mapPanel, mapView);

            root.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------

        /// <summary>Экран мини-урока (FR-043).</summary>
        private static LessonView BuildLesson(GameObject canvas)
        {
            var root = CreateFullScreenPanel(canvas.transform, "Screen_Lesson",
                new Color(0f, 0f, 0f, 0.72f));

            var frame = CreatePanel(root.transform, "LessonFrame", PanelColor,
                new Vector2(0.5f, 0.5f), new Vector2(1100f, 700f));

            var progress = CreateText(frame.transform, "Progress", string.Empty, 24f,
                new Vector2(1f, 1f), new Vector2(200f, 40f), TextAlignmentOptions.TopRight);
            progress.rectTransform.anchoredPosition = new Vector2(-28f, -24f);
            progress.color = new Color(0.65f, 0.7f, 0.8f);

            var kind = CreateText(frame.transform, "Kind", string.Empty, 26f,
                new Vector2(0.5f, 0.9f), new Vector2(900f, 44f), TextAlignmentOptions.Center);
            kind.color = AccentColor;

            var prompt = CreateText(frame.transform, "Prompt", string.Empty, 44f,
                new Vector2(0.5f, 0.72f), new Vector2(980f, 130f), TextAlignmentOptions.Center);

            var options = CreateColumn(frame.transform, "Options",
                new Vector2(0.5f, 0.38f), new Vector2(900f, 300f), spacing: 12f);

            var optionTemplate = CreateButton(options, "OptionTemplate", "Вариант",
                width: 860f, height: 58f);
            optionTemplate.gameObject.SetActive(false);

            var feedback = CreateText(frame.transform, "Feedback", string.Empty, 26f,
                new Vector2(0.5f, 0.14f), new Vector2(900f, 44f), TextAlignmentOptions.Center);

            var close = CreateButton(frame.transform, "CloseButton", "Закрыть",
                width: 240f, height: 52f);
            PlaceAt(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0.06f));

            var view = canvas.AddComponent<LessonView>();
            view.ConfigureRoot(root);
            view.Configure(progress, kind, prompt, feedback, options, optionTemplate, close);

            root.SetActive(false);
            return view;
        }

        private static GameObject CreateFullScreenPanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, worldPositionStays: false);

            var image = panel.AddComponent<Image>();
            image.color = color;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return panel;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color,
            Vector2 anchor, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, worldPositionStays: false);

            var image = panel.AddComponent<Image>();
            image.color = color;

            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            return panel;
        }

        private static Transform CreateColumn(Transform parent, string name, Vector2 anchor,
            Vector2 size, float spacing)
        {
            var column = new GameObject(name);
            column.transform.SetParent(parent, worldPositionStays: false);

            var rect = column.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return column.transform;
        }

        private static Transform CreateRow(Transform parent, string name, Vector2 anchor,
            Vector2 size, float spacing)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, worldPositionStays: false);

            var rect = row.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return row.transform;
        }

        private static TMP_Text CreateText(Transform parent, string name, string content,
            float fontSize, Vector2 anchor, Vector2 size, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, worldPositionStays: false);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.richText = true;

            var rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            return text;
        }

        private static void CreateSectionLabel(Transform parent, string caption)
        {
            var label = CreateText(parent, $"Section_{caption}", caption, 26f,
                new Vector2(0.5f, 0.5f), new Vector2(580f, 40f), TextAlignmentOptions.Left);
            label.color = AccentColor;
        }

        private static Button CreateButton(Transform parent, string name, string caption,
            float width = 400f, float height = 58f, float fontSize = 24f)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, worldPositionStays: false);

            var image = buttonObject.AddComponent<Image>();
            image.color = ButtonColor;
            image.rectTransform.sizeDelta = new Vector2(width, height);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            // Заметная разница для недоступного состояния: игрок должен видеть,
            // что кнопка есть, но сейчас не работает.
            var colors = button.colors;
            colors.disabledColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            colors.highlightedColor = new Color(0.22f, 0.26f, 0.36f, 1f);
            button.colors = colors;

            var label = CreateText(buttonObject.transform, "Label", caption, fontSize,
                new Vector2(0.5f, 0.5f), new Vector2(width - 24f, height - 8f),
                TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, string caption)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, worldPositionStays: false);

            var containerRect = container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(580f, 54f);

            var label = CreateText(container.transform, "Label", caption, 20f,
                new Vector2(0f, 0.5f), new Vector2(260f, 40f), TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(8f, 0f);

            var sliderObject = new GameObject("Slider");
            sliderObject.transform.SetParent(container.transform, worldPositionStays: false);

            var sliderRect = sliderObject.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(1f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(1f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(-8f, 0f);
            sliderRect.sizeDelta = new Vector2(280f, 26f);

            var background = sliderObject.AddComponent<Image>();
            background.color = new Color(0.1f, 0.11f, 0.14f, 1f);

            var fillArea = new GameObject("Fill");
            fillArea.transform.SetParent(sliderObject.transform, worldPositionStays: false);
            var fillImage = fillArea.AddComponent<Image>();
            fillImage.color = AccentColor;

            var fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var slider = sliderObject.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = background;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string name, string caption)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, worldPositionStays: false);

            var containerRect = container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(580f, 48f);

            var box = new GameObject("Box");
            box.transform.SetParent(container.transform, worldPositionStays: false);

            var boxImage = box.AddComponent<Image>();
            boxImage.color = new Color(0.12f, 0.13f, 0.17f, 1f);

            var boxRect = boxImage.rectTransform;
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = new Vector2(8f, 0f);
            boxRect.sizeDelta = new Vector2(32f, 32f);

            var check = new GameObject("Check");
            check.transform.SetParent(box.transform, worldPositionStays: false);
            var checkImage = check.AddComponent<Image>();
            checkImage.color = AccentColor;

            var checkRect = checkImage.rectTransform;
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(6f, 6f);
            checkRect.offsetMax = new Vector2(-6f, -6f);

            var label = CreateText(container.transform, "Label", caption, 20f,
                new Vector2(0f, 0.5f), new Vector2(500f, 40f), TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(52f, 0f);

            var toggle = container.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;

            return toggle;
        }

        private static RectTransform CreateMapMarker(RectTransform mapArea)
        {
            var marker = new GameObject("MarkerTemplate");
            marker.transform.SetParent(mapArea, worldPositionStays: false);

            var image = marker.AddComponent<Image>();
            image.color = Color.white;

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(12f, 12f);

            var label = CreateText(marker.transform, "Label", string.Empty, 16f,
                new Vector2(0.5f, 0.5f), new Vector2(200f, 26f), TextAlignmentOptions.Left);
            label.rectTransform.anchoredPosition = new Vector2(108f, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            marker.SetActive(false);
            return rect;
        }

        private static RectTransform CreateRouteLine(RectTransform mapArea)
        {
            var line = new GameObject("RouteLine");
            line.transform.SetParent(mapArea, worldPositionStays: false);

            var image = line.AddComponent<Image>();
            image.color = new Color(1f, 0.8f, 0.3f, 0.55f);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(10f, 3f);

            line.SetActive(false);
            return rect;
        }

        private static void PlaceAt(RectTransform rect, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
