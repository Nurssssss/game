using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.Core;
using UnityEngine;

namespace QonaevLife.Npc
{
    /// <summary>
    /// Показывает на сцене только тех NPC, которых сервис отметил активными
    /// (FR-032). Объекты переиспользуются из пула: удалённые NPC скрываются,
    /// а не уничтожаются, поэтому появление рядом с игроком не вызывает
    /// рывка кадров (NFR-003).
    /// </summary>
    public sealed class NpcSpawner : MonoBehaviour
    {
        [SerializeField] [Tooltip("Префаб фигуры NPC. Если пуст — создаётся капсула.")]
        private GameObject npcPrefab;

        [SerializeField] [Tooltip("Высота фигуры над точкой локации, м.")]
        private float groundOffset = 0.9f;

        [SerializeField] [Tooltip("Скорость перехода к новой точке расписания, м/с.")]
        [Min(0.1f)]
        private float walkSpeed = 1.6f;

        private readonly Dictionary<string, NpcInstance> _instances = new();
        private readonly List<string> _idBuffer = new();

        private IEventBus _eventBus;
        private INpcService _npcService;
        private ContentDatabase _content;

        /// <summary>Объект NPC и его целевая точка.</summary>
        private sealed class NpcInstance
        {
            public GameObject Root;
            public Vector3 Target;
        }

        public void Bind(IEventBus eventBus, INpcService npcService, ContentDatabase content)
        {
            Unbind();

            _eventBus = eventBus;
            _npcService = npcService;
            _content = content;

            _eventBus.Subscribe<NpcSimulationLevelChangedEvent>(OnSimulationLevelChanged);
            _eventBus.Subscribe<NpcScheduleChangedEvent>(OnScheduleChanged);
        }

        public void Unbind()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<NpcSimulationLevelChangedEvent>(OnSimulationLevelChanged);
            _eventBus.Unsubscribe<NpcScheduleChangedEvent>(OnScheduleChanged);
            _eventBus = null;
        }

        private void OnDestroy() => Unbind();

        private void OnSimulationLevelChanged(NpcSimulationLevelChangedEvent changed)
        {
            if (changed.Level == NpcSimulationLevel.Active)
                Show(changed.NpcId, changed.WorldPosition);
            else
                Hide(changed.NpcId);
        }

        /// <summary>
        /// При смене фазы активный NPC не телепортируется, а идёт к новой
        /// точке: игрок видит, что город живёт (FR-031).
        /// </summary>
        private void OnScheduleChanged(NpcScheduleChangedEvent changed)
        {
            if (!_instances.TryGetValue(changed.NpcId, out var instance))
                return;

            if (_npcService.TryGetState(changed.NpcId, out var state))
                instance.Target = state.WorldPosition + Vector3.up * groundOffset;
        }

        private void Update()
        {
            if (_instances.Count == 0)
                return;

            var step = walkSpeed * Time.deltaTime;

            _idBuffer.Clear();
            _idBuffer.AddRange(_instances.Keys);

            foreach (var npcId in _idBuffer)
            {
                var instance = _instances[npcId];
                if (instance.Root == null)
                    continue;

                var current = instance.Root.transform.position;
                if ((instance.Target - current).sqrMagnitude < 0.01f)
                    continue;

                var next = Vector3.MoveTowards(current, instance.Target, step);
                instance.Root.transform.position = next;

                // Разворот в сторону движения: фигура не должна ехать боком.
                var direction = instance.Target - current;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.001f)
                {
                    instance.Root.transform.rotation = Quaternion.RotateTowards(
                        instance.Root.transform.rotation,
                        Quaternion.LookRotation(direction, Vector3.up),
                        360f * Time.deltaTime);
                }
            }
        }

        private void Show(string npcId, Vector3 worldPosition)
        {
            var position = worldPosition + Vector3.up * groundOffset;

            if (_instances.TryGetValue(npcId, out var existing))
            {
                existing.Root.SetActive(true);
                existing.Target = position;
                return;
            }

            var root = CreateFigure(npcId);
            root.transform.position = position;

            _instances[npcId] = new NpcInstance { Root = root, Target = position };
        }

        private void Hide(string npcId)
        {
            if (_instances.TryGetValue(npcId, out var instance) && instance.Root != null)
                instance.Root.SetActive(false);
        }

        /// <summary>
        /// Фигура NPC. Без префаба создаётся цветная капсула: прототип должен
        /// работать до появления моделей персонажей (п. 13 ТЗ, этап P1).
        /// </summary>
        private GameObject CreateFigure(string npcId)
        {
            if (npcPrefab != null)
            {
                var instance = Instantiate(npcPrefab, transform);
                instance.name = $"Npc_{npcId}";
                return instance;
            }

            var root = new GameObject($"Npc_{npcId}");
            root.transform.SetParent(transform, worldPositionStays: false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, worldPositionStays: false);
            body.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);

            // Коллайдер капсулы мешал бы игроку пройти вплотную; взаимодействие
            // с NPC идёт через точки интереса, а не через саму фигуру.
            Destroy(body.GetComponent<Collider>());

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Facing";
            marker.transform.SetParent(root.transform, worldPositionStays: false);
            marker.transform.localPosition = new Vector3(0f, 0.45f, 0.3f);
            marker.transform.localScale = new Vector3(0.14f, 0.14f, 0.22f);
            Destroy(marker.GetComponent<Collider>());

            ApplyColor(body, marker, npcId);

            return root;
        }

        /// <summary>
        /// Цвет выводится из идентификатора: одинаковый NPC всегда одного
        /// цвета, а разные различимы без моделей.
        /// </summary>
        private void ApplyColor(GameObject body, GameObject marker, string npcId)
        {
            var hue = Mathf.Abs(npcId.GetHashCode() % 360) / 360f;
            var bodyColor = Color.HSVToRGB(hue, 0.55f, 0.85f);

            var renderer = body.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = bodyColor;

            var markerRenderer = marker.GetComponent<Renderer>();
            if (markerRenderer != null)
                markerRenderer.material.color = new Color(0.95f, 0.9f, 0.4f);
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(GameObject prefab) => npcPrefab = prefab;
    }
}
