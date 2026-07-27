using System;
using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.Language
{
    /// <summary>Настройки прогресса языка. Пороги задаются данными, а не кодом (п. 10 ТЗ).</summary>
    [Serializable]
    public struct LanguageProgressSettings
    {
        /// <summary>Опыт, необходимый для каждого следующего уровня.</summary>
        public float experiencePerLevel;

        /// <summary>Максимальный уровень языка.</summary>
        public int maxLevel;

        /// <summary>Сколько верных ответов подряд поднимают этап освоения.</summary>
        public int correctAnswersPerStage;

        /// <summary>
        /// Этап, начиная с которого адаптивные подсказки перестают показывать
        /// перевод слова (FR-044).
        /// </summary>
        public MasteryStage hideTranslationFromStage;

        public static LanguageProgressSettings Default => new()
        {
            experiencePerLevel = 100f,
            maxLevel = 10,
            correctAnswersPerStage = 2,
            hideTranslationFromStage = MasteryStage.Familiar
        };

        public bool IsValid()
            => experiencePerLevel > 0f
               && maxLevel >= 1
               && correctAnswersPerStage >= 1;
    }

    /// <summary>
    /// Прогресс изучения казахского языка (FR-040 — FR-046).
    /// Не зависит от Unity-плеера, поэтому полностью покрывается модульными тестами.
    /// </summary>
    public sealed class LanguageProgressService : ILanguageProgressService, IGameService
    {
        private readonly IEventBus _eventBus;
        private readonly LanguageProgressSettings _settings;
        private readonly Dictionary<string, LearnedWord> _words = new();

        private TranslationMode _mode = TranslationMode.RussianWithKazakh;
        private int _level = 1;
        private float _experience;

        public LanguageProgressService(IEventBus eventBus, LanguageProgressSettings settings)
        {
            if (!settings.IsValid())
                throw new ArgumentException("Некорректные настройки прогресса языка.", nameof(settings));

            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _settings = settings;
        }

        public TranslationMode Mode => _mode;

        public int Level => _level;

        public float Experience => _experience;

        public bool ForceFullTranslation { get; set; }

        public IReadOnlyCollection<LearnedWord> LearnedWords => _words.Values;

        public void Initialize()
        {
        }

        public void Shutdown() => _words.Clear();

        public void SetMode(TranslationMode mode)
        {
            if (_mode == mode)
                return;

            var previous = _mode;
            _mode = mode;
            _eventBus.Publish(new TranslationModeChangedEvent(previous, mode));
        }

        public void AddWord(string wordId)
        {
            if (string.IsNullOrWhiteSpace(wordId))
                throw new ArgumentException("Пустой идентификатор слова.", nameof(wordId));

            // Повторное добавление не сбрасывает уже достигнутый этап освоения.
            if (_words.ContainsKey(wordId))
                return;

            var word = new LearnedWord(wordId, MasteryStage.Seen, 0, 0);
            _words.Add(wordId, word);
            _eventBus.Publish(new WordLearnedEvent(wordId, word.Stage));
        }

        public bool TryGetWord(string wordId, out LearnedWord word)
        {
            if (string.IsNullOrWhiteSpace(wordId))
            {
                word = default;
                return false;
            }

            return _words.TryGetValue(wordId, out word);
        }

        public void RegisterAnswer(string wordId, bool isCorrect)
        {
            if (string.IsNullOrWhiteSpace(wordId))
                throw new ArgumentException("Пустой идентификатор слова.", nameof(wordId));

            if (!_words.TryGetValue(wordId, out var word))
            {
                AddWord(wordId);
                word = _words[wordId];
            }

            var correct = word.CorrectAnswers + (isCorrect ? 1 : 0);
            var wrong = word.WrongAnswers + (isCorrect ? 0 : 1);
            var stage = ResolveStage(correct, wrong);

            _words[wordId] = new LearnedWord(wordId, stage, correct, wrong);

            if (stage != word.Stage)
                _eventBus.Publish(new WordLearnedEvent(wordId, stage));
        }

        public void AddExperience(float amount)
        {
            if (amount <= 0f || _level >= _settings.maxLevel)
                return;

            var previousLevel = _level;
            _experience += amount;

            while (_level < _settings.maxLevel && _experience >= _settings.experiencePerLevel)
            {
                _experience -= _settings.experiencePerLevel;
                _level++;
            }

            if (_level >= _settings.maxLevel)
                _experience = 0f;

            if (_level != previousLevel)
                _eventBus.Publish(new LanguageLevelChangedEvent(previousLevel, _level));
        }

        public bool ShouldShowTranslation(string wordId)
        {
            // Настройка доступности всегда важнее адаптивных подсказок (FR-044).
            if (ForceFullTranslation)
                return true;

            switch (_mode)
            {
                case TranslationMode.KazakhOnly:
                    return false;

                case TranslationMode.InterfaceLanguageOnly:
                    return false;

                case TranslationMode.RussianWithKazakh:
                case TranslationMode.KazakhWithRussian:
                    if (!TryGetWord(wordId, out var word))
                        return true;

                    return word.Stage < _settings.hideTranslationFromStage;

                default:
                    return true;
            }
        }

        public void RestoreState(LanguageSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _words.Clear();
            _level = Math.Max(1, data.level);
            _experience = Math.Max(0f, data.experience);
            _mode = Enum.TryParse<TranslationMode>(data.translationMode, out var parsed)
                ? parsed
                : TranslationMode.RussianWithKazakh;

            if (data.learnedWords == null)
                return;

            foreach (var entry in data.learnedWords)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.wordId))
                    continue;

                var stage = (MasteryStage)Math.Clamp(
                    entry.masteryStage, (int)MasteryStage.New, (int)MasteryStage.Mastered);

                _words[entry.wordId] = new LearnedWord(
                    entry.wordId, stage, entry.correctAnswers, entry.wrongAnswers);
            }
        }

        public void CaptureState(LanguageSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.level = _level;
            data.experience = _experience;
            data.translationMode = _mode.ToString();
            data.learnedWords.Clear();

            foreach (var word in _words.Values)
            {
                data.learnedWords.Add(new LearnedWordData
                {
                    wordId = word.WordId,
                    masteryStage = (int)word.Stage,
                    correctAnswers = word.CorrectAnswers,
                    wrongAnswers = word.WrongAnswers
                });
            }
        }

        private MasteryStage ResolveStage(int correctAnswers, int wrongAnswers)
        {
            // Ошибки притормаживают продвижение, но не отбрасывают слово в New.
            var netProgress = correctAnswers - wrongAnswers;
            if (netProgress <= 0)
                return MasteryStage.Seen;

            var stagesGained = netProgress / _settings.correctAnswersPerStage;
            var stage = (int)MasteryStage.Seen + stagesGained;

            return (MasteryStage)Math.Min(stage, (int)MasteryStage.Mastered);
        }
    }
}
