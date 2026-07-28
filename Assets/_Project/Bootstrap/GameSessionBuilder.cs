using System;
using System.IO;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Dialogue;
using QonaevLife.Economy;
using QonaevLife.Jobs;
using QonaevLife.Language;
using QonaevLife.Npc;
using QonaevLife.Player;
using QonaevLife.World;

namespace QonaevLife.Bootstrap
{
    /// <summary>
    /// Собранная игровая сессия. Держит конкретные реализации, чтобы Bootstrap
    /// мог продвигать время и записывать состояние, не обращаясь к реестру
    /// за каждым сервисом.
    /// </summary>
    public sealed class GameSession
    {
        public GameSession(ServiceRegistry registry, EventBus eventBus, GameClock clock,
            WeatherService weather, WalletService wallet, NeedsService needs,
            LanguageProgressService language, ISaveService saveService,
            LocationRegistry locations, DialogueService dialogue, JobShiftService jobs,
            DialogueTriggerCoordinator npcState, ContentDatabase content,
            UI.UiRouter router, UI.ISettingsService settings, NpcService npcs,
            LessonService lessons)
        {
            Registry = registry;
            EventBus = eventBus;
            Clock = clock;
            Weather = weather;
            Wallet = wallet;
            Needs = needs;
            Language = language;
            SaveService = saveService;
            Locations = locations;
            Dialogue = dialogue;
            Jobs = jobs;
            NpcState = npcState;
            Content = content;
            Router = router;
            Settings = settings;
            Npcs = npcs;
            Lessons = lessons;
        }

        public ServiceRegistry Registry { get; }
        public EventBus EventBus { get; }
        public GameClock Clock { get; }
        public WeatherService Weather { get; }
        public WalletService Wallet { get; }
        public NeedsService Needs { get; }
        public LanguageProgressService Language { get; }
        public ISaveService SaveService { get; }
        public LocationRegistry Locations { get; }
        public DialogueService Dialogue { get; }
        public JobShiftService Jobs { get; }
        public DialogueTriggerCoordinator NpcState { get; }
        public ContentDatabase Content { get; }
        public UI.UiRouter Router { get; }
        public UI.ISettingsService Settings { get; }
        public NpcService Npcs { get; }
        public LessonService Lessons { get; }

        /// <summary>
        /// Продвигает время сессии. Позиция игрока нужна, чтобы решить,
        /// каких NPC симулировать полностью (FR-032).
        /// </summary>
        public void Tick(float realDeltaSeconds, UnityEngine.Vector3 playerPosition)
        {
            // Уровни симуляции NPC обновляются даже на паузе: иначе, пока
            // открыто меню, ни один NPC не станет активным и город останется
            // пустым при возврате в игру.
            Npcs.Update(playerPosition);

            if (Clock.IsPaused || realDeltaSeconds <= 0f)
                return;

            var gameMinutes = realDeltaSeconds * Clock.MinutesPerRealSecond;

            Clock.Tick(realDeltaSeconds);
            Weather.AdvanceMinutes(gameMinutes);
            Needs.AdvanceMinutes(gameMinutes);

            // После продвижения времени смена могла просрочить лимит этапа.
            Jobs.Tick();
        }

        /// <summary>Собирает текущее состояние сессии в сохранение (FR-003).</summary>
        public SaveData CaptureSave(string profileName)
        {
            var data = new SaveData { ProfileName = profileName };

            data.world.day = Clock.Day;
            data.world.minutesOfDay = Clock.TimeOfDay.TotalMinutes;

            Weather.CaptureState(data.world);
            Locations.CaptureState(data.world);
            Wallet.CaptureState(data.economy);
            Needs.CaptureState(data.player);
            Language.CaptureState(data.language);
            NpcState.CaptureState(data.npcs);

            // NpcService дописывает место и этап расписания в уже созданные
            // записи: доверие принадлежит одному владельцу, место — другому.
            Npcs.CaptureState(data.npcs);

            return data;
        }

        /// <summary>Восстанавливает состояние сессии из сохранения (FR-003, FR-023).</summary>
        public void RestoreSave(SaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Clock.RestoreState(data.world.day, data.world.minutesOfDay);
            Weather.RestoreState(data.world);
            Locations.RestoreState(data.world);
            Wallet.RestoreState(data.economy);
            Needs.RestoreState(data.player);
            Language.RestoreState(data.language);
            NpcState.RestoreState(data.npcs);
            Npcs.RestoreState(data.npcs);
        }

        public void Shutdown() => Registry.ShutdownAll();
    }

    /// <summary>
    /// Композиционный корень (п. 4.2 ТЗ). Единственное место, где создаются
    /// конкретные реализации сервисов и связываются их зависимости.
    /// Не хранит игровое состояние и не содержит игровой логики.
    /// </summary>
    public static class GameSessionBuilder
    {
        /// <summary>
        /// Создаёт сессию. <paramref name="persistentDataPath"/> передаётся
        /// параметром, чтобы сборку можно было проверить вне Unity-плеера.
        /// </summary>
        public static GameSession Build(GameSessionConfig config, ContentDatabase content,
            string persistentDataPath)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (!config.TryValidate(out var error))
                throw new InvalidOperationException($"Некорректный конфиг сессии: {error}");

            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("Не задан путь сохранений.", nameof(persistentDataPath));

            var registry = new ServiceRegistry();

            var eventBus = new EventBus();
            var clock = new GameClock(config.Clock);
            var weather = new WeatherService(eventBus, config.Weather, config.WeatherSeed);
            var wallet = new WalletService(clock, eventBus);
            var needs = new NeedsService(eventBus, config.Needs);
            var language = new LanguageProgressService(eventBus, config.Language);

            var saveDirectory = Path.Combine(persistentDataPath, config.SaveFolderName);
            var saveService = new JsonFileSaveService(saveDirectory, config.SaveSlotCount);

            var router = new UI.UiRouter(eventBus);
            var settings = new UI.JsonSettingsService(saveDirectory, eventBus);

            var locations = new LocationRegistry(content, eventBus, clock);
            var dialogue = new DialogueService(content, eventBus, language);
            var jobs = new JobShiftService(content, eventBus, clock, wallet, locations);

            var lessons = new LessonService(content, eventBus, language, config.Lessons);

            var npcService = new NpcService(
                content, eventBus, clock, locations, config.NpcSimulation);

            var dialogueTrigger = new DialogueTriggerCoordinator(
                eventBus, dialogue, content, clock, npcService);

            // Координатор смены. ID берутся из конфига, а не из кода (п. 10 ТЗ).
            // dialogueGuard не даёт выдать смену молча там, где стоит NPC:
            // работу игрок получает через разговор с диспетчером.
            var coordinator = new CourierShiftCoordinator(
                eventBus, jobs, config.PrimaryJobId, config.PrimaryJobHubLocationId,
                skillProvider: null,
                dialogueGuard: () => dialogue.IsActive);

            registry.Register<IEventBus>(eventBus);
            registry.Register<IGameClock>(clock);
            registry.Register<ISaveService>(saveService);
            registry.Register<IWalletService>(wallet);
            registry.Register<ILanguageProgressService>(language);

            // Конкретные типы регистрируются там, где контракт ещё не выделен;
            // потребители получают их из GameSession, а не через поиск синглтона.
            registry.Register(weather);
            registry.Register(needs);
            registry.Register(locations);
            registry.Register(dialogue);
            registry.Register(jobs);
            registry.Register(lessons);
            registry.Register<INpcService>(npcService);
            registry.Register(npcService);
            registry.Register(dialogueTrigger);
            registry.Register(coordinator);
            registry.Register<UI.IUiRouter>(router);
            registry.Register<UI.ISettingsService>(settings);

            registry.InitializeAll();

            return new GameSession(
                registry, eventBus, clock, weather, wallet, needs, language, saveService,
                locations, dialogue, jobs, dialogueTrigger, content, router, settings,
                npcService, lessons);
        }

        /// <summary>
        /// Начисляет стартовый капитал новой игры через журнал транзакций,
        /// чтобы у денег всегда была причина и источник (FR-050, FR-075).
        /// </summary>
        public static void ApplyNewGameState(GameSession session, GameSessionConfig config)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.StartingCapital <= 0)
                return;

            var result = session.Wallet.TryApply(new TransactionRequest(
                config.StartingCapital,
                TransactionReason.StartingCapital,
                sourceId: "new_game"));

            if (!result.Applied)
            {
                throw new InvalidOperationException(
                    $"Не удалось начислить стартовый капитал: {result.Status}.");
            }
        }
    }
}
