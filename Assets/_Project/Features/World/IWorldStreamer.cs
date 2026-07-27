using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.World
{
    /// <summary>Состояние загрузки сектора.</summary>
    public enum SectorLoadState
    {
        Unloaded = 0,
        Loading = 1,
        Loaded = 2,
        Unloading = 3
    }

    public readonly struct SectorLoadedEvent : IGameEvent
    {
        public SectorLoadedEvent(string sectorId) => SectorId = sectorId;
        public string SectorId { get; }
    }

    public readonly struct SectorUnloadedEvent : IGameEvent
    {
        public SectorUnloadedEvent(string sectorId) => SectorId = sectorId;
        public string SectorId { get; }
    }

    public readonly struct ActiveSectorChangedEvent : IGameEvent
    {
        public ActiveSectorChangedEvent(string previousSectorId, string currentSectorId)
        {
            PreviousSectorId = previousSectorId;
            CurrentSectorId = currentSectorId;
        }

        public string PreviousSectorId { get; }
        public string CurrentSectorId { get; }
    }

    /// <summary>
    /// Потоковая загрузка секторов района (FR-020, FR-024). Перемещение между
    /// секторами не требует возврата в главное меню, поэтому загрузка идёт
    /// аддитивно и асинхронно.
    /// </summary>
    public interface IWorldStreamer
    {
        /// <summary>Сектор, в котором находится игрок.</summary>
        string ActiveSectorId { get; }

        IReadOnlyCollection<string> LoadedSectorIds { get; }

        SectorLoadState GetState(string sectorId);

        /// <summary>
        /// Делает сектор активным: загружает его и соседей, выгружает лишние.
        /// </summary>
        void SetActiveSector(string sectorId);

        /// <summary>Загружен ли сектор и готов ли к работе.</summary>
        bool IsLoaded(string sectorId);
    }
}
