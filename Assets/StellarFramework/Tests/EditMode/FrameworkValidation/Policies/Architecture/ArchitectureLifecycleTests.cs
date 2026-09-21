using NUnit.Framework;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class ArchitectureLifecycleTests
    {
        [Test]
        public void InitInvokesInitModulesBeforeInitializingRegisteredModules()
        {
            TestArchitecture app = TestArchitecture.Interface;
            if (app.State == ArchitectureState.Initialized)
                app.Dispose();

            app = TestArchitecture.Interface;
            app.Init();

            try
            {
                Assert.That(app.State, Is.EqualTo(ArchitectureState.Initialized));
                Assert.That(app.InitModulesCalled, Is.True);

                TestModel model = app.GetModel<TestModel>();
                TestService service = app.GetService<TestService>();
                Assert.That(model, Is.Not.Null);
                Assert.That(service, Is.Not.Null);
                Assert.That(model.Initialized, Is.True);
                Assert.That(service.Initialized, Is.True);
            }
            finally
            {
                if (app.State == ArchitectureState.Initialized)
                    app.Dispose();
            }
        }

        private sealed class TestArchitecture : Architecture<TestArchitecture>
        {
            public bool InitModulesCalled { get; private set; }

            protected override void InitModules()
            {
                InitModulesCalled = true;
                RegisterModel(new TestModel());
                RegisterService(new TestService());
            }
        }

        private sealed class TestModel : AbstractModel
        {
            public bool Initialized { get; private set; }

            public override void Init() => Initialized = true;
            public override void Deinit() => Initialized = false;
        }

        private sealed class TestService : AbstractService
        {
            public bool Initialized { get; private set; }

            public override void Init() => Initialized = true;
            public override void Deinit() => Initialized = false;
        }
    }
}
