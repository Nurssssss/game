using QonaevLife.Language;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.UI
{
    /// <summary>
    /// Экран настроек (FR-093) с блоком доступности (FR-095). Изменения
    /// применяются сразу: игрок видит результат, не подтверждая отдельно.
    /// </summary>
    public sealed class SettingsView : ScreenView
    {
        [Header("Звук")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider ambienceSlider;

        [Header("Графика")]
        [SerializeField] private Slider qualitySlider;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Язык")]
        [SerializeField] private Button translationModeButton;
        [SerializeField] private TMP_Text translationModeLabel;

        [Header("Доступность")]
        [SerializeField] private Slider uiScaleSlider;
        [SerializeField] private Toggle subtitlesToggle;
        [SerializeField] private Toggle colorBlindToggle;
        [SerializeField] private Toggle reduceMotionToggle;
        [SerializeField] private Toggle forceTranslationToggle;

        [Header("Прочее")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button backButton;

        private ISettingsService _settings;

        /// <summary>
        /// Пока true, обработчики не применяют изменения: заполнение полей
        /// значениями из настроек само вызывает onValueChanged.
        /// </summary>
        private bool _suppressCallbacks;

        public override UiScreen Screen => UiScreen.Settings;

        public void BindSettings(ISettingsService settings)
        {
            _settings = settings;

            WireSlider(masterSlider, v => Mutate(s => s.masterVolume = v));
            WireSlider(musicSlider, v => Mutate(s => s.musicVolume = v));
            WireSlider(sfxSlider, v => Mutate(s => s.sfxVolume = v));
            WireSlider(ambienceSlider, v => Mutate(s => s.ambienceVolume = v));

            WireSlider(qualitySlider, v => Mutate(s => s.qualityProfile = Mathf.RoundToInt(v)));
            WireSlider(uiScaleSlider, v => Mutate(s => s.uiScale = v));

            WireToggle(fullscreenToggle, v => Mutate(s => s.fullscreen = v));
            WireToggle(subtitlesToggle, v => Mutate(s => s.subtitlesEnabled = v));
            WireToggle(colorBlindToggle, v => Mutate(s => s.colorBlindSafeMode = v));
            WireToggle(reduceMotionToggle, v => Mutate(s => s.reduceMotion = v));
            WireToggle(forceTranslationToggle, v => Mutate(s => s.forceFullTranslation = v));

            if (translationModeButton != null)
            {
                translationModeButton.onClick.RemoveAllListeners();
                translationModeButton.onClick.AddListener(CycleTranslationMode);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(CloseSelf);
            }

            if (uiScaleSlider != null)
            {
                uiScaleSlider.minValue = GameSettings.MinUiScale;
                uiScaleSlider.maxValue = GameSettings.MaxUiScale;
            }

            if (qualitySlider != null)
            {
                qualitySlider.minValue = (int)QualityProfile.Low;
                qualitySlider.maxValue = (int)QualityProfile.High;
                qualitySlider.wholeNumbers = true;
            }

            Refresh();
        }

        protected override void OnShown() => Refresh();

        /// <summary>Заполняет элементы текущими значениями настроек.</summary>
        public void Refresh()
        {
            if (_settings == null || Text == null)
                return;

            var current = _settings.Current;

            _suppressCallbacks = true;

            SetSlider(masterSlider, current.masterVolume);
            SetSlider(musicSlider, current.musicVolume);
            SetSlider(sfxSlider, current.sfxVolume);
            SetSlider(ambienceSlider, current.ambienceVolume);
            SetSlider(qualitySlider, current.qualityProfile);
            SetSlider(uiScaleSlider, current.uiScale);

            SetToggle(fullscreenToggle, current.fullscreen);
            SetToggle(subtitlesToggle, current.subtitlesEnabled);
            SetToggle(colorBlindToggle, current.colorBlindSafeMode);
            SetToggle(reduceMotionToggle, current.reduceMotion);
            SetToggle(forceTranslationToggle, current.forceFullTranslation);

            _suppressCallbacks = false;

            if (titleLabel != null)
                titleLabel.text = Text.Resolve("settings.title");

            if (translationModeLabel != null)
                translationModeLabel.text = Text.Resolve($"mode.{current.GetTranslationMode()}");

            if (backButton != null)
            {
                var label = backButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = Text.Resolve("common.back");
            }
        }

        private void CycleTranslationMode()
        {
            var next = _settings.Current.GetTranslationMode() switch
            {
                TranslationMode.RussianWithKazakh => TranslationMode.KazakhWithRussian,
                TranslationMode.KazakhWithRussian => TranslationMode.KazakhOnly,
                TranslationMode.KazakhOnly => TranslationMode.RussianWithKazakh,
                _ => TranslationMode.RussianWithKazakh
            };

            Mutate(s => s.translationMode = next.ToString());
        }

        /// <summary>
        /// Применяет изменение через сервис: он приводит значения к границам
        /// и рассылает событие, на которое реагирует SettingsApplier.
        /// </summary>
        private void Mutate(System.Action<GameSettings> change)
        {
            if (_suppressCallbacks || _settings == null)
                return;

            var copy = _settings.Current.Clone();
            change(copy);
            _settings.Apply(copy);

            Refresh();
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null)
                slider.value = value;
        }

        private static void SetToggle(Toggle toggle, bool value)
        {
            if (toggle != null)
                toggle.isOn = value;
        }

        private static void WireSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
        {
            if (slider == null)
                return;

            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(action);
        }

        private static void WireToggle(Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
        {
            if (toggle == null)
                return;

            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(action);
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(Slider master, Slider music, Slider sfx, Slider ambience,
            Slider quality, Slider uiScale, Toggle fullscreen, Toggle subtitles,
            Toggle colorBlind, Toggle reduceMotion, Toggle forceTranslation,
            Button translationMode, TMP_Text translationModeText, TMP_Text title, Button back)
        {
            masterSlider = master;
            musicSlider = music;
            sfxSlider = sfx;
            ambienceSlider = ambience;
            qualitySlider = quality;
            uiScaleSlider = uiScale;
            fullscreenToggle = fullscreen;
            subtitlesToggle = subtitles;
            colorBlindToggle = colorBlind;
            reduceMotionToggle = reduceMotion;
            forceTranslationToggle = forceTranslation;
            translationModeButton = translationMode;
            translationModeLabel = translationModeText;
            titleLabel = title;
            backButton = back;
        }
    }
}
