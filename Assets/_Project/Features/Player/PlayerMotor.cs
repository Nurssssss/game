using UnityEngine;

namespace QonaevLife.Player
{
    /// <summary>
    /// Перемещение персонажа от третьего лица (FR-010): ходьба, бег, поворот
    /// в направлении движения и гравитация. Прыжок в MVP не используется —
    /// он добавляется только при необходимости по геймдизайну.
    /// Логика движения отделена от чтения ввода, поэтому её можно
    /// проверять и переиспользовать в катсценах и отладке.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Скорости")]
        [SerializeField] [Min(0f)] [Tooltip("Скорость ходьбы, м/с.")]
        private float walkSpeed = 2.4f;

        [SerializeField] [Min(0f)] [Tooltip("Скорость бега, м/с.")]
        private float sprintSpeed = 5.2f;

        [SerializeField] [Min(0f)] [Tooltip("Насколько быстро набирается и гасится скорость.")]
        private float acceleration = 12f;

        [Header("Поворот")]
        [SerializeField] [Min(0f)] [Tooltip("Скорость разворота персонажа, градусов/с.")]
        private float turnSpeed = 720f;

        [Header("Гравитация")]
        [SerializeField] [Tooltip("Ускорение свободного падения, м/с².")]
        private float gravity = -19.6f;

        [SerializeField] [Min(0f)]
        [Tooltip("Прижимающая скорость на земле — не даёт персонажу отрываться на склонах.")]
        private float groundedStickSpeed = 2f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        /// <summary>Текущая горизонтальная скорость — используется анимацией и UI.</summary>
        public float CurrentSpeed => _horizontalVelocity.magnitude;

        /// <summary>Доля от максимальной скорости в диапазоне [0, 1] для Animator.</summary>
        public float NormalizedSpeed => sprintSpeed > 0f
            ? Mathf.Clamp01(CurrentSpeed / sprintSpeed)
            : 0f;

        public bool IsGrounded => _controller != null && _controller.isGrounded;

        /// <summary>Заблокировано ли управление: диалог, магазин, катсцена.</summary>
        public bool IsMovementLocked { get; set; }

        private void Awake() => _controller = GetComponent<CharacterController>();

        /// <summary>
        /// Двигает персонажа. <paramref name="worldDirection"/> — желаемое
        /// направление в мировых координатах; длина больше единицы игнорируется.
        /// </summary>
        public void Move(Vector3 worldDirection, bool isSprinting, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            var direction = worldDirection;
            direction.y = 0f;

            if (IsMovementLocked)
                direction = Vector3.zero;

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            var targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            var targetVelocity = direction * targetSpeed;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, acceleration * deltaTime);

            ApplyGravity(deltaTime);
            RotateTowards(direction, deltaTime);

            var motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * deltaTime);
        }

        /// <summary>
        /// Мгновенно перемещает персонажа: загрузка сохранения, вход в интерьер,
        /// поездка на такси. Сбрасывает скорость, чтобы не было рывка.
        /// </summary>
        public void Teleport(Vector3 position, float yaw)
        {
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;

            // CharacterController нужно отключить: иначе он вернёт персонажа назад.
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _controller.enabled = true;
        }

        private void ApplyGravity(float deltaTime)
        {
            if (_controller.isGrounded)
            {
                // Небольшая прижимающая скорость надёжнее нуля: с нулём контроллер
                // теряет контакт с землёй на спусках и начинает мелко подпрыгивать.
                _verticalVelocity = -groundedStickSpeed;
                return;
            }

            _verticalVelocity += gravity * deltaTime;
        }

        private void RotateTowards(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeed * deltaTime);
        }
    }
}
