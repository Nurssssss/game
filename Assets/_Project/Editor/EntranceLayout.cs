using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Куда встали фасады зданий после расстановки города. Генератор точек
    /// интереса берёт координаты отсюда, а не из заданных вручную значений:
    /// иначе вход оказывается внутри дома, потому что расстановка домов
    /// зависит от их фактической ширины.
    /// </summary>
    public static class EntranceLayout
    {
        /// <summary>Место у фасада, пригодное для входа в заведение.</summary>
        public readonly struct Entrance
        {
            public Entrance(Vector3 doorPosition, float facingYaw, string buildingName)
            {
                DoorPosition = doorPosition;
                FacingYaw = facingYaw;
                BuildingName = buildingName;
            }

            /// <summary>Точка перед фасадом, на тротуаре.</summary>
            public Vector3 DoorPosition { get; }

            /// <summary>Поворот вывески — она смотрит от дома к улице.</summary>
            public float FacingYaw { get; }

            public string BuildingName { get; }
        }

        private static readonly List<Entrance> Registered = new();

        public static IReadOnlyList<Entrance> All => Registered;

        public static void Clear() => Registered.Clear();

        /// <summary>
        /// Регистрирует вход у фасада. Позиция сдвигается от центра дома
        /// наружу, чтобы вывеска и маркер оказались перед стеной, а не в ней.
        /// </summary>
        public static void Register(GameObject building, float buildingYaw, string buildingName)
        {
            if (building == null)
                return;

            // Отступ считается от фактических габаритов меша, а не от центра
            // с угаданной шириной: модели набора не симметричны, и вход,
            // отложенный от центра, попадал внутрь стены.
            var bounds = CalculateBounds(building);
            var forward = Quaternion.Euler(0f, buildingYaw, 0f) * Vector3.forward;

            // Половина габарита вдоль направления фасада плюс ширина тротуара.
            var extent = Mathf.Abs(forward.x) * bounds.extents.x
                         + Mathf.Abs(forward.z) * bounds.extents.z;

            // Отходим от фасада, пока место не окажется свободным. Вычислять
            // отступ по габаритам недостаточно: соседние дома и козырьки тоже
            // занимают пространство, а прижим к тротуару возвращал вход в стену.
            const float sidewalkGap = 1.4f;
            const float step = 0.8f;
            const int maxSteps = 12;

            var placement = bounds.center + forward * (extent + sidewalkGap);
            placement.y = 0f;

            for (var i = 0; i < maxSteps && IsBlocked(placement); i++)
                placement += forward * step;

            Registered.Add(new Entrance(placement, buildingYaw, buildingName));
        }

        /// <summary>
        /// Занято ли место домом. Проверяется физикой, а не расчётом: рядом
        /// могут стоять соседние дома и козырьки, которых расчёт по габаритам
        /// одного здания не учитывает.
        /// </summary>
        private static bool IsBlocked(Vector3 position)
        {
            var probe = position + Vector3.up * 1f;

            foreach (var collider in Physics.OverlapSphere(probe, 1.3f))
            {
                // Дома помечены LOD-группой; земля, дороги и бордюры — нет.
                if (collider.GetComponentInParent<LODGroup>() != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Стоит ли точка на тротуаре, а не на проезжей части. Планировка
        /// города: полуширина дороги 5 м, тротуар до 8.5 м от осевой.
        /// </summary>
        private static bool IsOnSidewalk(Vector3 position)
        {
            const float roadHalfWidth = 5.2f;

            return Mathf.Abs(position.x) > roadHalfWidth
                   && Mathf.Abs(position.z) > roadHalfWidth;
        }

        /// <summary>Габариты дома по его рендерерам в мировых координатах.</summary>
        private static Bounds CalculateBounds(GameObject building)
        {
            var renderers = building.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
                return new Bounds(building.transform.position, Vector3.one * 8f);

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        /// <summary>
        /// Ближайший свободный вход к желаемой точке. Использованные входы
        /// исключаются, чтобы два заведения не оказались в одной двери.
        /// </summary>
        public static bool TryTakeNearest(Vector3 preferred, HashSet<int> used,
            out Entrance entrance)
        {
            var bestIndex = -1;
            var bestDistance = float.MaxValue;

            // Сначала ищем только среди входов на тротуаре: вход посреди
            // проезжей части технически свободен, но игрок к нему подойдёт
            // по дороге, а заведение будет выглядеть стоящим на асфальте.
            for (var pass = 0; pass < 2; pass++)
            {
                var requireSidewalk = pass == 0;

                for (var i = 0; i < Registered.Count; i++)
                {
                    if (used.Contains(i))
                        continue;

                    if (requireSidewalk && !IsOnSidewalk(Registered[i].DoorPosition))
                        continue;

                    var distance = Vector3.SqrMagnitude(
                        Registered[i].DoorPosition - preferred);

                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    bestIndex = i;
                }

                if (bestIndex >= 0)
                    break;
            }

            if (bestIndex < 0)
            {
                entrance = default;
                return false;
            }

            used.Add(bestIndex);
            entrance = Registered[bestIndex];
            return true;
        }
    }
}
