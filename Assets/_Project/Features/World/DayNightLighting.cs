using System;
using QonaevLife.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace QonaevLife.World
{
    /// <summary>
    /// Освещение по фазам суток (FR-021): цвет и угол солнца, окружающий свет,
    /// туман и плотность неба. Значения интерполируются по времени, поэтому
    /// рассвет и закат идут плавно, а не переключаются рывком.
    /// Управляется профилями качества: дорогие эффекты можно отключить (п. 8.4 ТЗ).
    /// </summary>
    public sealed class DayNightLighting : MonoBehaviour
    {
        /// <summary>Настройки освещения для одного момента суток.</summary>
        [Serializable]
        public struct LightingKey
        {
            [Tooltip("Час суток, к которому относятся значения.")]
            [Range(0f, 24f)]
            public float hour;

            [Tooltip("Цвет направленного света.")]
            [ColorUsage(false, true)]
            public Color sunColor;

            [Tooltip("Яркость направленного света.")]
            [Min(0f)]
            public float sunIntensity;

            [Tooltip("Высота солнца над горизонтом, градусов.")]
            public float sunElevation;

            [Tooltip("Цвет окружающего света — он определяет тени.")]
            [ColorUsage(false, true)]
            public Color ambientColor;

            [Tooltip("Цвет неба и тумана.")]
            [ColorUsage(false, true)]
            public Color skyColor;

            [Tooltip("Плотность тумана.")]
            [Min(0f)]
            public float fogDensity;
        }

        [Header("Ссылки")]
        [SerializeField] private Light sun;

        [Header("Ключи суток")]
        [SerializeField]
        [Tooltip("Ключи должны идти по возрастанию часа.")]
        private LightingKey[] keys = DefaultKeys();

        private IGameClock _clock;

        /// <summary>Набор ключей по умолчанию: ночь, рассвет, день, закат, ночь.</summary>
        public static LightingKey[] DefaultKeys() => new[]
        {
            new LightingKey
            {
                hour = 0f,
                sunColor = new Color(0.32f, 0.40f, 0.62f),
                sunIntensity = 0.16f,
                sunElevation = -12f,
                ambientColor = new Color(0.07f, 0.09f, 0.16f),
                skyColor = new Color(0.04f, 0.05f, 0.10f),
                fogDensity = 0.020f
            },
            new LightingKey
            {
                hour = 6f,
                sunColor = new Color(1f, 0.62f, 0.38f),
                sunIntensity = 0.75f,
                sunElevation = 6f,
                ambientColor = new Color(0.28f, 0.24f, 0.26f),
                skyColor = new Color(0.55f, 0.42f, 0.42f),
                fogDensity = 0.016f
            },
            new LightingKey
            {
                hour = 9f,
                sunColor = new Color(1f, 0.94f, 0.84f),
                sunIntensity = 1.25f,
                sunElevation = 38f,
                ambientColor = new Color(0.45f, 0.48f, 0.55f),
                skyColor = new Color(0.55f, 0.68f, 0.86f),
                fogDensity = 0.006f
            },
            new LightingKey
            {
                hour = 14f,
                sunColor = new Color(1f, 0.98f, 0.94f),
                sunIntensity = 1.45f,
                sunElevation = 66f,
                ambientColor = new Color(0.52f, 0.55f, 0.60f),
                skyColor = new Color(0.50f, 0.66f, 0.90f),
                fogDensity = 0.004f
            },
            new LightingKey
            {
                hour = 19f,
                sunColor = new Color(1f, 0.52f, 0.28f),
                sunIntensity = 0.70f,
                sunElevation = 8f,
                ambientColor = new Color(0.30f, 0.24f, 0.28f),
                skyColor = new Color(0.62f, 0.38f, 0.32f),
                fogDensity = 0.014f
            },
            new LightingKey
            {
                hour = 22f,
                sunColor = new Color(0.36f, 0.44f, 0.66f),
                sunIntensity = 0.20f,
                sunElevation = -8f,
                ambientColor = new Color(0.09f, 0.11f, 0.19f),
                skyColor = new Color(0.06f, 0.07f, 0.13f),
                fogDensity = 0.019f
            },
            new LightingKey
            {
                hour = 24f,
                sunColor = new Color(0.32f, 0.40f, 0.62f),
                sunIntensity = 0.16f,
                sunElevation = -12f,
                ambientColor = new Color(0.07f, 0.09f, 0.16f),
                skyColor = new Color(0.04f, 0.05f, 0.10f),
                fogDensity = 0.020f
            }
        };

        /// <summary>Подключает к игровым часам.</summary>
        public void Bind(IGameClock clock)
        {
            _clock = clock;

            if (sun == null)
                sun = GetComponent<Light>();

            ApplyForHour(_clock?.TimeOfDay.TotalHours ?? 9d);
        }

        private void Update()
        {
            if (_clock == null)
                return;

            ApplyForHour(_clock.TimeOfDay.TotalHours);
        }

        /// <summary>
        /// Применяет освещение для заданного часа. Публичный, чтобы художник мог
        /// проверить любое время из редактора, не запуская игру.
        /// </summary>
        public void ApplyForHour(double hour)
        {
            if (keys == null || keys.Length < 2)
                return;

            var key = Interpolate((float)hour);

            if (sun != null)
            {
                sun.color = key.sunColor;
                sun.intensity = key.sunIntensity;

                // Азимут привязан к часу: солнце идёт с востока на запад.
                var azimuth = (float)(hour / 24d * 360d) - 90f;
                sun.transform.rotation = Quaternion.Euler(key.sunElevation, azimuth, 0f);

                // Ниже горизонта светить нечему — иначе ночью появятся тени «снизу».
                sun.enabled = key.sunElevation > -2f;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = key.skyColor;
            RenderSettings.ambientEquatorColor = key.ambientColor;
            RenderSettings.ambientGroundColor = key.ambientColor * 0.6f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = key.skyColor;
            RenderSettings.fogDensity = key.fogDensity;
        }

        /// <summary>Линейная интерполяция между соседними ключами по часу.</summary>
        private LightingKey Interpolate(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);

            var previous = keys[0];
            var next = keys[keys.Length - 1];

            for (var i = 0; i < keys.Length - 1; i++)
            {
                if (hour < keys[i].hour || hour > keys[i + 1].hour)
                    continue;

                previous = keys[i];
                next = keys[i + 1];
                break;
            }

            var span = next.hour - previous.hour;
            var t = span <= 0f ? 0f : Mathf.InverseLerp(previous.hour, next.hour, hour);

            return new LightingKey
            {
                hour = hour,
                sunColor = Color.Lerp(previous.sunColor, next.sunColor, t),
                sunIntensity = Mathf.Lerp(previous.sunIntensity, next.sunIntensity, t),
                sunElevation = Mathf.Lerp(previous.sunElevation, next.sunElevation, t),
                ambientColor = Color.Lerp(previous.ambientColor, next.ambientColor, t),
                skyColor = Color.Lerp(previous.skyColor, next.skyColor, t),
                fogDensity = Mathf.Lerp(previous.fogDensity, next.fogDensity, t)
            };
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(Light directionalLight)
        {
            sun = directionalLight;
            keys = DefaultKeys();
        }
    }
}
