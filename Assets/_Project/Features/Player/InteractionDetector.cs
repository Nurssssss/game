using QonaevLife.Core;
using UnityEngine;

namespace QonaevLife.Player
{
    /// <summary>
    /// Ищет ближайший доступный объект перед игроком и публикует смену цели,
    /// чтобы UI показывал контекстную подсказку (FR-012).
    /// Опрашивается с фиксированным интервалом, а не каждый кадр: подсказка
    /// не требует кадровой точности, а физический запрос стоит дорого.
    /// </summary>
    public sealed class InteractionDetector : MonoBehaviour
    {
        [Header("Зона поиска")]
        [SerializeField] [Min(0.1f)] [Tooltip("Радиус поиска интерактивных объектов, м.")]
        private float searchRadius = 2.5f;

        [SerializeField] [Tooltip("Слои, на которых лежат интерактивные объекты.")]
        private LayerMask interactableLayers = ~0;

        [SerializeField]
        [Tooltip("Максимальный угол между взглядом персонажа и объектом, градусов.")]
        [Range(15f, 180f)]
        private float maxAngle = 120f;

        [Header("Частота опроса")]
        [SerializeField] [Min(0.02f)] [Tooltip("Интервал между проверками, с.")]
        private float scanInterval = 0.1f;

        private readonly Collider[] _hits = new Collider[16];
        private IEventBus _eventBus;
        private IInteractionContext _context;
        private float _nextScanTime;

        /// <summary>Объект в фокусе или null.</summary>
        public IInteractable Current { get; private set; }

        /// <summary>
        /// Подключает детектор к сессии. Без вызова детектор молчит,
        /// поэтому сцену можно открыть без запущенной сессии.
        /// </summary>
        public void Bind(IEventBus eventBus, IInteractionContext context)
        {
            _eventBus = eventBus;
            _context = context;
        }

        private void Update()
        {
            if (_eventBus == null || Time.time < _nextScanTime)
                return;

            _nextScanTime = Time.time + scanInterval;
            UpdateTarget(FindBestTarget());
        }

        /// <summary>Выполняет действие с объектом в фокусе (FR-012).</summary>
        public bool TryInteract()
        {
            if (Current == null || !Current.IsAvailable || _context == null)
                return false;

            Current.Interact(_context);
            return true;
        }

        private IInteractable FindBestTarget()
        {
            var origin = transform.position;
            var count = Physics.OverlapSphereNonAlloc(
                origin, searchRadius, _hits, interactableLayers, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            var bestScore = float.MaxValue;
            var forward = transform.forward;
            var cosLimit = Mathf.Cos(maxAngle * 0.5f * Mathf.Deg2Rad);

            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit == null)
                    continue;

                // GetComponentInParent: коллайдер часто висит на дочернем объекте.
                var candidate = hit.GetComponentInParent<IInteractable>();
                if (candidate == null)
                    continue;

                var toTarget = hit.bounds.center - origin;
                toTarget.y = 0f;

                var distance = toTarget.magnitude;
                if (distance < 0.001f)
                {
                    // Игрок стоит внутри объекта — угол посчитать нельзя, берём как есть.
                    return candidate;
                }

                if (Vector3.Dot(forward, toTarget / distance) < cosLimit)
                    continue;

                // Доступные объекты всегда важнее недоступных при равном расстоянии.
                var score = candidate.IsAvailable ? distance : distance + searchRadius;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private void UpdateTarget(IInteractable target)
        {
            if (ReferenceEquals(target, Current))
                return;

            Current = target;
            _eventBus.Publish(new InteractionTargetChangedEvent(target));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, searchRadius);
        }
    }
}
