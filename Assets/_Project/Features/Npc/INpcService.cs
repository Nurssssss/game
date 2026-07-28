using System.Collections.Generic;
using QonaevLife.Core;
using UnityEngine;

namespace QonaevLife.Npc
{
    /// <summary>
    /// Уровень симуляции NPC (FR-032). Полная симуляция стоит дорого, поэтому
    /// вдали от игрока NPC существует записью состояния, без анимации и
    /// навигации, но расписание при этом не теряется.
    /// </summary>
    public enum NpcSimulationLevel
    {
        /// <summary>Далеко: только логическое место и этап расписания.</summary>
        Distant = 0,

        /// <summary>Рядом: настоящий объект, навигация, анимация.</summary>
        Active = 1
    }

    /// <summary>Состояние NPC в мире.</summary>
    public readonly struct NpcState
    {
        public NpcState(string npcId, string currentLocationId, string scheduleEntryId,
            NpcSimulationLevel level, Vector3 worldPosition)
        {
            NpcId = npcId;
            CurrentLocationId = currentLocationId;
            ScheduleEntryId = scheduleEntryId;
            Level = level;
            WorldPosition = worldPosition;
        }

        public string NpcId { get; }

        /// <summary>Где NPC находится логически — по расписанию.</summary>
        public string CurrentLocationId { get; }

        public string ScheduleEntryId { get; }
        public NpcSimulationLevel Level { get; }

        /// <summary>Позиция в мире. У удалённых NPC — позиция целевой локации.</summary>
        public Vector3 WorldPosition { get; }
    }

    public readonly struct NpcScheduleChangedEvent : IGameEvent
    {
        public NpcScheduleChangedEvent(string npcId, string previousLocationId,
            string currentLocationId, string scheduleEntryId)
        {
            NpcId = npcId;
            PreviousLocationId = previousLocationId;
            CurrentLocationId = currentLocationId;
            ScheduleEntryId = scheduleEntryId;
        }

        public string NpcId { get; }
        public string PreviousLocationId { get; }
        public string CurrentLocationId { get; }
        public string ScheduleEntryId { get; }
    }

    public readonly struct NpcSimulationLevelChangedEvent : IGameEvent
    {
        public NpcSimulationLevelChangedEvent(string npcId, NpcSimulationLevel level,
            Vector3 worldPosition)
        {
            NpcId = npcId;
            Level = level;
            WorldPosition = worldPosition;
        }

        public string NpcId { get; }
        public NpcSimulationLevel Level { get; }
        public Vector3 WorldPosition { get; }
    }

    /// <summary>
    /// Именные NPC района (FR-030 — FR-032). Ведёт расписания, решает, кто
    /// симулируется полностью, и сообщает сцене, каких NPC нужно показать.
    /// </summary>
    public interface INpcService
    {
        IReadOnlyCollection<string> NpcIds { get; }

        bool TryGetState(string npcId, out NpcState state);

        /// <summary>Кто сейчас находится в этой локации по расписанию.</summary>
        IReadOnlyList<string> GetNpcsAt(string locationId);

        /// <summary>Обновляет расписания и уровни симуляции.</summary>
        void Update(Vector3 playerPosition);
    }
}
