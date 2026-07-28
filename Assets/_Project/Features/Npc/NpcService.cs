using System;
using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.World;
using UnityEngine;

namespace QonaevLife.Npc
{
    /// <summary>Настройки симуляции NPC (п. 8.4 ТЗ — плотность управляется качеством).</summary>
    [Serializable]
    public struct NpcSimulationSettings
    {
        [Tooltip("Радиус полной симуляции вокруг игрока, м.")]
        public float activeRadius;

        [Tooltip("Гистерезис: насколько дальше нужно уйти, чтобы NPC выключился.")]
        public float deactivationMargin;

        [Tooltip("Максимум одновременно активных NPC.")]
        public int maxActiveNpcs;

        public static NpcSimulationSettings Default => new()
        {
            activeRadius = 45f,
            deactivationMargin = 8f,
            maxActiveNpcs = 12
        };

        public bool IsValid()
            => activeRadius > 0f && deactivationMargin >= 0f && maxActiveNpcs > 0;
    }

    /// <summary>
    /// Именные NPC района (FR-030 — FR-032). Расписание определяет, где NPC
    /// находится в текущую фазу суток; вблизи игрока NPC симулируется
    /// полностью, вдали — только записью состояния, но фаза не теряется.
    /// Логика не зависит от Unity-плеера, поэтому покрывается тестами.
    /// </summary>
    public sealed class NpcService : INpcService, IGameService
    {
        private readonly ContentDatabase _content;
        private readonly IEventBus _eventBus;
        private readonly IGameClock _clock;
        private readonly LocationRegistry _locations;
        private readonly NpcSimulationSettings _settings;

        private readonly Dictionary<string, NpcState> _states = new();
        private readonly Dictionary<string, List<string>> _byLocation = new();
        private readonly List<string> _emptyList = new();
        private readonly List<(string NpcId, float Distance)> _candidates = new();

        // Переиспользуемые буферы: Update вызывается каждый кадр, и создание
        // коллекций здесь давало бы постоянный мусор для сборщика.
        private readonly HashSet<string> _shouldBeActive = new();
        private readonly List<string> _idBuffer = new();
        private readonly List<NpcScheduleChangedEvent> _changeBuffer = new();

        private DayPhase _lastPhase;
        private bool _initialized;

        public NpcService(ContentDatabase content, IEventBus eventBus, IGameClock clock,
            LocationRegistry locations, NpcSimulationSettings settings)
        {
            if (!settings.IsValid())
                throw new ArgumentException("Некорректные настройки симуляции NPC.",
                    nameof(settings));

            _content = content ?? throw new ArgumentNullException(nameof(content));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
            _settings = settings;
        }

        public IReadOnlyCollection<string> NpcIds => _states.Keys;

        /// <summary>Сколько NPC симулируется полностью — для профилирования.</summary>
        public int ActiveCount { get; private set; }

        public void Initialize()
        {
            _lastPhase = _clock.Phase;

            foreach (var npc in _content.Npcs)
            {
                if (npc == null || string.IsNullOrWhiteSpace(npc.Id))
                    continue;

                var entry = ResolveScheduleEntry(npc, _lastPhase);
                var locationId = entry?.locationId ?? npc.HomeLocationId;

                _states[npc.Id] = new NpcState(
                    npc.Id, locationId, entry?.entryId ?? string.Empty,
                    NpcSimulationLevel.Distant, ResolveWorldPosition(locationId));
            }

            RebuildLocationIndex();
            _initialized = true;
        }

        public void Shutdown()
        {
            _states.Clear();
            _byLocation.Clear();
            _initialized = false;
        }

        public bool TryGetState(string npcId, out NpcState state)
        {
            if (!string.IsNullOrWhiteSpace(npcId))
                return _states.TryGetValue(npcId, out state);

            state = default;
            return false;
        }

        public IReadOnlyList<string> GetNpcsAt(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
                return _emptyList;

            return _byLocation.TryGetValue(locationId, out var list) ? list : _emptyList;
        }

        public void Update(Vector3 playerPosition)
        {
            if (!_initialized)
                return;

            // Расписания пересчитываются только при смене фазы: делать это
            // каждый кадр незачем, а событий смены фазы всего четыре в сутки.
            if (_clock.Phase != _lastPhase)
            {
                _lastPhase = _clock.Phase;
                ApplySchedules();
            }

            UpdateSimulationLevels(playerPosition);
        }

        /// <summary>Переводит NPC на места, положенные им в новую фазу суток.</summary>
        private void ApplySchedules()
        {
            _changeBuffer.Clear();

            foreach (var npc in _content.Npcs)
            {
                if (npc == null || !_states.TryGetValue(npc.Id, out var state))
                    continue;

                var entry = ResolveScheduleEntry(npc, _lastPhase);
                var targetLocation = entry?.locationId ?? npc.HomeLocationId;

                if (string.Equals(state.CurrentLocationId, targetLocation, StringComparison.Ordinal))
                    continue;

                _states[npc.Id] = new NpcState(
                    npc.Id, targetLocation, entry?.entryId ?? string.Empty,
                    state.Level, ResolveWorldPosition(targetLocation));

                _changeBuffer.Add(new NpcScheduleChangedEvent(
                    npc.Id, state.CurrentLocationId, targetLocation,
                    entry?.entryId ?? string.Empty));
            }

            if (_changeBuffer.Count == 0)
                return;

            RebuildLocationIndex();

            // События рассылаются после перестройки индекса: подписчик,
            // спросивший GetNpcsAt, должен получить уже новое состояние.
            foreach (var change in _changeBuffer)
                _eventBus.Publish(change);
        }

        /// <summary>
        /// Выбирает, кто симулируется полностью. Ближайшие NPC получают
        /// приоритет, а число активных ограничено бюджетом (FR-032).
        /// </summary>
        private void UpdateSimulationLevels(Vector3 playerPosition)
        {
            _candidates.Clear();

            foreach (var pair in _states)
            {
                var distance = Vector3.Distance(playerPosition, pair.Value.WorldPosition);

                // Гистерезис: уже активный NPC выключается только за границей
                // радиуса с запасом, иначе на границе он мерцал бы каждый кадр.
                var threshold = pair.Value.Level == NpcSimulationLevel.Active
                    ? _settings.activeRadius + _settings.deactivationMargin
                    : _settings.activeRadius;

                if (distance <= threshold)
                    _candidates.Add((pair.Key, distance));
            }

            _candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            var activeLimit = Math.Min(_candidates.Count, _settings.maxActiveNpcs);

            _shouldBeActive.Clear();
            for (var i = 0; i < activeLimit; i++)
                _shouldBeActive.Add(_candidates[i].NpcId);

            ActiveCount = _shouldBeActive.Count;

            // Копия ключей: значения меняются внутри обхода.
            _idBuffer.Clear();
            _idBuffer.AddRange(_states.Keys);

            foreach (var npcId in _idBuffer)
            {
                var state = _states[npcId];
                var target = _shouldBeActive.Contains(npcId)
                    ? NpcSimulationLevel.Active
                    : NpcSimulationLevel.Distant;

                if (state.Level == target)
                    continue;

                _states[npcId] = new NpcState(
                    state.NpcId, state.CurrentLocationId, state.ScheduleEntryId,
                    target, state.WorldPosition);

                _eventBus.Publish(new NpcSimulationLevelChangedEvent(
                    npcId, target, state.WorldPosition));
            }
        }

        /// <summary>Запись расписания для фазы. Приоритет разрешает конфликты.</summary>
        private static ScheduleEntry? ResolveScheduleEntry(NpcDefinition npc, DayPhase phase)
        {
            var phaseName = phase.ToString();
            ScheduleEntry? best = null;

            foreach (var entry in npc.Schedule)
            {
                if (!string.Equals(entry.dayPhase, phaseName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (best == null || entry.priority > best.Value.priority)
                    best = entry;
            }

            return best;
        }

        private Vector3 ResolveWorldPosition(string locationId)
            => _locations.TryGet(locationId, out var definition)
                ? definition.MarkerPosition
                : Vector3.zero;

        private void RebuildLocationIndex()
        {
            foreach (var list in _byLocation.Values)
                list.Clear();

            foreach (var state in _states.Values)
            {
                if (string.IsNullOrWhiteSpace(state.CurrentLocationId))
                    continue;

                if (!_byLocation.TryGetValue(state.CurrentLocationId, out var list))
                {
                    list = new List<string>();
                    _byLocation[state.CurrentLocationId] = list;
                }

                list.Add(state.NpcId);
            }
        }

        /// <summary>Восстанавливает расписания из сохранения (п. 7 ТЗ).</summary>
        public void RestoreState(List<NpcSaveData> source)
        {
            if (source == null)
                return;

            foreach (var entry in source)
            {
                if (entry == null || !_states.TryGetValue(entry.npcId, out var state))
                    continue;

                var locationId = string.IsNullOrWhiteSpace(entry.currentLocationId)
                    ? state.CurrentLocationId
                    : entry.currentLocationId;

                _states[entry.npcId] = new NpcState(
                    entry.npcId, locationId, entry.scheduleEntryId,
                    NpcSimulationLevel.Distant, ResolveWorldPosition(locationId));
            }

            RebuildLocationIndex();
        }

        /// <summary>Дописывает место и этап расписания в существующие записи NPC.</summary>
        public void CaptureState(List<NpcSaveData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            foreach (var data in target)
            {
                if (data == null || !_states.TryGetValue(data.npcId, out var state))
                    continue;

                data.currentLocationId = state.CurrentLocationId;
                data.scheduleEntryId = state.ScheduleEntryId;
            }
        }
    }
}
