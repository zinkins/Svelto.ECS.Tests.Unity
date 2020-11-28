using System.Collections;
using NUnit.Framework;
using Svelto.ECS;
using Svelto.ECS.Schedulers;

///Note: these tests are only meant to test the code on several platforms. The extensive test coveage of Svelto.ECS
/// is found at https://github.com/sebas77/Svelto.ECS.Tests
namespace Tests
{
    [TestFixture]
    public class Tests
    {
        EnginesRoot _enginesRoot;
        SimpleEntitiesSubmissionScheduler _simpleEntitiesSubmissionScheduler;
        readonly ExclusiveGroup @group = new ExclusiveGroup();
        TestEngine _testEngine;

        [SetUp]
        public void Setup()
        {
            _simpleEntitiesSubmissionScheduler = new SimpleEntitiesSubmissionScheduler();
            _enginesRoot = new EnginesRoot(_simpleEntitiesSubmissionScheduler);
            _testEngine = new TestEngine(@group);
            _enginesRoot.AddEngine(_testEngine);
        }
        
        // A Test behaves as an ordinary method
        [Test]
        public void SimplePasses()
        {
            var init = _enginesRoot.GenerateEntityFactory().BuildEntity<TestEntityDescriptor>(new EGID(0, group));
            init.Init(new UnmanagedComponent() {test = 2});
            _simpleEntitiesSubmissionScheduler.SubmitEntities();

            Assert.DoesNotThrow(() => _testEngine.Test());
            
            _enginesRoot.Dispose();
        }
        
        // A Test behaves as an ordinary method
        [Test]
        public void NativeFactory()
        {
            var init = _enginesRoot.GenerateEntityFactory().ToNative<TestEntityDescriptor>("test").BuildEntity(new EGID(0, group), 0);
            init.Init(new UnmanagedComponent() {test = 2});
            _simpleEntitiesSubmissionScheduler.SubmitEntities();

            Assert.DoesNotThrow(() => _testEngine.Test());
            
            _enginesRoot.Dispose();
        }
    }

    public class TestEntityDescriptor : GenericEntityDescriptor<UnmanagedComponent> { }

    public struct UnmanagedComponent : IEntityComponent, INeedEGID
    {
        public int test;
        public EGID ID { get; set; }
    }

    public class TestEngine : IQueryingEntitiesEngine
    {
        readonly ExclusiveGroup @group;
        public TestEngine(ExclusiveGroup @group) { this.@group = @group; }
        public EntitiesDB entitiesDB { get; set; }

        public void Ready()
        {
        }

        public void Test()
        {
            var (buffer, count) = entitiesDB.QueryEntities<UnmanagedComponent>(@group);

            for (int i = 0; i < count; ++i)
            {
                buffer[i].test = 1;
            }
        }
    }
}
