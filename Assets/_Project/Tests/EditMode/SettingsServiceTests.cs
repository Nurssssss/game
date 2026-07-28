using System.IO;
using NUnit.Framework;
using QonaevLife.Core;
using QonaevLife.Language;
using QonaevLife.UI;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Настройки и доступность (FR-093, FR-095).</summary>
    [TestFixture]
    public sealed class SettingsServiceTests
    {
        private string _directory;
        private EventBus _eventBus;
        private JsonSettingsService _service;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "QonaevSettings", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);

            _eventBus = new EventBus();
            _service = new JsonSettingsService(_directory, _eventBus);
            _service.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void FirstRun_UsesDefaults()
        {
            Assert.That(_service.Current.interfaceLanguage, Is.EqualTo("ru"));
            Assert.That(_service.Current.uiScale, Is.EqualTo(1f));
            Assert.That(_service.Current.subtitlesEnabled, Is.True);
            Assert.That(_service.Current.GetQualityProfile(), Is.EqualTo(QualityProfile.Medium));
        }

        /// <summary>FR-093: изменения сохраняются между сессиями.</summary>
        [Test]
        public void Apply_PersistsAcrossSessions()
        {
            var settings = _service.Current.Clone();
            settings.uiScale = 1.4f;
            settings.masterVolume = 0.5f;
            settings.interfaceLanguage = "kk";
            settings.subtitlesEnabled = false;

            _service.Apply(settings);

            var reloaded = new JsonSettingsService(_directory, _eventBus);
            reloaded.Initialize();

            Assert.That(reloaded.Current.uiScale, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(reloaded.Current.masterVolume, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(reloaded.Current.interfaceLanguage, Is.EqualTo("kk"));
            Assert.That(reloaded.Current.subtitlesEnabled, Is.False);
        }

        [Test]
        public void Apply_PublishesEvent()
        {
            SettingsChangedEvent captured = default;
            var count = 0;
            _eventBus.Subscribe<SettingsChangedEvent>(e => { captured = e; count++; });

            var settings = _service.Current.Clone();
            settings.uiScale = 1.2f;
            _service.Apply(settings);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(captured.Settings.uiScale, Is.EqualTo(1.2f).Within(0.001f));
        }

        /// <summary>FR-095: масштаб UI зажимается в допустимые границы.</summary>
        [Test]
        public void UiScale_IsClamped()
        {
            var settings = _service.Current.Clone();
            settings.uiScale = 99f;
            _service.Apply(settings);

            Assert.That(_service.Current.uiScale, Is.EqualTo(GameSettings.MaxUiScale));

            settings = _service.Current.Clone();
            settings.uiScale = 0.01f;
            _service.Apply(settings);

            Assert.That(_service.Current.uiScale, Is.EqualTo(GameSettings.MinUiScale));
        }

        [Test]
        public void Volumes_AreClampedToUnitRange()
        {
            var settings = _service.Current.Clone();
            settings.masterVolume = 5f;
            settings.musicVolume = -3f;
            _service.Apply(settings);

            Assert.That(_service.Current.masterVolume, Is.EqualTo(1f));
            Assert.That(_service.Current.musicVolume, Is.EqualTo(0f));
        }

        /// <summary>NFR-002: разрешение не опускается ниже минимальной конфигурации.</summary>
        [Test]
        public void Resolution_HasMinimumBound()
        {
            var settings = _service.Current.Clone();
            settings.screenWidth = 320;
            settings.screenHeight = 240;
            _service.Apply(settings);

            Assert.That(_service.Current.screenWidth, Is.EqualTo(1280));
            Assert.That(_service.Current.screenHeight, Is.EqualTo(720));
        }

        [Test]
        public void UnknownLanguage_FallsBackToRussian()
        {
            var settings = _service.Current.Clone();
            settings.interfaceLanguage = "fr";
            _service.Apply(settings);

            Assert.That(_service.Current.interfaceLanguage, Is.EqualTo("ru"));
        }

        [Test]
        public void UnknownTranslationMode_FallsBackToDefault()
        {
            var settings = _service.Current.Clone();
            settings.translationMode = "мусор";
            _service.Apply(settings);

            Assert.That(_service.Current.GetTranslationMode(),
                Is.EqualTo(TranslationMode.RussianWithKazakh));
        }

        [Test]
        public void QualityProfile_IsClamped()
        {
            var settings = _service.Current.Clone();
            settings.qualityProfile = 99;
            _service.Apply(settings);

            Assert.That(_service.Current.GetQualityProfile(), Is.EqualTo(QualityProfile.High));
        }

        /// <summary>NFR-006: повреждённый файл настроек не мешает запуску.</summary>
        [Test]
        public void CorruptedFile_FallsBackToDefaults()
        {
            File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ это не json");

            var service = new JsonSettingsService(_directory, _eventBus);

            Assert.DoesNotThrow(() => service.Initialize());
            Assert.That(service.Current.uiScale, Is.EqualTo(1f));
        }

        [Test]
        public void Apply_DoesNotLeaveTempFiles()
        {
            var settings = _service.Current.Clone();
            settings.uiScale = 1.1f;
            _service.Apply(settings);
            _service.Apply(settings);

            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void ApplyNull_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => _service.Apply(null));
        }

        /// <summary>Настройки доступности сохраняются вместе с остальными.</summary>
        [Test]
        public void AccessibilityFlags_Persist()
        {
            var settings = _service.Current.Clone();
            settings.colorBlindSafeMode = true;
            settings.reduceMotion = true;
            settings.forceFullTranslation = true;
            _service.Apply(settings);

            var reloaded = new JsonSettingsService(_directory, _eventBus);
            reloaded.Initialize();

            Assert.That(reloaded.Current.colorBlindSafeMode, Is.True);
            Assert.That(reloaded.Current.reduceMotion, Is.True);
            Assert.That(reloaded.Current.forceFullTranslation, Is.True);
        }
    }
}
