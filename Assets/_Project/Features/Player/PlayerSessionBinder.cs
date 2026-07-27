using UnityEngine;

namespace QonaevLife.Player
{
    /// <summary>
    /// Соединяет объект игрока с запущенной сессией: детектор взаимодействия
    /// без шины событий молчит, поэтому кто-то должен передать ему контекст.
    /// Выделено в отдельный компонент, чтобы сборка Player не зависела от
    /// Bootstrap — связывание выполняет тот, кто владеет сессией.
    /// </summary>
    public sealed class PlayerSessionBinder : MonoBehaviour
    {
        [SerializeField] private InteractionDetector interactionDetector;
        [SerializeField] private PlayerMotor motor;

        public InteractionDetector Detector => interactionDetector;

        public PlayerMotor Motor => motor;

        private void Awake()
        {
            if (interactionDetector == null)
                interactionDetector = GetComponentInChildren<InteractionDetector>();

            if (motor == null)
                motor = GetComponent<PlayerMotor>();
        }
    }
}
