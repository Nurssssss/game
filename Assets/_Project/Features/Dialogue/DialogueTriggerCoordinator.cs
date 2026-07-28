using System;
using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.World;

namespace QonaevLife.Dialogue
{
    /// <summary>
    /// Запускает диалог, когда игрок взаимодействует с точкой, где по расписанию
    /// находится именной NPC (FR-031, FR-033). Хранит доверие и флаги диалогов,
    /// применяя эффекты выбора (FR-034) — до появления полноценного NpcService
    /// это владелец состояния NPC.
    /// </summary>
    public sealed class DialogueTriggerCoordinator : IGameService
    {
        private const string TrustEffect = "trust";
        private const string FlagEffect = "flag";

        private readonly IEventBus _eventBus;
        private readonly DialogueService _dialogue;
        private readonly ContentDatabase _content;
        private readonly IGameClock _clock;
        private readonly Npc.INpcService _npcService;

        private readonly Dictionary<string, float> _trust = new();
        private readonly Dictionary<string, HashSet<string>> _flags = new();

        public DialogueTriggerCoordinator(IEventBus eventBus, DialogueService dialogue,
            ContentDatabase content, IGameClock clock, Npc.INpcService npcService = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _npcService = npcService;
        }

        public void Initialize()
        {
            foreach (var npc in _content.Npcs)
            {
                if (npc == null || string.IsNullOrWhiteSpace(npc.Id))
                    continue;

                _trust[npc.Id] = npc.InitialTrust;
                _flags[npc.Id] = new HashSet<string>();
            }

            _eventBus.Subscribe<LocationInteractedEvent>(OnLocationInteracted);
            _eventBus.Subscribe<DialogueEffectRequestedEvent>(OnEffectRequested);
        }

        public void Shutdown()
        {
            _eventBus.Unsubscribe<LocationInteractedEvent>(OnLocationInteracted);
            _eventBus.Unsubscribe<DialogueEffectRequestedEvent>(OnEffectRequested);
            _trust.Clear();
            _flags.Clear();
        }

        public float GetTrust(string npcId)
            => _trust.TryGetValue(npcId, out var value) ? value : 0f;

        private void OnLocationInteracted(LocationInteractedEvent interacted)
        {
            if (_dialogue.IsActive)
                return;

            var npc = FindNpcAt(interacted.LocationId);
            if (npc == null || string.IsNullOrWhiteSpace(npc.RootDialogueId))
                return;

            _dialogue.TryStart(npc.Id, GetTrust(npc.Id), GetFlags(npc.Id));
        }

        /// <summary>
        /// Кто находится в этой локации сейчас. Ответ берётся у NpcService —
        /// он ведёт расписания и учитывает уже совершённые переходы, поэтому
        /// дублировать разбор расписания здесь не нужно (FR-031).
        /// Без сервиса используется резервный разбор: диалог должен работать
        /// в сцене без полной симуляции NPC.
        /// </summary>
        private NpcDefinition FindNpcAt(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
                return null;

            if (_npcService != null)
            {
                foreach (var npcId in _npcService.GetNpcsAt(locationId))
                {
                    if (_content.TryGetNpc(npcId, out var npc)
                        && !string.IsNullOrWhiteSpace(npc.RootDialogueId))
                    {
                        return npc;
                    }
                }

                return null;
            }

            return FindByScheduleFallback(locationId);
        }

        private NpcDefinition FindByScheduleFallback(string locationId)
        {
            var phase = _clock.Phase.ToString();

            foreach (var npc in _content.Npcs)
            {
                if (npc == null)
                    continue;

                foreach (var entry in npc.Schedule)
                {
                    if (!string.Equals(entry.dayPhase, phase, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.Equals(entry.locationId, locationId, StringComparison.Ordinal))
                        return npc;
                }
            }

            return null;
        }

        /// <summary>Применяет эффекты выбора реплики (FR-034).</summary>
        private void OnEffectRequested(DialogueEffectRequestedEvent effect)
        {
            if (string.IsNullOrWhiteSpace(effect.TargetId))
                return;

            switch (effect.EffectType)
            {
                case TrustEffect:
                    // Доверие меняется только по явному эффекту и зажимается
                    // в [0, 1]: случайных изменений быть не должно.
                    if (_trust.TryGetValue(effect.TargetId, out var current))
                        _trust[effect.TargetId] = Math.Clamp(current + effect.Value, 0f, 1f);
                    break;

                case FlagEffect:
                    GetFlags(effect.NpcId).Add(effect.TargetId);
                    break;
            }
        }

        private HashSet<string> GetFlags(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return new HashSet<string>();

            if (!_flags.TryGetValue(npcId, out var flags))
            {
                flags = new HashSet<string>();
                _flags[npcId] = flags;
            }

            return flags;
        }

        public void CaptureState(List<NpcSaveData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();

            foreach (var pair in _trust)
            {
                var data = new NpcSaveData
                {
                    npcId = pair.Key,
                    trust = pair.Value
                };

                data.dialogueFlags.AddRange(GetFlags(pair.Key));
                target.Add(data);
            }
        }

        public void RestoreState(List<NpcSaveData> source)
        {
            if (source == null)
                return;

            foreach (var entry in source)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.npcId))
                    continue;

                // Исчезнувший из контента NPC молча пропускается, иначе старое
                // сохранение считалось бы повреждённым.
                if (!_content.TryGetNpc(entry.npcId, out _))
                    continue;

                _trust[entry.npcId] = Math.Clamp(entry.trust, 0f, 1f);
                _flags[entry.npcId] = new HashSet<string>(entry.dialogueFlags ?? new List<string>());
            }
        }
    }
}
