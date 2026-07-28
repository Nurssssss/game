using NUnit.Framework;
using QonaevLife.Core;
using QonaevLife.UI;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Маршрутизация экранов интерфейса (п. 9 ТЗ).</summary>
    [TestFixture]
    public sealed class UiRouterTests
    {
        private EventBus _eventBus;
        private UiRouter _router;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _router = new UiRouter(_eventBus);
        }

        [Test]
        public void NewRouter_HasNoScreen()
        {
            Assert.That(_router.Current, Is.EqualTo(UiScreen.None));
            Assert.That(_router.IsGameplayBlocked, Is.False);
            Assert.That(_router.ShouldPauseTime, Is.False);
        }

        [Test]
        public void Push_OpensScreenAndBlocksGameplay()
        {
            _router.Push(UiScreen.Phone);

            Assert.That(_router.Current, Is.EqualTo(UiScreen.Phone));
            Assert.That(_router.IsGameplayBlocked, Is.True);
        }

        [Test]
        public void Push_PublishesEvent()
        {
            ScreenChangedEvent captured = default;
            _eventBus.Subscribe<ScreenChangedEvent>(e => captured = e);

            _router.Push(UiScreen.Settings);

            Assert.That(captured.Previous, Is.EqualTo(UiScreen.None));
            Assert.That(captured.Current, Is.EqualTo(UiScreen.Settings));
        }

        [Test]
        public void PushSameScreenTwice_DoesNotStack()
        {
            _router.Push(UiScreen.Phone);
            _router.Push(UiScreen.Phone);

            Assert.That(_router.Stack, Has.Count.EqualTo(1));
        }

        [Test]
        public void Pop_ReturnsToPreviousScreen()
        {
            _router.Push(UiScreen.Phone);
            _router.Push(UiScreen.Map);

            _router.Pop();

            Assert.That(_router.Current, Is.EqualTo(UiScreen.Phone));
            Assert.That(_router.IsGameplayBlocked, Is.True);
        }

        [Test]
        public void PopLastScreen_ReturnsControlToPlayer()
        {
            _router.Push(UiScreen.Phone);

            _router.Pop();

            Assert.That(_router.Current, Is.EqualTo(UiScreen.None));
            Assert.That(_router.IsGameplayBlocked, Is.False);
        }

        [Test]
        public void Pop_OnEmptyStack_IsSafe()
        {
            var events = 0;
            _eventBus.Subscribe<ScreenChangedEvent>(_ => events++);

            Assert.DoesNotThrow(() => _router.Pop());
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void CloseAll_ClearsStack()
        {
            _router.Push(UiScreen.Phone);
            _router.Push(UiScreen.Map);
            _router.Push(UiScreen.Settings);

            _router.CloseAll();

            Assert.That(_router.Current, Is.EqualTo(UiScreen.None));
            Assert.That(_router.Stack, Is.Empty);
        }

        [Test]
        public void Replace_DiscardsStack()
        {
            _router.Push(UiScreen.Phone);
            _router.Push(UiScreen.Map);

            _router.Replace(UiScreen.MainMenu);

            Assert.That(_router.Current, Is.EqualTo(UiScreen.MainMenu));
            Assert.That(_router.Stack, Has.Count.EqualTo(1));
        }

        /// <summary>Меню и настройки останавливают время, диалог — нет.</summary>
        [TestCase(UiScreen.MainMenu, true)]
        [TestCase(UiScreen.SaveSlots, true)]
        [TestCase(UiScreen.Settings, true)]
        [TestCase(UiScreen.Credits, true)]
        [TestCase(UiScreen.Dialogue, false)]
        [TestCase(UiScreen.Phone, false)]
        [TestCase(UiScreen.Map, false)]
        public void ShouldPauseTime_DependsOnScreen(UiScreen screen, bool expected)
        {
            _router.Push(screen);

            Assert.That(_router.ShouldPauseTime, Is.EqualTo(expected));
        }

        [Test]
        public void PushNone_ClosesEverything()
        {
            _router.Push(UiScreen.Phone);
            _router.Push(UiScreen.None);

            Assert.That(_router.Current, Is.EqualTo(UiScreen.None));
        }

        [Test]
        public void Shutdown_ClearsStack()
        {
            _router.Push(UiScreen.Phone);

            _router.Shutdown();

            Assert.That(_router.Current, Is.EqualTo(UiScreen.None));
        }
    }
}
