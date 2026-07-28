using System.Collections.Generic;
using QonaevLife.Core;
using QonaevLife.Language;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.UI
{
    /// <summary>
    /// Экран мини-урока (FR-043). Показывает задание, варианты ответа и
    /// результат. После ошибки подсвечивает правильный ответ, как требует ТЗ:
    /// «урок завершается результатом, начисляет опыт языка и показывает
    /// корректный ответ».
    /// </summary>
    public sealed class LessonView : ScreenView
    {
        [Header("Задание")]
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private TMP_Text feedbackLabel;

        [Header("Варианты")]
        [SerializeField] private Transform optionContainer;
        [SerializeField] [Tooltip("Шаблон кнопки варианта. Отключён на сцене.")]
        private Button optionTemplate;

        [Header("Прочее")]
        [SerializeField] private Button closeButton;

        [SerializeField] [Tooltip("Сколько секунд показывать разбор ответа.")]
        [Min(0.3f)]
        private float feedbackDuration = 1.4f;

        private readonly List<Button> _optionButtons = new();

        private IEventBus _eventBus;
        private LessonService _lessons;
        private float _advanceTime;
        private bool _awaitingAdvance;

        public override UiScreen Screen => UiScreen.Lesson;

        public void BindLesson(IEventBus eventBus, LessonService lessons)
        {
            _eventBus = eventBus;
            _lessons = lessons;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseLesson);
            }

            _eventBus.Subscribe<LessonFinishedEvent>(OnLessonFinished);
        }

        protected override void OnUnbound()
        {
            _eventBus?.Unsubscribe<LessonFinishedEvent>(OnLessonFinished);
            _eventBus = null;
        }

        protected override void OnShown()
        {
            _awaitingAdvance = false;
            ShowCurrentTask();
        }

        private void Update()
        {
            // Пауза после ответа: игрок должен увидеть разбор, прежде чем
            // появится следующее задание.
            if (!_awaitingAdvance || Time.time < _advanceTime)
                return;

            _awaitingAdvance = false;
            ShowCurrentTask();
        }

        private void ShowCurrentTask()
        {
            if (_lessons == null || Text == null)
                return;

            if (!_lessons.IsActive)
            {
                // Урок закончился — экран закрывается событием, здесь только
                // прячем варианты, чтобы не осталось кликабельных кнопок.
                HideOptions();
                return;
            }

            var task = _lessons.CurrentTask;
            if (task == null)
                return;

            if (progressLabel != null)
            {
                progressLabel.text =
                    $"{_lessons.CurrentIndex + 1} / {_lessons.TaskCount}";
            }

            if (kindLabel != null)
                kindLabel.text = Text.Resolve($"lesson.kind.{task.Value.Kind}");

            if (promptLabel != null)
                promptLabel.text = task.Value.Prompt;

            if (feedbackLabel != null)
                feedbackLabel.text = string.Empty;

            BuildOptions(task.Value);
        }

        private void BuildOptions(LessonTask task)
        {
            if (optionTemplate == null || optionContainer == null)
                return;

            while (_optionButtons.Count < task.Options.Count)
            {
                var instance = Instantiate(optionTemplate, optionContainer);
                instance.gameObject.SetActive(true);
                _optionButtons.Add(instance);
            }

            for (var i = 0; i < _optionButtons.Count; i++)
            {
                var button = _optionButtons[i];

                if (i >= task.Options.Count)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                button.gameObject.SetActive(true);
                button.interactable = true;

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = $"{i + 1}. {task.Options[i]}";
                    label.color = Color.white;
                }

                var index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Answer(index));
            }
        }

        /// <summary>Отвечает на текущее задание. Публичный — для клавиш 1–4.</summary>
        public void Answer(int optionIndex)
        {
            if (_lessons == null || !_lessons.IsActive || _awaitingAdvance)
                return;

            var task = _lessons.CurrentTask;
            if (task == null)
                return;

            var correctIndex = task.Value.CorrectIndex;
            var isCorrect = _lessons.Answer(optionIndex);

            ShowFeedback(isCorrect, correctIndex, optionIndex);

            _awaitingAdvance = true;
            _advanceTime = Time.time + feedbackDuration;
        }

        /// <summary>
        /// Разбор ответа. Верный и неверный различаются символом и текстом, а
        /// не только цветом (п. 9 ТЗ).
        /// </summary>
        private void ShowFeedback(bool isCorrect, int correctIndex, int chosenIndex)
        {
            foreach (var button in _optionButtons)
                button.interactable = false;

            if (correctIndex >= 0 && correctIndex < _optionButtons.Count)
            {
                var label = _optionButtons[correctIndex].GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = $"✓ {label.text}";
                    label.color = new Color(0.5f, 0.9f, 0.55f);
                }
            }

            if (!isCorrect && chosenIndex >= 0 && chosenIndex < _optionButtons.Count)
            {
                var label = _optionButtons[chosenIndex].GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = $"× {label.text}";
                    label.color = new Color(1f, 0.55f, 0.5f);
                }
            }

            if (feedbackLabel != null)
            {
                feedbackLabel.text = Text.Resolve(
                    isCorrect ? "lesson.correct" : "lesson.wrong");

                feedbackLabel.color = isCorrect
                    ? new Color(0.5f, 0.9f, 0.55f)
                    : new Color(1f, 0.7f, 0.5f);
            }
        }

        private void OnLessonFinished(LessonFinishedEvent finished)
        {
            HideOptions();

            if (promptLabel != null)
            {
                promptLabel.text =
                    $"{Text.Resolve("lesson.result")}: " +
                    $"{finished.Result.CorrectAnswers} / {finished.Result.TotalTasks}";
            }

            if (kindLabel != null)
                kindLabel.text = Text.Resolve("lesson.finished");

            if (feedbackLabel != null)
            {
                var experience = (int)finished.Result.ExperienceGained;
                feedbackLabel.text = finished.Result.IsPerfect
                    ? $"{Text.Resolve("lesson.perfect")}  +{experience}"
                    : $"{Text.Resolve("lesson.experience")}: +{experience}";

                feedbackLabel.color = new Color(0.8f, 0.9f, 1f);
            }

            if (progressLabel != null)
                progressLabel.text = string.Empty;
        }

        private void HideOptions()
        {
            foreach (var button in _optionButtons)
                button.gameObject.SetActive(false);
        }

        private void CloseLesson()
        {
            _lessons?.Abandon();
            Router?.Pop();
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(TMP_Text progress, TMP_Text kind, TMP_Text prompt,
            TMP_Text feedback, Transform options, Button optionButtonTemplate, Button close)
        {
            progressLabel = progress;
            kindLabel = kind;
            promptLabel = prompt;
            feedbackLabel = feedback;
            optionContainer = options;
            optionTemplate = optionButtonTemplate;
            closeButton = close;
        }
    }
}
