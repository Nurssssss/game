using System.Collections.Generic;
using QonaevLife.Core;
using QonaevLife.Dialogue;
using QonaevLife.Language;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.UI
{
    /// <summary>
    /// Окно диалога (FR-033, FR-040 — FR-042, AT-003). Показывает реплику
    /// говорящего, перевод по текущему режиму, варианты ответа с причиной
    /// недоступности и кнопки словарных слов.
    /// Не решает игровую логику: только отображает состояние
    /// <see cref="DialogueService"/> и передаёт ему выбор игрока.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        [Header("Корень")]
        [SerializeField] [Tooltip("Скрывается целиком, когда диалог не активен.")]
        private GameObject root;

        [Header("Реплика")]
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private TMP_Text translationLabel;

        [Header("Варианты ответа")]
        [SerializeField] [Tooltip("Контейнер, куда складываются кнопки вариантов.")]
        private Transform choiceContainer;

        [SerializeField] [Tooltip("Шаблон кнопки варианта. Отключён на сцене.")]
        private Button choiceTemplate;

        [Header("Словарь и режим")]
        [SerializeField] private Transform wordContainer;
        [SerializeField] private Button wordTemplate;
        [SerializeField] private TMP_Text modeLabel;

        private readonly List<Button> _choiceButtons = new();
        private readonly List<Button> _wordButtons = new();

        private IEventBus _eventBus;
        private DialogueService _dialogue;
        private ILanguageProgressService _language;
        private ILocalizedText _text;

        /// <summary>Открыт ли диалог — ввод игрока в это время блокируется.</summary>
        public bool IsOpen => root != null && root.activeSelf;

        public void Bind(IEventBus eventBus, DialogueService dialogue,
            ILanguageProgressService language, ILocalizedText text)
        {
            Unbind();

            _eventBus = eventBus;
            _dialogue = dialogue;
            _language = language;
            _text = text;

            _eventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            _eventBus.Subscribe<DialogueNodeChangedEvent>(OnNodeChanged);
            _eventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
            _eventBus.Subscribe<TranslationModeChangedEvent>(OnTranslationModeChanged);

            Close();
        }

        public void Unbind()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            _eventBus.Unsubscribe<DialogueNodeChangedEvent>(OnNodeChanged);
            _eventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
            _eventBus.Unsubscribe<TranslationModeChangedEvent>(OnTranslationModeChanged);
            _eventBus = null;
        }

        private void OnDestroy() => Unbind();

        private void OnDialogueStarted(DialogueStartedEvent started)
        {
            if (root != null)
                root.SetActive(true);

            if (speakerLabel != null)
                speakerLabel.text = _text.Resolve($"npc.{TrimNpcPrefix(started.NpcId)}");

            Refresh();
        }

        private void OnNodeChanged(DialogueNodeChangedEvent changed) => Refresh();

        private void OnDialogueEnded(DialogueEndedEvent ended) => Close();

        /// <summary>
        /// Смена режима перевода перерисовывает окно, но не трогает состояние
        /// диалога: игрок остаётся на том же узле (FR-041).
        /// </summary>
        private void OnTranslationModeChanged(TranslationModeChangedEvent changed)
        {
            if (IsOpen)
                Refresh();
        }

        /// <summary>Переключает режим перевода по кругу (FR-041).</summary>
        public void CycleTranslationMode()
        {
            if (_language == null)
                return;

            var next = _language.Mode switch
            {
                TranslationMode.RussianWithKazakh => TranslationMode.KazakhWithRussian,
                TranslationMode.KazakhWithRussian => TranslationMode.KazakhOnly,
                TranslationMode.KazakhOnly => TranslationMode.RussianWithKazakh,
                _ => TranslationMode.RussianWithKazakh
            };

            _language.SetMode(next);
        }

        private void Refresh()
        {
            if (_dialogue == null || !_dialogue.IsActive)
            {
                Close();
                return;
            }

            var line = _dialogue.GetCurrentLine();

            if (primaryLabel != null)
                primaryLabel.text = line.Primary;

            if (translationLabel != null)
            {
                translationLabel.text = line.Translation;
                translationLabel.gameObject.SetActive(line.HasTranslation);
            }

            if (modeLabel != null)
                modeLabel.text = _text.Resolve($"mode.{_language.Mode}");

            RebuildChoices();
            RebuildWords(line.WordIds);
        }

        private void RebuildChoices()
        {
            EnsureButtonCount(_choiceButtons, choiceTemplate, choiceContainer,
                _dialogue.Choices.Count);

            for (var i = 0; i < _choiceButtons.Count; i++)
            {
                var button = _choiceButtons[i];

                if (i >= _dialogue.Choices.Count)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                var choice = _dialogue.Choices[i];
                button.gameObject.SetActive(true);

                // Недоступный вариант остаётся видимым: игрок должен понимать,
                // что реплика существует и чего не хватает (FR-046).
                button.interactable = choice.IsAvailable;

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = choice.IsAvailable
                        ? $"{i + 1}. {choice.Line.Primary}"
                        : $"{i + 1}. {choice.Line.Primary}  — " +
                          $"{_text.Resolve(choice.LockedReasonKey)}";

                    label.color = choice.IsAvailable
                        ? Color.white
                        : new Color(0.65f, 0.65f, 0.65f);
                }

                var index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectChoice(index));
            }
        }

        private void RebuildWords(IReadOnlyList<string> wordIds)
        {
            var count = wordIds?.Count ?? 0;
            EnsureButtonCount(_wordButtons, wordTemplate, wordContainer, count);

            for (var i = 0; i < _wordButtons.Count; i++)
            {
                var button = _wordButtons[i];

                if (i >= count)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                var wordId = wordIds[i];
                button.gameObject.SetActive(true);

                // Уже добавленное слово помечается, повторно добавлять нечего.
                var alreadyKnown = _language.TryGetWord(wordId, out _);
                button.interactable = !alreadyKnown;

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    var wordText = _text.Resolve($"word.{TrimWordPrefix(wordId)}");
                    if (wordText.StartsWith('#'))
                        wordText = TrimWordPrefix(wordId);

                    label.text = alreadyKnown ? $"✓ {wordText}" : $"+ {wordText}";
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => AddWord(wordId));
            }
        }

        /// <summary>
        /// Выбор варианта по номеру — для клавиш 1–4. Недоступный или
        /// несуществующий вариант игнорируется без ошибки.
        /// </summary>
        public void SelectChoiceByIndex(int index)
        {
            if (_dialogue == null || index < 0 || index >= _dialogue.Choices.Count)
                return;

            if (!_dialogue.Choices[index].IsAvailable)
                return;

            SelectChoice(index);
        }

        private void SelectChoice(int index)
        {
            if (_dialogue == null)
                return;

            if (!_dialogue.TrySelectChoice(index))
                return;

            // Терминальный узел завершает диалог сам и присылает событие;
            // иначе перерисовываем текущее состояние.
            if (_dialogue.IsActive)
                Refresh();
        }

        private void AddWord(string wordId)
        {
            if (_dialogue != null && _dialogue.TryAddWordToDictionary(wordId))
                Refresh();
        }

        /// <summary>Закрывает окно по кнопке игрока.</summary>
        public void CloseByPlayer() => _dialogue?.End();

        private void Close()
        {
            if (root != null)
                root.SetActive(false);
        }

        /// <summary>
        /// Держит нужное число кнопок: лишние скрываются, недостающие
        /// создаются из шаблона. Пересоздавать список целиком не нужно.
        /// </summary>
        private static void EnsureButtonCount(List<Button> buttons, Button template,
            Transform container, int required)
        {
            if (template == null || container == null)
                return;

            while (buttons.Count < required)
            {
                var instance = Instantiate(template, container);
                instance.gameObject.SetActive(true);
                buttons.Add(instance);
            }
        }

        private static string TrimNpcPrefix(string npcId)
            => npcId != null && npcId.StartsWith("npc_") ? npcId[4..] : npcId;

        private static string TrimWordPrefix(string wordId)
            => wordId != null && wordId.StartsWith("word_") ? wordId[5..] : wordId;

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(GameObject windowRoot, TMP_Text speaker, TMP_Text primary,
            TMP_Text translation, Transform choices, Button choiceButtonTemplate,
            Transform words, Button wordButtonTemplate, TMP_Text mode)
        {
            root = windowRoot;
            speakerLabel = speaker;
            primaryLabel = primary;
            translationLabel = translation;
            choiceContainer = choices;
            choiceTemplate = choiceButtonTemplate;
            wordContainer = words;
            wordTemplate = wordButtonTemplate;
            modeLabel = mode;
        }
    }
}
