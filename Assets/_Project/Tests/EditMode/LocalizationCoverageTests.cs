using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Economy;
using QonaevLife.Language;
using QonaevLife.UI;
using QonaevLife.World;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>
    /// Полнота словаря интерфейса (NFR-022: не допускаются необработанные
    /// ключи локализации). Ключ, которого нет в словаре, показывается игроку
    /// как «#ключ» — этот тест ловит такие пропуски до сборки.
    /// </summary>
    [TestFixture]
    public sealed class LocalizationCoverageTests
    {
        private ILocalizedText _text;

        [SetUp]
        public void SetUp() => _text = DictionaryLocalizedText.CreateRussianPrototype();

        /// <summary>Ключи, которые UI запрашивает напрямую.</summary>
        private static readonly string[] StaticKeys =
        {
            // Главное меню (FR-001)
            "menu.title", "menu.new_game", "menu.continue", "menu.load",
            "menu.settings", "menu.credits", "menu.quit", "menu.version",
            "menu.hint_new", "menu.hint_continue",

            // Слоты (FR-002 — FR-004)
            "slots.title_load", "slots.title_save",
            "slot.empty", "slot.valid", "slot.corrupted", "slot.unsupported",
            "slot.unknown", "slot.no_name",

            // Настройки (FR-093)
            "settings.title",

            // HUD (FR-090)
            "hud.day", "hud.money", "hud.objective", "hud.no_objective",

            // Телефон (FR-091)
            "phone.tasks_empty", "phone.dictionary_empty", "phone.finance_empty",
            "phone.contacts_empty", "phone.transport_empty",
            "phone.language_level", "phone.time_left",

            // Карта (FR-092)
            "map.you",

            // Работа (FR-070)
            "job.shift_started", "job.shift_completed", "job.shift_failed",

            // Диалог (FR-046)
            "dialogue.locked.language", "dialogue.locked.trust", "dialogue.locked.flag",

            // Взаимодействие (FR-012)
            "prompt.interact", "prompt.closed",

            // Общие
            "common.back", "common.minutes"
        };

        [Test]
        public void AllStaticKeys_AreTranslated()
        {
            var missing = new List<string>();

            foreach (var key in StaticKeys)
            {
                if (!_text.HasKey(key))
                    missing.Add(key);
            }

            Assert.That(missing, Is.Empty,
                $"Нет перевода для ключей: {string.Join(", ", missing)}");
        }

        /// <summary>
        /// Ключи, собираемые из значений перечислений. Тест перебирает все
        /// значения, поэтому новый элемент перечисления без перевода
        /// немедленно ломает сборку.
        /// </summary>
        [Test]
        public void AllDayPhases_AreTranslated()
            => AssertEnumKeys<Core.DayPhase>("phase.");

        [Test]
        public void AllWeatherStates_AreTranslated()
            => AssertEnumKeys<WeatherState>("weather.");

        [Test]
        public void AllTranslationModes_AreTranslated()
            => AssertEnumKeys<TranslationMode>("mode.");

        [Test]
        public void AllMasteryStages_AreTranslated()
            => AssertEnumKeys<MasteryStage>("mastery.");

        [Test]
        public void AllTransactionReasons_AreTranslated()
            => AssertEnumKeys<TransactionReason>("reason.");

        [Test]
        public void AllPhoneTabs_AreTranslated()
            => AssertEnumKeys<PhoneTab>("phone.tab.");

        /// <summary>Отсутствующий ключ возвращается заметно, а не пустой строкой.</summary>
        [Test]
        public void MissingKey_IsVisiblyMarked()
        {
            var resolved = _text.Resolve("key.that.does.not.exist");

            Assert.That(resolved, Does.StartWith("#"),
                "Пропущенный перевод должен быть виден при проверке, а не исчезать.");
        }

        [Test]
        public void EmptyKey_ResolvesToEmptyString()
        {
            Assert.That(_text.Resolve(null), Is.Empty);
            Assert.That(_text.Resolve("  "), Is.Empty);
        }

        [Test]
        public void Language_IsRussianByDefault()
        {
            Assert.That(_text.Language, Is.EqualTo("ru"));
        }

        private void AssertEnumKeys<TEnum>(string prefix) where TEnum : System.Enum
        {
            var missing = new List<string>();

            foreach (var value in System.Enum.GetValues(typeof(TEnum)))
            {
                var key = $"{prefix}{value}";
                if (!_text.HasKey(key))
                    missing.Add(key);
            }

            Assert.That(missing, Is.Empty,
                $"Нет перевода для ключей: {string.Join(", ", missing)}");
        }
    }
}
