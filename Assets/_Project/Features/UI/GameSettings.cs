using System;
using QonaevLife.Core;
using QonaevLife.Language;

namespace QonaevLife.UI
{
    /// <summary>Профиль качества графики (п. 8.4 ТЗ).</summary>
    public enum QualityProfile
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Пользовательские настройки (FR-093, FR-095). Значения храняся отдельно
    /// от сохранений: они привязаны к устройству, а не к игровому прогрессу,
    /// поэтому переносятся между слотами.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const float MinUiScale = 0.8f;
        public const float MaxUiScale = 1.6f;

        // Язык и перевод
        public string interfaceLanguage = "ru";
        public string translationMode = nameof(TranslationMode.RussianWithKazakh);

        /// <summary>
        /// Принудительный полный перевод из настроек доступности: адаптивные
        /// подсказки перестают сокращать перевод (FR-044).
        /// </summary>
        public bool forceFullTranslation;

        // Звук: громкость по группам от 0 до 1 (FR-100)
        public float masterVolume = 1f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 1f;
        public float ambienceVolume = 0.8f;
        public float uiVolume = 0.9f;
        public float dialogueVolume = 1f;

        // Графика
        public int qualityProfile = (int)QualityProfile.Medium;
        public int screenWidth = 1920;
        public int screenHeight = 1080;
        public bool fullscreen = true;

        // Управление
        public float cameraSensitivity = 1f;
        public bool invertCameraY;

        // Доступность (FR-095)
        /// <summary>Масштаб интерфейса. Влияет на размер шрифта и элементов.</summary>
        public float uiScale = 1f;

        /// <summary>Показывать субтитры для речи и звуковых событий.</summary>
        public bool subtitlesEnabled = true;

        /// <summary>
        /// Дублировать информацию, передаваемую цветом, текстом и иконкой:
        /// критическая информация не должна сообщаться только цветом (п. 9 ТЗ).
        /// </summary>
        public bool colorBlindSafeMode;

        /// <summary>Отключить тряску камеры и резкие эффекты.</summary>
        public bool reduceMotion;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>
        /// Приводит значения к допустимым границам. Вызывается после загрузки:
        /// файл настроек мог быть повреждён или отредактирован вручную.
        /// </summary>
        public void Sanitize()
        {
            interfaceLanguage = interfaceLanguage is "ru" or "kk" ? interfaceLanguage : "ru";

            if (!Enum.TryParse<TranslationMode>(translationMode, out _))
                translationMode = nameof(TranslationMode.RussianWithKazakh);

            masterVolume = Math.Clamp(masterVolume, 0f, 1f);
            musicVolume = Math.Clamp(musicVolume, 0f, 1f);
            sfxVolume = Math.Clamp(sfxVolume, 0f, 1f);
            ambienceVolume = Math.Clamp(ambienceVolume, 0f, 1f);
            uiVolume = Math.Clamp(uiVolume, 0f, 1f);
            dialogueVolume = Math.Clamp(dialogueVolume, 0f, 1f);

            qualityProfile = Math.Clamp(qualityProfile,
                (int)QualityProfile.Low, (int)QualityProfile.High);

            // Разрешение ниже 1280×720 не поддерживается: ТЗ задаёт его как
            // минимальную конфигурацию (NFR-002).
            screenWidth = Math.Max(1280, screenWidth);
            screenHeight = Math.Max(720, screenHeight);

            cameraSensitivity = Math.Clamp(cameraSensitivity, 0.2f, 3f);
            uiScale = Math.Clamp(uiScale, MinUiScale, MaxUiScale);
        }

        public TranslationMode GetTranslationMode()
            => Enum.TryParse<TranslationMode>(translationMode, out var mode)
                ? mode
                : TranslationMode.RussianWithKazakh;

        public QualityProfile GetQualityProfile() => (QualityProfile)qualityProfile;
    }

    public readonly struct SettingsChangedEvent : IGameEvent
    {
        public SettingsChangedEvent(GameSettings settings) => Settings = settings;
        public GameSettings Settings { get; }
    }

    /// <summary>
    /// Хранит и применяет настройки. Изменения применяются сразу и сохраняются
    /// между сессиями (FR-093).
    /// </summary>
    public interface ISettingsService
    {
        GameSettings Current { get; }

        /// <summary>Применяет и сохраняет изменённые настройки.</summary>
        void Apply(GameSettings settings);

        void Reload();
    }
}
