using System;
using System.Collections.Generic;

namespace QonaevLife.Core
{
    /// <summary>
    /// Пошаговая миграция сохранений между версиями схемы (FR-004).
    /// Каждый шаг поднимает данные ровно на одну версию, поэтому цепочка
    /// 1 → 2 → 3 работает без отдельного кода для каждой пары версий.
    /// </summary>
    public static class SaveMigrations
    {
        /// <summary>
        /// Шаги миграции: ключ — версия, ИЗ которой мигрируем.
        /// Возвращают true при успехе. Пока схема версии 1 — таблица пуста.
        /// </summary>
        private static readonly Dictionary<int, Func<SaveData, bool>> Steps = new();

        public static bool TryMigrate(SaveData data, out string error)
        {
            if (data == null)
            {
                error = "Данные сохранения отсутствуют.";
                return false;
            }

            if (data.SchemaVersion > SaveData.CurrentSchemaVersion)
            {
                error = $"Версия схемы {data.SchemaVersion} новее поддерживаемой " +
                        $"{SaveData.CurrentSchemaVersion}.";
                return false;
            }

            while (data.SchemaVersion < SaveData.CurrentSchemaVersion)
            {
                var from = data.SchemaVersion;

                if (!Steps.TryGetValue(from, out var step))
                {
                    error = $"Нет шага миграции с версии {from}.";
                    return false;
                }

                if (!step(data))
                {
                    error = $"Шаг миграции с версии {from} завершился ошибкой.";
                    return false;
                }

                if (data.SchemaVersion <= from)
                {
                    // Защита от бесконечного цикла, если шаг забыл поднять версию.
                    error = $"Шаг миграции с версии {from} не увеличил версию схемы.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
