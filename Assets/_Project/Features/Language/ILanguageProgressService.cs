using System;
using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.Language
{
    /// <summary>Режимы показа двуязычных реплик (FR-041).</summary>
    public enum TranslationMode
    {
        /// <summary>Русский текст с казахским переводом.</summary>
        RussianWithKazakh = 0,

        /// <summary>Казахский текст с русским переводом.</summary>
        KazakhWithRussian = 1,

        /// <summary>Казахский без перевода — режим погружения.</summary>
        KazakhOnly = 2,

        /// <summary>Только язык интерфейса, без учебного слоя.</summary>
        InterfaceLanguageOnly = 3
    }

    /// <summary>Этап освоения слова личного словаря (FR-042).</summary>
    public enum MasteryStage
    {
        New = 0,
        Seen = 1,
        Learning = 2,
        Familiar = 3,
        Mastered = 4
    }

    /// <summary>Состояние слова в личном словаре игрока.</summary>
    public readonly struct LearnedWord
    {
        public LearnedWord(string wordId, MasteryStage stage, int correctAnswers, int wrongAnswers)
        {
            WordId = wordId;
            Stage = stage;
            CorrectAnswers = correctAnswers;
            WrongAnswers = wrongAnswers;
        }

        public string WordId { get; }
        public MasteryStage Stage { get; }
        public int CorrectAnswers { get; }
        public int WrongAnswers { get; }
    }

    public readonly struct TranslationModeChangedEvent : IGameEvent
    {
        public TranslationModeChangedEvent(TranslationMode previous, TranslationMode current)
        {
            Previous = previous;
            Current = current;
        }

        public TranslationMode Previous { get; }
        public TranslationMode Current { get; }
    }

    public readonly struct WordLearnedEvent : IGameEvent
    {
        public WordLearnedEvent(string wordId, MasteryStage stage)
        {
            WordId = wordId;
            Stage = stage;
        }

        public string WordId { get; }
        public MasteryStage Stage { get; }
    }

    public readonly struct LanguageLevelChangedEvent : IGameEvent
    {
        public LanguageLevelChangedEvent(int previousLevel, int newLevel)
        {
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
        }

        public int PreviousLevel { get; }
        public int NewLevel { get; }
    }

    /// <summary>
    /// Прогресс казахского языка (FR-040 — FR-046). Учитывается отдельно от
    /// общих навыков и влияет только на подсказки и варианты диалогов.
    /// </summary>
    public interface ILanguageProgressService
    {
        TranslationMode Mode { get; }

        int Level { get; }

        float Experience { get; }

        /// <summary>
        /// Принудительный полный перевод из настроек доступности. Когда включён,
        /// адаптивные подсказки не сокращают перевод (FR-044).
        /// </summary>
        bool ForceFullTranslation { get; set; }

        IReadOnlyCollection<LearnedWord> LearnedWords { get; }

        /// <summary>Смена режима не сбрасывает прогресс диалога (FR-041).</summary>
        void SetMode(TranslationMode mode);

        /// <summary>Добавляет слово в личный словарь; повторный вызов не сбрасывает этап.</summary>
        void AddWord(string wordId);

        bool TryGetWord(string wordId, out LearnedWord word);

        /// <summary>Регистрирует ответ в мини-уроке и продвигает этап освоения (FR-043).</summary>
        void RegisterAnswer(string wordId, bool isCorrect);

        void AddExperience(float amount);

        /// <summary>
        /// Нужно ли показывать перевод для слова с учётом режима, уровня освоения
        /// и настройки доступности (FR-044).
        /// </summary>
        bool ShouldShowTranslation(string wordId);
    }
}
