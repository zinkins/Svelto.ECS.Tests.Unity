using NUnit.Framework;
using Svelto.ECS;
using Svelto.ECS.Native;
using Svelto.ECS.Schedulers;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public class NativeEntityFactoryTests
{
    SimpleEntitiesSubmissionScheduler _scheduler;
    EnginesRoot _enginesRoot;
    IEntityFactory _factory;
    IEntityFunctions _functions;
    TestEngine _engine;

    [SetUp]
    public void Init()
    {
        _scheduler   = new SimpleEntitiesSubmissionScheduler();
        _enginesRoot = new EnginesRoot(_scheduler);
        _factory = _enginesRoot.GenerateEntityFactory();
        _functions = _enginesRoot.GenerateEntityFunctions();
        _engine = new TestEngine();

        _enginesRoot.AddEngine(_engine);
    }

    [Test]
    public void TestParallelNativeInitializerReturningEntityReferencesWithoutReuse()
    {
        var creationJob = new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"),
            references = new NativeArray<EntityReference>(10000, Allocator.Persistent)
        };

        // Full granularity to have as many threads as possible.
        var job = creationJob.Schedule(creationJob.references.Length, 1);
        job.Complete();

        _scheduler.SubmitEntities();

        var (egids, references, count) = _engine.entitiesDB.QueryEntities<EGIDComponent, NativeSelfReferenceComponent>(TestGroupA);
        for (var i = 0; i < count; i++)
        {
            Assert.IsTrue(_engine.entitiesDB.TryGetEGID(references[i].value, out var refEgid));
            Assert.AreEqual(egids[i].ID, refEgid);
        }
    }

    [Test]
    public void TestParallelNativeInitializerReturningEntityReferencesWithReuse()
    {
        var firstCreationJob = new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"),
            references = new NativeArray<EntityReference>(1500, Allocator.Persistent)
        };

        // Full granularity to have as many threads as possible.
        var firstJob = firstCreationJob.Schedule(firstCreationJob.references.Length, 1);
        firstJob.Complete();
        _scheduler.SubmitEntities();

        _functions.RemoveEntitiesFromGroup(TestGroupA);
        _scheduler.SubmitEntities();

        var secondCreationJob = new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"),
            references = new NativeArray<EntityReference>(5000, Allocator.Persistent)
        };

        // Full granularity to have as many threads as possible.
        var secondJob = secondCreationJob.Schedule(secondCreationJob.references.Length, 1);
        secondJob.Complete();
        _scheduler.SubmitEntities();

        var (egids, references, count) = _engine.entitiesDB.QueryEntities<EGIDComponent, NativeSelfReferenceComponent>(TestGroupA);
        for (var i = 0; i < count; i++)
        {
            Assert.IsTrue(_engine.entitiesDB.TryGetEGID(references[i].value, out var refEgid));
            Assert.AreEqual(egids[i].ID, refEgid);
        }
    }

    [Test]
    public void TestParallelNativeInitializerReturningEntityReferencesMultipleJobs()
    {
        var firstCreationJob = new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"),
            references = new NativeArray<EntityReference>(10000, Allocator.Persistent)
        };
        var firstJob = firstCreationJob.Schedule(firstCreationJob.references.Length, 1);

        var secondCreationJob = new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"),
            references = new NativeArray<EntityReference>(5000, Allocator.Persistent)
        };
        var secondJob = secondCreationJob.Schedule(secondCreationJob.references.Length, 1);

        JobHandle.CombineDependencies(firstJob, secondJob).Complete();

        _scheduler.SubmitEntities();

        var (egids, references, count) = _engine.entitiesDB.QueryEntities<EGIDComponent, NativeSelfReferenceComponent>(TestGroupA);
        for (var i = 0; i < count; i++)
        {
            Assert.IsTrue(_engine.entitiesDB.TryGetEGID(references[i].value, out var refEgid));
            Assert.AreEqual(egids[i].ID, refEgid);
        }
    }

    struct CreateEntitiesJob : IJobParallelFor
    {
        public NativeEntityFactory factory;
        [DeallocateOnJobCompletion]
        public NativeArray<EntityReference> references;

        [NativeSetThreadIndex] int threadIndex;

        public void Execute(int index)
        {
            var initializer = factory.BuildEntity(new EGID(0, TestGroupA), threadIndex);
            initializer.Init(new NativeSelfReferenceComponent{value = initializer.reference});

            references[index] = initializer.reference;
        }
    }

    public static readonly ExclusiveGroup TestGroupA = new ExclusiveGroup();

    struct NativeSelfReferenceComponent : IEntityComponent
    {
        public EntityReference value;
    }

    class TestDescriptor : GenericEntityDescriptor<EGIDComponent, NativeSelfReferenceComponent> {}

    class TestEngine : IQueryingEntitiesEngine
    {
        public void Ready() { }

        public EntitiesDB entitiesDB { get; set; }
    }
}
