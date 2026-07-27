using UnityEngine;
using UnityEngine.InputSystem;

namespace QonaevLife.Player
{
    /// <summary>
    /// Читает действия Input System и передаёт их мотору и детектору (FR-010).
    /// Использует action-based workflow, поэтому переназначение клавиш работает
    /// без правки кода (FR-013). Направление движения пересчитывается из
    /// экранных осей в мировые относительно камеры.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerInputBridge : MonoBehaviour
    {
        [Header("Действия")]
        [SerializeField] [Tooltip("Действие движения, Vector2.")]
        private InputActionReference moveAction;

        [SerializeField] [Tooltip("Действие бега, кнопка.")]
        private InputActionReference sprintAction;

        [SerializeField] [Tooltip("Действие взаимодействия, кнопка.")]
        private InputActionReference interactAction;

        [Header("Ссылки")]
        [SerializeField] [Tooltip("Камера, относительно которой считается движение.")]
        private Transform cameraTransform;

        [SerializeField] private InteractionDetector interactionDetector;

        private PlayerMotor _motor;

        /// <summary>Заблокирован ли ввод: открыт диалог, магазин или меню.</summary>
        public bool IsInputBlocked { get; set; }

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            EnableAction(moveAction);
            EnableAction(sprintAction);
            EnableAction(interactAction);

            if (interactAction?.action != null)
                interactAction.action.performed += OnInteractPerformed;
        }

        private void OnDisable()
        {
            if (interactAction?.action != null)
                interactAction.action.performed -= OnInteractPerformed;
        }

        private void Update()
        {
            var input = IsInputBlocked ? Vector2.zero : ReadMoveInput();
            var isSprinting = !IsInputBlocked && ReadSprintInput();

            _motor.Move(ToWorldDirection(input), isSprinting, Time.deltaTime);
        }

        private Vector2 ReadMoveInput()
            => moveAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;

        private bool ReadSprintInput()
            => sprintAction?.action?.IsPressed() ?? false;

        /// <summary>
        /// Переводит экранный ввод в мировое направление относительно камеры,
        /// чтобы «вперёд» всегда означало «вперёд по экрану».
        /// </summary>
        private Vector3 ToWorldDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            if (cameraTransform == null)
                return new Vector3(input.x, 0f, input.y);

            var forward = cameraTransform.forward;
            var right = cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;

            // Камера смотрит строго вниз — горизонтальной проекции нет,
            // берём ввод как есть, иначе персонаж перестанет двигаться.
            if (forward.sqrMagnitude < 0.0001f)
                return new Vector3(input.x, 0f, input.y);

            forward.Normalize();
            right.Normalize();

            return forward * input.y + right * input.x;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (IsInputBlocked || interactionDetector == null)
                return;

            interactionDetector.TryInteract();
        }

        private static void EnableAction(InputActionReference reference)
        {
            if (reference?.action is { enabled: false } action)
                action.Enable();
        }
    }
}
