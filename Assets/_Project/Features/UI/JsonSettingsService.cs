using System;
using System.IO;
using QonaevLife.Core;
using UnityEngine;

namespace QonaevLife.UI
{
    /// <summary>
    /// Настройки в JSON рядом с сохранениями (FR-093). Запись атомарна, а
    /// повреждённый файл не роняет запуск: настройки сбрасываются к значениям
    /// по умолчанию, и игра продолжает работать (NFR-006).
    /// </summary>
    public sealed class JsonSettingsService : ISettingsService, IGameService
    {
        private const string FileName = "settings.json";
        private const string TempExtension = ".tmp";

        private readonly string _directory;
        private readonly IEventBus _eventBus;

        public JsonSettingsService(string directory, IEventBus eventBus)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Не задана папка настроек.", nameof(directory));

            _directory = directory;
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            Current = new GameSettings();
        }

        public GameSettings Current { get; private set; }

        public void Initialize() => Reload();

        public void Shutdown()
        {
        }

        public void Reload()
        {
            var path = Path.Combine(_directory, FileName);

            if (!File.Exists(path))
            {
                Current = new GameSettings();
                Current.Sanitize();
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonUtility.FromJson<GameSettings>(json);

                Current = loaded ?? new GameSettings();
            }
            catch (Exception exception)
            {
                // Повреждённый файл настроек — не причина не запускать игру.
                Debug.LogWarning(
                    $"[Настройки] Не удалось прочитать {path}: {exception.Message}. " +
                    "Использованы значения по умолчанию.");

                Current = new GameSettings();
            }

            Current.Sanitize();
        }

        public void Apply(GameSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            settings.Sanitize();
            Current = settings;

            Save();
            _eventBus.Publish(new SettingsChangedEvent(Current));
        }

        private void Save()
        {
            var path = Path.Combine(_directory, FileName);
            var tempPath = path + TempExtension;

            try
            {
                Directory.CreateDirectory(_directory);

                File.WriteAllText(tempPath, JsonUtility.ToJson(Current, prettyPrint: true));

                // Замена одним действием: сбой при записи не оставит игрока
                // без настроек и не создаст полуфайл.
                if (File.Exists(path))
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                else
                    File.Move(tempPath, path);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Настройки] Не удалось сохранить: {exception.Message}");

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning(
                        $"[Настройки] Временный файл не удалён: {cleanupException.Message}");
                }
            }
        }
    }
}
