using System;
using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Language;

namespace QonaevLife.Dialogue
{
    /// <summary>Реплика, подготовленная к показу в выбранном режиме перевода.</summary>
    public readonly struct PresentedLine
    {
        public PresentedLine(string primary, string translation, IReadOnlyList<string> wordIds)
        {
            Primary = primary;
            Translation = translation;
            WordIds = wordIds;
        }

        /// <summary>Основной текст реплики.</summary>
        public string Primary { get; }

        /// <summary>Перевод или пустая строка, если он скрыт (FR-041, FR-044).</summary>
        public string Translation { get; }

        /// <summary>Слова, которые можно добавить в словарь (FR-042).</summary>
        public IReadOnlyList<string> WordIds { get; }

        public bool HasTranslation => !string.IsNullOrEmpty(Translation);
    }

    /// <summary>Вариант ответа, доступный игроку прямо сейчас.</summary>
    public readonly struct PresentedChoice
    {
        public PresentedChoice(int index, string choiceId, PresentedLine line, bool isAvailable,
            string lockedReasonKey)
        {
            Index = index;
            ChoiceId = choiceId;
            Line = line;
            IsAvailable = isAvailable;
            LockedReasonKey = lockedReasonKey;
        }

        public int Index { get; }
        public string ChoiceId { get; }
        public PresentedLine Line { get; }

        /// <summary>Доступен ли вариант: хватает уровня языка, доверия и флага.</summary>
        public bool IsAvailable { get; }

        /// <summary>Почему вариант закрыт. Пусто, если доступен.</summary>
        public string LockedReasonKey { get; }
    }

    public readonly struct DialogueStartedEvent : IGameEvent
    {
        public DialogueStartedEvent(string npcId, string nodeId)
        {
            NpcId = npcId;
            NodeId = nodeId;
        }

        public string NpcId { get; }
        public string NodeId { get; }
    }

    public readonly struct DialogueEndedEvent : IGameEvent
    {
        public DialogueEndedEvent(string npcId) => NpcId = npcId;
        public string NpcId { get; }
    }

    public readonly struct DialogueNodeChangedEvent : IGameEvent
    {
        public DialogueNodeChangedEvent(string nodeId) => NodeId = nodeId;
        public string NodeId { get; }
    }

    /// <summary>Диалог запросил эффект — доверие, квест, деньги (FR-033, FR-034).</summary>
    public readonly struct DialogueEffectRequestedEvent : IGameEvent
    {
        public DialogueEffectRequestedEvent(string npcId, string effectType, string targetId,
            float value)
        {
            NpcId = npcId;
            EffectType = effectType;
            TargetId = targetId;
            Value = value;
        }

        public string NpcId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public float Value { get; }
    }

    /// <summary>
    /// Проигрывание диалогов (FR-033, FR-040 — FR-042). Сервис не рисует UI и не
    /// меняет чужое состояние напрямую: эффекты выбора публикуются событиями,
    /// а применяют их владельцы соответствующих данных.
    /// </summary>
    public sealed class DialogueService : IGameService
    {
        /// <summary>Ключи причин недоступности варианта — для локализации UI.</summary>
        public const string LockedByLanguageKey = "dialogue.locked.language";
        public const string LockedByTrustKey = "dialogue.locked.trust";
        public const string LockedByFlagKey = "dialogue.locked.flag";

        private readonly ContentDatabase _content;
        private readonly IEventBus _eventBus;
        private readonly ILanguageProgressService _language;
        private readonly List<PresentedChoice> _choices = new();

        private DialogueNodeDefinition _currentNode;
        private string _currentNpcId = string.Empty;
        private float _currentTrust;
        private HashSet<string> _currentFlags;

        public DialogueService(ContentDatabase content, IEventBus eventBus,
            ILanguageProgressService language)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _language = language ?? throw new ArgumentNullException(nameof(language));
        }

        public bool IsActive => _currentNode != null;

        public string CurrentNodeId => _currentNode != null ? _currentNode.Id : string.Empty;

        public string CurrentNpcId => _currentNpcId;

        /// <summary>Варианты ответа для текущего узла, включая недоступные.</summary>
        public IReadOnlyList<PresentedChoice> Choices => _choices;

        public void Initialize()
        {
        }

        public void Shutdown() => Reset();

        /// <summary>
        /// Начинает диалог с NPC. <paramref name="trust"/> и
        /// <paramref name="flags"/> передаются владельцем состояния NPC, поэтому
        /// сервис диалогов не зависит от модуля NPC.
        /// </summary>
        public bool TryStart(string npcId, float trust, HashSet<string> flags)
        {
            if (!_content.TryGetNpc(npcId, out var npc))
                return false;

            if (!_content.TryGetDialogueNode(npc.RootDialogueId, out var root))
                return false;

            _currentNpcId = npcId;
            _currentTrust = trust;
            _currentFlags = flags ?? new HashSet<string>();
            _currentNode = root;

            RebuildChoices();
            _eventBus.Publish(new DialogueStartedEvent(npcId, root.Id));

            return true;
        }

        /// <summary>Текущая реплика в выбранном режиме перевода (FR-041).</summary>
        public PresentedLine GetCurrentLine()
            => _currentNode == null
                ? default
                : Present(_currentNode.Line, _currentNode.IsTranslationExercise);

        /// <summary>
        /// Выбирает вариант ответа. Недоступный вариант отклоняется, состояние
        /// диалога при этом не меняется.
        /// </summary>
        public bool TrySelectChoice(int index)
        {
            if (_currentNode == null || index < 0 || index >= _choices.Count)
                return false;

            if (!_choices[index].IsAvailable)
                return false;

            var choice = _currentNode.Choices[index];

            PublishEffects(choice);
            AwardLanguageExperience();

            if (string.IsNullOrWhiteSpace(choice.nextNodeId))
            {
                End();
                return true;
            }

            if (!_content.TryGetDialogueNode(choice.nextNodeId, out var next))
            {
                // Битая ссылка ловится проверкой контента; в рантайме
                // безопаснее завершить диалог, чем застрять в узле.
                End();
                return false;
            }

            _currentNode = next;
            RebuildChoices();
            _eventBus.Publish(new DialogueNodeChangedEvent(next.Id));

            return true;
        }

        /// <summary>Добавляет слово текущей реплики в личный словарь (FR-042).</summary>
        public bool TryAddWordToDictionary(string wordId)
        {
            if (!_content.TryGetWord(wordId, out _))
                return false;

            _language.AddWord(wordId);
            return true;
        }

        /// <summary>Завершает диалог принудительно: игрок закрыл окно.</summary>
        public void End()
        {
            if (_currentNode == null)
                return;

            var npcId = _currentNpcId;
            Reset();
            _eventBus.Publish(new DialogueEndedEvent(npcId));
        }

        private void RebuildChoices()
        {
            _choices.Clear();

            if (_currentNode == null)
                return;

            var choices = _currentNode.Choices;
            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var lockedReason = ResolveLockedReason(choice);

                _choices.Add(new PresentedChoice(
                    index: i,
                    choiceId: choice.choiceId,
                    line: Present(choice.line, _currentNode.IsTranslationExercise),
                    isAvailable: string.IsNullOrEmpty(lockedReason),
                    lockedReasonKey: lockedReason));
            }
        }

        private string ResolveLockedReason(DialogueChoice choice)
        {
            if (choice.requiredLanguageLevel > _language.Level)
                return LockedByLanguageKey;

            if (choice.requiredTrust > _currentTrust)
                return LockedByTrustKey;

            if (!string.IsNullOrWhiteSpace(choice.requiredFlag)
                && !_currentFlags.Contains(choice.requiredFlag))
            {
                return LockedByFlagKey;
            }

            return string.Empty;
        }

        /// <summary>
        /// Раскладывает двуязычную реплику по режиму перевода. В упражнении
        /// перевод скрыт независимо от режима, но настройка доступности
        /// «полный перевод» всё равно имеет приоритет (п. 5.5 ТЗ, FR-044).
        /// </summary>
        private PresentedLine Present(BilingualLine line, bool isExercise)
        {
            var wordIds = line.wordIds ?? new List<string>();

            var mode = _language.Mode;
            var kazakhIsPrimary = mode is TranslationMode.KazakhWithRussian
                or TranslationMode.KazakhOnly;

            var primary = kazakhIsPrimary ? line.kazakh : line.russian;
            var secondary = kazakhIsPrimary ? line.russian : line.kazakh;

            if (isExercise && !_language.ForceFullTranslation)
                return new PresentedLine(primary, string.Empty, wordIds);

            if (mode == TranslationMode.InterfaceLanguageOnly)
                return new PresentedLine(line.russian, string.Empty, wordIds);

            // Перевод показывается, если его просит хотя бы одно слово реплики:
            // иначе строка с одним незнакомым словом осталась бы без подсказки.
            var showTranslation = _language.ForceFullTranslation
                                  || ShouldShowTranslationForAnyWord(wordIds);

            return new PresentedLine(
                primary,
                showTranslation ? secondary : string.Empty,
                wordIds);
        }

        private bool ShouldShowTranslationForAnyWord(IReadOnlyList<string> wordIds)
        {
            if (_language.Mode == TranslationMode.KazakhOnly)
                return false;

            if (wordIds.Count == 0)
                return true;

            for (var i = 0; i < wordIds.Count; i++)
            {
                if (_language.ShouldShowTranslation(wordIds[i]))
                    return true;
            }

            return false;
        }

        private void PublishEffects(DialogueChoice choice)
        {
            if (choice.effects == null)
                return;

            foreach (var effect in choice.effects)
            {
                if (string.IsNullOrWhiteSpace(effect.effectType))
                    continue;

                _eventBus.Publish(new DialogueEffectRequestedEvent(
                    _currentNpcId, effect.effectType, effect.targetId, effect.value));
            }
        }

        private void AwardLanguageExperience()
        {
            if (_currentNode != null && _currentNode.LanguageExperience > 0f)
                _language.AddExperience(_currentNode.LanguageExperience);
        }

        private void Reset()
        {
            _currentNode = null;
            _currentNpcId = string.Empty;
            _currentTrust = 0f;
            _currentFlags = null;
            _choices.Clear();
        }
    }
}
