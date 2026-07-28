using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QonaevLife.Player
{
    /// <summary>
    /// Назначает действие Look осям орбитальной камеры (FR-010, FR-011).
    /// Делается в рантайме, а не при сборке сцены: список осей контроллера —
    /// свойство базового класса, оно недоступно через SerializedObject и
    /// заполняется самим компонентом, когда у камеры появляется цель.
    /// Чувствительность берётся из настроек игрока (FR-093).
    /// </summary>
    [RequireComponent(typeof(CinemachineInputAxisController))]
    public sealed class CameraLookBinder : MonoBehaviour
    {
        [SerializeField] [Tooltip("Действие обзора, Vector2.")]
        private InputActionReference lookAction;

        [SerializeField] [Tooltip("Базовая чувствительность по горизонтали.")]
        private float horizontalGain = 0.6f;

        [SerializeField] [Tooltip("Базовая чувствительность по вертикали.")]
        private float verticalGain = -0.4f;

        private CinemachineInputAxisController _controller;
        private bool _applied;

        /// <summary>Множитель чувствительности из настроек.</summary>
        public float SensitivityMultiplier { get; set; } = 1f;

        private void Awake() => _controller = GetComponent<CinemachineInputAxisController>();

        private void OnEnable()
        {
            _applied = false;

            if (lookAction?.action is { enabled: false } action)
                action.Enable();
        }

        private void Update()
        {
            // Оси появляются не сразу: контроллер создаёт их, когда камера
            // получает цель слежения. Пытаемся назначить, пока не выйдет.
            if (_applied || _controller == null)
                return;

            Apply();
        }

        private void Apply()
        {
            var controllers = _controller.Controllers;
            if (controllers == null || controllers.Count == 0)
                return;

            if (lookAction == null || lookAction.action == null)
            {
                Debug.LogWarning(
                    "[Камера] Не назначено действие Look — обзор мышью не работает.");

                // Повторять попытки бессмысленно: ссылка не появится сама.
                _applied = true;
                return;
            }

            for (var i = 0; i < controllers.Count; i++)
            {
                var controller = controllers[i];
                if (controller?.Input == null)
                    continue;

                controller.Input.InputAction = lookAction;

                // Ось X вращает камеру вокруг персонажа, ось Y поднимает и
                // опускает взгляд; вертикаль инвертирована, чтобы движение
                // мыши вверх поднимало камеру.
                var isVertical = controller.Name != null
                                 && controller.Name.Contains("Vertical");

                controller.Input.Gain = (isVertical ? verticalGain : horizontalGain)
                                        * SensitivityMultiplier;
            }

            _applied = true;
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(InputActionReference look)
        {
            lookAction = look;
            _applied = false;
        }
    }
}
