using System;
using NUnit.Framework;
using Svelto.DataStructures;
using Svelto.DataStructures.Native;
using Svelto.ECS;
using Svelto.ECS.Native;
using Svelto.ECS.Schedulers;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public class NativeFilterTests
{
    SimpleEntitiesSubmissionScheduler _scheduler;
    EnginesRoot                       _enginesRoot;
    IEntityFactory                    _factory;
    IEntityFunctions                  _functions;
    TestEngine                        _engine;

    [SetUp]
    public void Init()
    {
        _scheduler   = new SimpleEntitiesSubmissionScheduler();
        _enginesRoot = new EnginesRoot(_scheduler);
        _factory     = _enginesRoot.GenerateEntityFactory();
        _functions   = _enginesRoot.GenerateEntityFunctions();
        _engine      = new TestEngine();

        _enginesRoot.AddEngine(_engine);
    }

    [Test]
    public void TestGettingFiltersInsideJobs()
    {
        new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative")
          , group   = TestGroupA
           ,
        }.Schedule(10, default).Complete();

        _scheduler.SubmitEntities();
        var filters = _engine.entitiesDB.GetFilters();

        new AddEntitiesInFilters
        {
            filters = filters
          , group  = TestGroupA
          , typeRef = new NativeRefWrapperType(new RefWrapperType(typeof(NativeSelfReferenceComponent)))
        }.Schedule(10, default).Complete();

        EntityFilterCollection filter = filters.GetOrCreatePersistentFilter<NativeSelfReferenceComponent>(
            new EntitiesDB.SveltoFilters.CombinedFilterID(0, new EntitiesDB.SveltoFilters.ContextID(1)));

        Assert.That(filter.GetGroupFilter(TestGroupA).count, Is.EqualTo(10));
    }

    [BurstCompile]
    struct CreateEntitiesJob : IJobParallelFor
    {
        public NativeEntityFactory factory;

        [NativeSetThreadIndex] int                  threadIndex;
        public                 ExclusiveGroupStruct @group;

        public void Execute(int index)
        {
            var initializer = factory.BuildEntity(new EGID((uint)index, group), threadIndex);
            initializer.Init(new NativeSelfReferenceComponent
            {
                value = initializer.reference
            });
        }
    }

    [BurstCompile]
    struct AddEntitiesInFilters : IJobFor
    {
        public ExclusiveGroupStruct     @group;
        public EntitiesDB.SveltoFilters filters;
        public NativeRefWrapperType     typeRef;

        public void Execute(int index)
        {
            var filter = filters.GetOrCreatePersistentFilter<NativeSelfReferenceComponent>(
                new EntitiesDB.SveltoFilters.CombinedFilterID(0, new EntitiesDB.SveltoFilters.ContextID(1)), typeRef);
            
         //   filter.Add(new EGID((uint)index, group), (uint)index);
        }
    }

    public static readonly ExclusiveGroupStruct TestGroupA = new ExclusiveGroup();
    public static readonly ExclusiveGroupStruct TestGroupB = new ExclusiveGroup();

    struct NativeSelfReferenceComponent : IEntityComponent
    {
        public EntityReference value;
    }

    class TestDescriptor : GenericEntityDescriptor<EGIDComponent, NativeSelfReferenceComponent> { }

    class TestEngine : IQueryingEntitiesEngine
    {
        public void Ready() { }

        public EntitiesDB entitiesDB { get; set; }
    }
}