using System;
using UnityEngine;

namespace QonaevLife.Npc
{
    /// <summary>
    /// Поведение NPC внутри фазы суток. Смена фазы происходит раз в несколько
    /// игровых часов, поэтому без этих настроек NPC стоял бы на месте почти
    /// весь день и город выглядел бы неживым.
    /// </summary>
    [Serializable]
    public struct NpcWanderSettings
    {
        [Tooltip("Радиус прогулки вокруг точки расписания, м. 0 — стоять на месте.")]
        [Min(0f)]
        public float wanderRadius;

        [Tooltip("Минимальная пауза на месте перед следующим шагом, с.")]
        [Min(0f)]
        public float minPause;

        [Tooltip("Максимальная пауза на месте.")]
        [Min(0f)]
        public float maxPause;

        [Tooltip("Скорость прогулочного шага, м/с.")]
        [Min(0.1f)]
        public float walkSpeed;

        public static NpcWanderSettings Default => new()
        {
            wanderRadius = 4f,
            minPause = 2f,
            maxPause = 6f,
            walkSpeed = 1.1f
        };

        public bool IsValid()
            => wanderRadius >= 0f
               && minPause >= 0f
               && maxPause >= minPause
               && walkSpeed > 0f;
    }
}
