using NUnit.Framework;
using QonaevLife.Core;
using QonaevLife.Language;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Прогресс казахского языка (FR-041 — FR-046).</summary>
    [TestFixture]
    public sealed class LanguageProgressServiceTests
    {
        private EventBus _eventBus;
        private LanguageProgressService _service;

        private static LanguageProgressSettings Settings => new()
        {
            experiencePerLevel = 100f,
            maxLevel = 5,
            correctAnswersPerStage = 2,
            hideTranslationFromStage = MasteryStage.Familiar
        };

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _service = new LanguageProgressService(_eventBus, Settings);
        }

        [Test]
        public void NewService_StartsAtLevelOneWithDefaultMode()
        {
            Assert.That(_service.Level, Is.EqualTo(1));
            Assert.That(_service.Experience, Is.Zero);
            Assert.That(_service.Mode, Is.EqualTo(TranslationMode.RussianWithKazakh));
            Assert.That(_service.LearnedWords, Is.Empty);
        }

        [Test]
        public void SetMode_PublishesEventOnce()
        {
            var events = 0;
            _eventBus.Subscribe<TranslationModeChangedEvent>(_ => events++);

            _service.SetMode(TranslationMode.KazakhOnly);
            _service.SetMode(TranslationMode.KazakhOnly); // тот же режим — без события

            Assert.That(_service.Mode, Is.EqualTo(TranslationMode.KazakhOnly));
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void AddWord_AddsToDictionaryOnce()
        {
            _service.AddWord("word_salem");
            _service.AddWord("word_salem");

            Assert.That(_service.LearnedWords, Has.Count.EqualTo(1));
            Assert.That(_service.TryGetWord("word_salem", out var word), Is.True);
            Assert.That(word.Stage, Is.EqualTo(MasteryStage.Seen));
        }

        /// <summary>Повторное добавление не сбрасывает уже достигнутый этап.</summary>
        [Test]
        public void AddWord_DoesNotResetExistingProgress()
        {
            _service.AddWord("word_rahmet");
            _service.RegisterAnswer("word_rahmet", isCorrect: true);
            _service.RegisterAnswer("word_rahmet", isCorrect: true);

            _service.TryGetWord("word_rahmet", out var before);
            _service.AddWord("word_rahmet");
            _service.TryGetWord("word_rahmet", out var after);

            Assert.That(after.Stage, Is.EqualTo(before.Stage));
            Assert.That(after.CorrectAnswers, Is.EqualTo(2));
        }

        [Test]
        public void CorrectAnswers_AdvanceMasteryStage()
        {
            _service.AddWord("word_su");

            _service.RegisterAnswer("word_su", isCorrect: true);
            _service.RegisterAnswer("word_su", isCorrect: true);

            _service.TryGetWord("word_su", out var word);
            Assert.That(word.Stage, Is.EqualTo(MasteryStage.Learning));
            Assert.That(word.CorrectAnswers, Is.EqualTo(2));
        }

        [Test]
        public void WrongAnswers_SlowProgressButDoNotResetToNew()
        {
            _service.AddWord("word_nan");

            _service.RegisterAnswer("word_nan", isCorrect: true);
            _service.RegisterAnswer("word_nan", isCorrect: false);
            _service.RegisterAnswer("word_nan", isCorrect: false);

            _service.TryGetWord("word_nan", out var word);
            Assert.That(word.Stage, Is.EqualTo(MasteryStage.Seen));
            Assert.That(word.WrongAnswers, Is.EqualTo(2));
        }

        [Test]
        public void MasteryStage_IsCappedAtMastered()
        {
            _service.AddWord("word_kok");

            for (var i = 0; i < 50; i++)
                _service.RegisterAnswer("word_kok", isCorrect: true);

            _service.TryGetWord("word_kok", out var word);
            Assert.That(word.Stage, Is.EqualTo(MasteryStage.Mastered));
        }

        [Test]
        public void RegisterAnswer_ForUnknownWord_AddsItFirst()
        {
            _service.RegisterAnswer("word_new", isCorrect: true);

            Assert.That(_service.TryGetWord("word_new", out _), Is.True);
        }

        [Test]
        public void AddExperience_LevelsUpAndPublishesEvent()
        {
            LanguageLevelChangedEvent captured = default;
            _eventBus.Subscribe<LanguageLevelChangedEvent>(e => captured = e);

            _service.AddExperience(250f);

            Assert.That(_service.Level, Is.EqualTo(3));
            Assert.That(_service.Experience, Is.EqualTo(50f).Within(0.001f));
            Assert.That(captured.PreviousLevel, Is.EqualTo(1));
            Assert.That(captured.NewLevel, Is.EqualTo(3));
        }

        [Test]
        public void Experience_StopsAtMaxLevel()
        {
            _service.AddExperience(10_000f);

            Assert.That(_service.Level, Is.EqualTo(Settings.maxLevel));

            _service.AddExperience(500f);

            Assert.That(_service.Level, Is.EqualTo(Settings.maxLevel));
        }

        /// <summary>FR-041: режим «казахский без перевода» скрывает перевод.</summary>
        [Test]
        public void KazakhOnlyMode_HidesTranslation()
        {
            _service.SetMode(TranslationMode.KazakhOnly);

            Assert.That(_service.ShouldShowTranslation("word_any"), Is.False);
        }

        [Test]
        public void UnknownWord_ShowsTranslation()
        {
            Assert.That(_service.ShouldShowTranslation("word_unseen"), Is.True);
        }

        /// <summary>FR-044: адаптивные подсказки убирают перевод освоенных слов.</summary>
        [Test]
        public void MasteredWord_HidesTranslation()
        {
            _service.AddWord("word_kitap");
            for (var i = 0; i < 6; i++)
                _service.RegisterAnswer("word_kitap", isCorrect: true);

            Assert.That(_service.ShouldShowTranslation("word_kitap"), Is.False);
        }

        /// <summary>FR-044: настройка доступности возвращает полный перевод принудительно.</summary>
        [Test]
        public void ForceFullTranslation_OverridesAdaptiveHints()
        {
            _service.AddWord("word_kitap");
            for (var i = 0; i < 6; i++)
                _service.RegisterAnswer("word_kitap", isCorrect: true);

            _service.ForceFullTranslation = true;

            Assert.That(_service.ShouldShowTranslation("word_kitap"), Is.True);
        }

        [Test]
        public void ForceFullTranslation_OverridesKazakhOnlyMode()
        {
            _service.SetMode(TranslationMode.KazakhOnly);
            _service.ForceFullTranslation = true;

            Assert.That(_service.ShouldShowTranslation("word_any"), Is.True);
        }

        [Test]
        public void CaptureAndRestore_PreservesProgress()
        {
            _service.SetMode(TranslationMode.KazakhWithRussian);
            _service.AddExperience(150f);
            _service.AddWord("word_salem");
            _service.RegisterAnswer("word_salem", isCorrect: true);
            _service.RegisterAnswer("word_salem", isCorrect: true);

            var data = new LanguageSaveData();
            _service.CaptureState(data);

            var restored = new LanguageProgressService(_eventBus, Settings);
            restored.RestoreState(data);

            Assert.That(restored.Level, Is.EqualTo(_service.Level));
            Assert.That(restored.Experience, Is.EqualTo(_service.Experience).Within(0.001f));
            Assert.That(restored.Mode, Is.EqualTo(TranslationMode.KazakhWithRussian));
            Assert.That(restored.TryGetWord("word_salem", out var word), Is.True);
            Assert.That(word.Stage, Is.EqualTo(MasteryStage.Learning));
            Assert.That(word.CorrectAnswers, Is.EqualTo(2));
        }

        [Test]
        public void EmptyWordId_IsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => _service.AddWord("  "));
            Assert.That(_service.TryGetWord(null, out _), Is.False);
        }
    }
}
