using QonaevLife.Core;
using QonaevLife.Player;
using TMPro;
using UnityEngine;

namespace QonaevLife.UI
{
    /// <summary>
    /// Контекстная подсказка у объекта в фокусе (FR-012, FR-090).
    /// Показывает клавишу и действие, а для закрытой локации — причину,
    /// почему действие недоступно. Критическая информация не передаётся
    /// одним цветом: рядом с текстом всегда есть слово (п. 9 ТЗ).
    /// </summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] [Tooltip("Корень подсказки — скрывается целиком.")]
        private GameObject root;

        [SerializeField] private TMP_Text label;

        [SerializeField] [Tooltip("Цвет доступного действия.")]
        private Color availableColor = new(1f, 1f, 1f, 1f);

        [SerializeField] [Tooltip("Цвет недоступного действия.")]
        private Color unavailableColor = new(1f, 0.65f, 0.35f, 1f);

        private IEventBus _eventBus;
        private ILocalizedText _text;

        /// <summary>Подключает представление к сессии.</summary>
        public void Bind(IEventBus eventBus, ILocalizedText text)
        {
            Unbind();

            _eventBus = eventBus;
            _text = text;

            _eventBus?.Subscribe<InteractionTargetChangedEvent>(OnTargetChanged);
            Hide();
        }

        public void Unbind()
        {
            _eventBus?.Unsubscribe<InteractionTargetChangedEvent>(OnTargetChanged);
            _eventBus = null;
        }

        private void OnDestroy() => Unbind();

        private void OnTargetChanged(InteractionTargetChangedEvent changed)
        {
            var target = changed.Target;

            if (target == null)
            {
                Hide();
                return;
            }

            if (target.IsAvailable)
            {
                Show(_text.Resolve(target.PromptKey), availableColor);
                return;
            }

            // Недоступное действие всё равно показываем: игрок должен понимать,
            // что объект интерактивный, но сейчас закрыт (FR-012).
            Show(_text.Resolve(target.UnavailableReasonKey), unavailableColor);
        }

        private void Show(string message, Color color)
        {
            if (root != null)
                root.SetActive(true);

            if (label == null)
                return;

            label.text = message;
            label.color = color;
        }

        private void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(GameObject promptRoot, TMP_Text promptLabel)
        {
            root = promptRoot;
            label = promptLabel;
        }
    }
}
