using System.Collections.Generic;

namespace QonaevLife.UI
{
    /// <summary>
    /// Источник локализованных строк (FR-094, NFR-022). UI никогда не собирает
    /// текст в коде: он запрашивает его по ключу, поэтому правки формулировок
    /// делаются в данных (п. 11 ТЗ).
    /// </summary>
    public interface ILocalizedText
    {
        /// <summary>Текущий язык интерфейса: "ru" или "kk".</summary>
        string Language { get; }

        /// <summary>
        /// Возвращает строку по ключу. Отсутствующий ключ возвращается как
        /// «#ключ», чтобы необработанная локализация была заметна при проверке,
        /// а не молча превратилась в пустое место.
        /// </summary>
        string Resolve(string key);

        bool HasKey(string key);
    }

    /// <summary>
    /// Простой словарь строк на время прототипа. Полноценный Unity Localization
    /// подключается отдельным пакетом контента (FR-094).
    /// </summary>
    public sealed class DictionaryLocalizedText : ILocalizedText
    {
        private readonly Dictionary<string, string> _entries;

        public DictionaryLocalizedText(string language, Dictionary<string, string> entries)
        {
            Language = string.IsNullOrWhiteSpace(language) ? "ru" : language;
            _entries = entries ?? new Dictionary<string, string>();
        }

        public string Language { get; }

        public string Resolve(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return _entries.TryGetValue(key, out var value) ? value : $"#{key}";
        }

        public bool HasKey(string key)
            => !string.IsNullOrWhiteSpace(key) && _entries.ContainsKey(key);

        /// <summary>Русские строки интерфейса прототипа.</summary>
        public static DictionaryLocalizedText CreateRussianPrototype()
            => new("ru", new Dictionary<string, string>
            {
                // Подсказки взаимодействия
                ["prompt.interact"] = "E — взаимодействовать",
                ["prompt.enter_home"] = "E — войти домой",
                ["prompt.take_shift"] = "E — взять смену курьера",
                ["prompt.open_shop"] = "E — открыть магазин",
                ["prompt.deliver_order"] = "E — вручить заказ",
                ["prompt.closed"] = "Закрыто — приходите в рабочие часы",

                // Локации
                ["loc.apartment"] = "Квартира",
                ["loc.courier_hub"] = "Курьерский пункт",
                ["loc.shop"] = "Магазин",
                ["loc.cafe"] = "Кафе",

                // HUD
                ["hud.money"] = "Деньги",
                ["hud.objective"] = "Цель",
                ["hud.no_objective"] = "Нет активной цели",
                ["hud.day"] = "День",

                // Фазы суток
                ["phase.Morning"] = "Утро",
                ["phase.Day"] = "День",
                ["phase.Evening"] = "Вечер",
                ["phase.Night"] = "Ночь",

                // Погода
                ["weather.Clear"] = "Ясно",
                ["weather.Cloudy"] = "Облачно",
                ["weather.Rain"] = "Дождь",

                // Работа
                ["job.courier"] = "Курьер",
                ["job.courier.pickup"] = "Забрать посылку на курьерском пункте",
                ["job.courier.deliver"] = "Доставить посылку в кафе",
                ["job.shift_started"] = "Смена начата",
                ["job.shift_completed"] = "Смена завершена. Начислено:",
                ["job.shift_failed"] = "Смена провалена",

                // Диалог
                ["dialogue.locked.language"] = "Нужен более высокий уровень казахского",
                ["dialogue.locked.trust"] = "Недостаточно доверия",
                ["dialogue.locked.flag"] = "Пока недоступно",
                ["dialogue.add_word"] = "Добавить слово в словарь",
                ["dialogue.close"] = "Закрыть",

                // Режимы перевода (FR-041)
                ["mode.RussianWithKazakh"] = "Русский + казахский перевод  (T — сменить)",
                ["mode.KazakhWithRussian"] = "Казахский + русский перевод  (T — сменить)",
                ["mode.KazakhOnly"] = "Только казахский  (T — сменить)",
                ["mode.InterfaceLanguageOnly"] = "Только язык интерфейса  (T — сменить)",

                // Словарные слова
                ["word.salem"] = "Сәлем",
                ["word.sau_bolynyz"] = "Сау болыңыз",
                ["word.rahmet"] = "Рақмет",
                ["word.iya"] = "Иә",
                ["word.zhok"] = "Жоқ",
                ["word.zhumys"] = "Жұмыс",
                ["word.kofe"] = "Кофе",

                // Потребности
                ["need.hunger"] = "Голод",
                ["need.energy"] = "Энергия",
                ["need.fatigue"] = "Усталость",
                ["need.mood"] = "Настроение",

                // Предметы
                ["item.bread"] = "Хлеб",
                ["item.water"] = "Вода",
                ["item.coffee"] = "Кофе",

                // NPC
                ["npc.dispatcher"] = "Диспетчер",
                ["npc.aidana"] = "Айдана",
                ["profession.dispatcher"] = "Диспетчер",
                ["profession.barista"] = "Бариста"
            });
    }
}
