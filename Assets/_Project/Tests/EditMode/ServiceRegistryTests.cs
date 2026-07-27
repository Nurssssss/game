using NUnit.Framework;
using QonaevLife.Core;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Реестр сервисов композиционного корня (п. 4.2 ТЗ).</summary>
    [TestFixture]
    public sealed class ServiceRegistryTests
    {
        private interface ISampleContract
        {
            int InitializeCalls { get; }
        }

        private sealed class SampleService : ISampleContract, IGameService
        {
            public int InitializeCalls { get; private set; }
            public int ShutdownCalls { get; private set; }

            public void Initialize() => InitializeCalls++;
            public void Shutdown() => ShutdownCalls++;
        }

        private interface IOtherContract
        {
        }

        private sealed class OtherService : IOtherContract
        {
        }

        private ServiceRegistry _registry;

        [SetUp]
        public void SetUp() => _registry = new ServiceRegistry();

        [Test]
        public void Resolve_ReturnsRegisteredImplementation()
        {
            var service = new SampleService();
            _registry.Register<ISampleContract>(service);

            Assert.That(_registry.Resolve<ISampleContract>(), Is.SameAs(service));
        }

        [Test]
        public void Resolve_UnregisteredContract_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => _registry.Resolve<ISampleContract>());
        }

        [Test]
        public void TryResolve_ReportsMissingContract()
        {
            Assert.That(_registry.TryResolve<ISampleContract>(out var service), Is.False);
            Assert.That(service, Is.Null);
        }

        [Test]
        public void DuplicateRegistration_Throws()
        {
            _registry.Register<ISampleContract>(new SampleService());

            Assert.Throws<System.InvalidOperationException>(
                () => _registry.Register<ISampleContract>(new SampleService()));
        }

        [Test]
        public void NullImplementation_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => _registry.Register<ISampleContract>(null));
        }

        [Test]
        public void InitializeAll_InitializesGameServicesOnce()
        {
            var service = new SampleService();
            _registry.Register<ISampleContract>(service);

            _registry.InitializeAll();
            _registry.InitializeAll();

            Assert.That(service.InitializeCalls, Is.EqualTo(1));
        }

        [Test]
        public void Register_AfterInitialize_Throws()
        {
            _registry.InitializeAll();

            Assert.Throws<System.InvalidOperationException>(
                () => _registry.Register<IOtherContract>(new OtherService()));
        }

        [Test]
        public void ShutdownAll_ShutsDownAndClearsRegistry()
        {
            var service = new SampleService();
            _registry.Register<ISampleContract>(service);
            _registry.InitializeAll();

            _registry.ShutdownAll();

            Assert.That(service.ShutdownCalls, Is.EqualTo(1));
            Assert.That(_registry.TryResolve<ISampleContract>(out _), Is.False);
        }

        [Test]
        public void NonGameService_IsStillResolvable()
        {
            _registry.Register<IOtherContract>(new OtherService());
            _registry.InitializeAll();

            Assert.That(_registry.Resolve<IOtherContract>(), Is.Not.Null);
        }
    }
}
