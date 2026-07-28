using QonaevLife.Core;
using QonaevLife.Language;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.UI
{
    /// <summary>
    /// Применяет настройки к Unity: качество, разрешение, масштаб интерфейса
    /// (FR-093, FR-095). Отделён от <see cref="ISettingsService"/>: сервис
    /// хранит значения и не зависит от движка, применение живёт в сцене.
    /// </summary>
    public sealed class SettingsApplier : MonoBehaviour
    {
        [SerializeField] [Tooltip("Масштабируемые Canvas интерфейса.")]
        private CanvasScaler[] scalers = System.Array.Empty<CanvasScaler>();

        private IEventBus _eventBus;
        private ISettingsService _settings;
        private ILanguageProgressService _language;

        public void Bind(IEventBus eventBus, ISettingsService settings,
            ILanguageProgressService language)
        {
            Unbind();

            _eventBus = eventBus;
            _settings = settings;
            _language = language;

            _eventBus.Subscribe<SettingsChangedEvent>(OnSettingsChanged);

            Apply(_settings.Current);
        }

        public void Unbind()
        {
            _eventBus?.Unsubscribe<SettingsChangedEvent>(OnSettingsChanged);
            _eventBus = null;
        }

        private void OnDestroy() => Unbind();

        private void OnSettingsChanged(SettingsChangedEvent changed) => Apply(changed.Settings);

        private void Apply(GameSettings settings)
        {
            if (settings == null)
                return;

            ApplyQuality(settings);
            ApplyResolution(settings);
            ApplyUiScale(settings);
            ApplyLanguage(settings);
            ApplyAudio(settings);
        }

        /// <summary>
        /// Профиль качества управляет тенями, дальностью и постобработкой
        /// через уровни QualitySettings проекта (п. 8.4 ТЗ).
        /// </summary>
        private static void ApplyQuality(GameSettings settings)
        {
            var levels = QualitySettings.names.Length;
            if (levels == 0)
                return;

            // Профилей в ТЗ три, а уровней в проекте может быть больше или
            // меньше: раскладываем пропорционально, а не по фиксированному
            // индексу, иначе на другом наборе уровней выбор будет неверным.
            var normalized = (float)settings.qualityProfile / (int)QualityProfile.High;
            var index = Mathf.RoundToInt(normalized * (levels - 1));

            QualitySettings.SetQualityLevel(Mathf.Clamp(index, 0, levels - 1),
                applyExpensiveChanges: false);
        }

        private static void ApplyResolution(GameSettings settings)
        {
            // В редакторе разрешение окна менять не нужно: это сломает
            // раскладку панелей и не отражает поведение сборки.
            if (Application.isEditor)
                return;

            var mode = settings.fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            if (Screen.width == settings.screenWidth
                && Screen.height == settings.screenHeight
                && Screen.fullScreenMode == mode)
            {
                return;
            }

            Screen.SetResolution(settings.screenWidth, settings.screenHeight, mode);
        }

        /// <summary>Масштаб интерфейса для доступности (FR-095).</summary>
        private void ApplyUiScale(GameSettings settings)
        {
            foreach (var scaler in scalers)
            {
                if (scaler == null)
                    continue;

                scaler.scaleFactor = settings.uiScale;

                // Референсное разрешение делим на масштаб: элементы становятся
                // крупнее, а раскладка не расползается.
                scaler.referenceResolution = new Vector2(
                    1920f / settings.uiScale, 1080f / settings.uiScale);
            }
        }

        private void ApplyLanguage(GameSettings settings)
        {
            if (_language == null)
                return;

            _language.SetMode(settings.GetTranslationMode());
            _language.ForceFullTranslation = settings.forceFullTranslation;
        }

        /// <summary>
        /// Громкость по группам (FR-100). Пока модуль Audio пуст, значения
        /// применяются к общему уровню Unity — микшер подключается вместе
        /// с реализацией IAudioService.
        /// </summary>
        private static void ApplyAudio(GameSettings settings)
        {
            AudioListener.volume = settings.masterVolume;
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(CanvasScaler[] canvasScalers) => scalers = canvasScalers;
    }
}
