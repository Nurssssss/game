using QonaevLife.Core;
using QonaevLife.Dialogue;
using QonaevLife.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QonaevLife.UI
{
    /// <summary>
    /// Блокирует управление персонажем, пока открыт диалог, и позволяет
    /// закрыть его и переключить режим перевода клавишами (FR-041, п. 9 ТЗ).
    /// Живёт отдельно от <see cref="DialogueView"/>: представление рисует,
    /// а этот компонент управляет вводом.
    /// </summary>
    public sealed class DialogueInputGate : MonoBehaviour
    {
        [SerializeField] [Tooltip("Ввод персонажа, который блокируется в диалоге.")]
        private PlayerInputBridge playerInput;

        [SerializeField] private DialogueView dialogueView;

        [SerializeField] [Tooltip("Клавиша переключения режима перевода.")]
        private Key translationModeKey = Key.T;

        [SerializeField] [Tooltip("Клавиша закрытия диалога.")]
        private Key closeKey = Key.Escape;

        private IEventBus _eventBus;

        public void Bind(IEventBus eventBus, PlayerInputBridge input, DialogueView view)
        {
            Unbind();

            _eventBus = eventBus;
            playerInput = input;
            dialogueView = view;

            _eventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            _eventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        public void Unbind()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            _eventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
            _eventBus = null;
        }

        private void OnDestroy() => Unbind();

        private void OnDialogueStarted(DialogueStartedEvent started) => SetInputBlocked(true);

        private void OnDialogueEnded(DialogueEndedEvent ended) => SetInputBlocked(false);

        private void SetInputBlocked(bool blocked)
        {
            if (playerInput != null)
                playerInput.IsInputBlocked = blocked;
        }

        private void Update()
        {
            if (dialogueView == null || !dialogueView.IsOpen)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            // Цифры 1–4 выбирают вариант ответа: в диалоге это быстрее мыши.
            for (var i = 0; i < 4; i++)
            {
                var key = Key.Digit1 + i;
                if (keyboard[key].wasPressedThisFrame)
                {
                    dialogueView.SelectChoiceByIndex(i);
                    return;
                }
            }

            if (keyboard[translationModeKey].wasPressedThisFrame)
            {
                dialogueView.CycleTranslationMode();
                return;
            }

            if (keyboard[closeKey].wasPressedThisFrame)
                dialogueView.CloseByPlayer();
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(PlayerInputBridge input, DialogueView view)
        {
            playerInput = input;
            dialogueView = view;
        }
    }
}
