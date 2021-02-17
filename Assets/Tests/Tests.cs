#warning to run the tests, open the Unity Test Runner

using System;
using NUnit.Framework;
using Svelto.ECS;
using Svelto.ECS.Schedulers;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

///Note: these tests are only meant to test the code on unity platforms. The extensive test coveage of Svelto.ECS
/// is found at https://github.com/sebas77/Svelto.ECS.Tests
namespace Tests
{
    [TestFixture]
    public class Tests
    {
        EnginesRoot                       _enginesRoot;
        SimpleEntitiesSubmissionScheduler _simpleEntitiesSubmissionScheduler;
        readonly ExclusiveGroup           @group = new ExclusiveGroup();
        TestEngine                        _testEngine;

        [SetUp]
        public void Setup()
        {
            _simpleEntitiesSubmissionScheduler = new SimpleEntitiesSubmissionScheduler();
            _enginesRoot                       = new EnginesRoot(_simpleEntitiesSubmissionScheduler);
            _testEngine                        = new TestEngine(@group);
            _enginesRoot.AddEngine(_testEngine);
        }

        // A Test behaves as an ordinary method
        [Test]
        public void Factory()
        {
            var init = _enginesRoot.GenerateEntityFactory().BuildEntity<TestEntityDescriptor>(new EGID(0, group));
            init.Init(new UnmanagedComponent()
            {
                test = 2
            });
            _simpleEntitiesSubmissionScheduler.SubmitEntities();

            Assert.DoesNotThrow(() => _testEngine.Test());

            _enginesRoot.Dispose();
        }

#if PROFILE_SVELTO
        [Test]
        public void Preallocation()
        {
            void BuildEntity(IEntityFactory entityFactory)
            {
                var init = entityFactory.BuildEntity<TestEntityDescriptor>(new EGID(0, @group));
                init.Init(new UnmanagedComponent()
                {
                    test = 2
                });
            }

            var generateEntityFactory = _enginesRoot.GenerateEntityFactory();
            generateEntityFactory.PreallocateEntitySpace<TestEntityDescriptor>(group, 100);
            generateEntityFactory.BuildEntity<TestEntityDescriptor>(new EGID(0, group));
            
            _simpleEntitiesSubmissionScheduler.SubmitEntities(); //warm up method
            
            _simpleEntitiesSubmissionScheduler = new SimpleEntitiesSubmissionScheduler();
            _enginesRoot                       = new EnginesRoot(_simpleEntitiesSubmissionScheduler);
            
            generateEntityFactory              = _enginesRoot.GenerateEntityFactory();
            generateEntityFactory.PreallocateEntitySpace<TestEntityDescriptor>(group, 100);
           
            Assert.That(() =>  BuildEntity(generateEntityFactory), Is.Not.AllocatingGCMemory());
            Assert.That(_simpleEntitiesSubmissionScheduler.SubmitEntities, Is.Not.AllocatingGCMemory());

            _enginesRoot.Dispose();
        }
#endif
        
        // A Test behaves as an ordinary method
        [Test]
        public void NativeFactory()
        {
            var init = _enginesRoot.GenerateEntityFactory().ToNative<TestEntityDescriptor>("test")
                                   .BuildEntity(new EGID(0, group), 0);
            init.Init(new UnmanagedComponent()
            {
                test = 2
            });
            _simpleEntitiesSubmissionScheduler.SubmitEntities();

            Assert.DoesNotThrow(() => _testEngine.Test());

            _enginesRoot.Dispose();
        }
    }

    public class TestEntityDescriptor : GenericEntityDescriptor<UnmanagedComponent> { }

    public struct UnmanagedComponent : IEntityComponent, INeedEGID
    {
        public int  test;
        public EGID ID { get; set; }
    }

    public class TestEngine : IQueryingEntitiesEngine
    {
        readonly ExclusiveGroup @group;
        public TestEngine(ExclusiveGroup @group) { this.@group = @group; }
        public EntitiesDB entitiesDB { get; set; }

        public void Ready() { }

        public void Test()
        {
            var (buffer, count) = entitiesDB.QueryEntities<UnmanagedComponent>(@group);

            if (count == 0)
                throw new Exception();
            
            for (int i = 0; i < count; ++i)
            {
                if (buffer[i].test != 2)
                    throw new Exception();
            }
        }
    }
}