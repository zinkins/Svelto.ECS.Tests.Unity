using System;
using NUnit.Framework;
using Svelto.DataStructures.Native;
using Svelto.ECS;
using Svelto.ECS.Native;
using Svelto.ECS.Schedulers;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public class TestsForBurstTeam
{
    SimpleEntitiesSubmissionScheduler _scheduler;
    EnginesRoot                       _enginesRoot;
    IEntityFactory                    _factory;
    TestEngine                        _engine;

    [SetUp]
    public void Init()
    {
        _scheduler   = new SimpleEntitiesSubmissionScheduler();
        _enginesRoot = new EnginesRoot(_scheduler);
        _factory     = _enginesRoot.GenerateEntityFactory();
        _engine      = new TestEngine();

        _enginesRoot.AddEngine(_engine);
    }

    [TearDown]
    public void Stop()
    {
        _enginesRoot.Dispose();
    }

    [Test]
    public void TestUnsafeUtilityFreeIsNotRecognisedByBurst()
    {
        new TestFreeJob
        {
        }.Run();

        Assert.Pass();
    }

    [Test]
    //This test will fail, but only the first time it runs!
    public void TestCreatingAndModifyingFiltersInsideJob()
    {
        new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"), group = TestGroupA,
        }.Run();
        
        _scheduler.SubmitEntities();
        var filters = _engine.entitiesDB.GetFilters();
        
        new CreateFilterAndAddEntitiesInFiltersJob
        {
            filters         = filters, group = TestGroupA,
            filterContextId = _filterContextId
        }.Run();
        
        EntityFilterCollection filter = filters.GetPersistentFilter<NativeSelfReferenceComponent>(1, _filterContextId);
        
        Assert.That(filter.GetGroupFilter(TestGroupA).count, Is.EqualTo(10));
    }
    
    [Test]
    //This test will fail, but only the first time it runs!
    public void TestCreatingAndModifyingFiltersInsideAndUsingThemInAnotherJob()
    {
        new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"), group = TestGroupA,
        }.Run();
        
        _scheduler.SubmitEntities();
        var filters = _engine.entitiesDB.GetFilters();
        
        new CreateFilterAndAddEntitiesInFiltersJob
        {
            filters         = filters, group = TestGroupA,
            filterContextId = _filterContextId
        }.Run();
        
        new AddEntitiesInFiltersJob
        {
            filters         = filters, 
            group           = TestGroupA, 
            filterContextId = _filterContextId,
            filterID = 1
        }.Run();
        
        EntityFilterCollection filter = filters.GetPersistentFilter<NativeSelfReferenceComponent>(1, _filterContextId);
        
        Assert.That(filter.GetGroupFilter(TestGroupA).count, Is.EqualTo(10));
    }
    
    [Test]
    //This test will succeed, this means that allocating native memory outside the job and then fetching
    //it inside the job will work.1
    public void TestModifyingExistingFiltersInsideJobs()
    {
        new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"), 
            group = TestGroupA,
        }.Run();

        _scheduler.SubmitEntities();
        var filters = _engine.entitiesDB.GetFilters();
        filters.GetOrCreatePersistentFilter<NativeSelfReferenceComponent>(0, _filterContextId);

        new AddEntitiesInFiltersJob
        {
            filters = filters, 
            group = TestGroupA, 
            filterContextId = _filterContextId
        }.Run();

        EntityFilterCollection filter = filters.GetPersistentFilter<NativeSelfReferenceComponent>(0, _filterContextId);

        Assert.That(filter.GetGroupFilter(TestGroupA).count, Is.EqualTo(10));
    }

    [BurstCompile]
    struct CreateEntitiesJob : IJob
    {
        public NativeEntityFactory factory;

        [NativeSetThreadIndex] int                  threadIndex;
        public                 ExclusiveGroupStruct @group;

        public void Execute()
        {
            for (int index = 0; index < 10; index++)
            {
                var initializer = factory.BuildEntity(new EGID((uint)index, group), threadIndex);
                initializer.Init(new NativeSelfReferenceComponent
                {
                    value = initializer.reference
                });
            }
        }
    }

    [BurstCompile]
    //this works, SveltoDictionaryNative uses Allocator.Persistent
    struct TestFreeJob : IJob
    {
        public void Execute()
        {
            var dictionary = new SveltoDictionaryNative<uint, uint>(1);
            dictionary.Add(1, 2);
            if (dictionary[1] != 2) throw new Exception("Test failed");
            dictionary.Dispose();
        }
    }
    
    [BurstCompile]
    //This job will actually allocate the filter it's working with and then it will populate it.
    //However after the first time this job runs, for some reason the filter is found empty. 
    struct CreateFilterAndAddEntitiesInFiltersJob : IJob
    {
        public ExclusiveGroupStruct     @group;
        public EntitiesDB.SveltoFilters filters;
        public FilterContextID          filterContextId;

        public void Execute()
        {
            ref var filter = ref filters.GetOrCreatePersistentFilter<NativeSelfReferenceComponent>(1, filterContextId);

            for (int index = 0; index < 10; index++)
                filter.Add(new EGID((uint)index, group), (uint)index);
        }
    }

    [BurstCompile]
    //This job assumes that the filter has been previously allocated. Populating it works as expected.
    struct AddEntitiesInFiltersJob : IJob
    {
        public ExclusiveGroupStruct     @group;
        public EntitiesDB.SveltoFilters filters;
        public FilterContextID          filterContextId;
        public int                      filterID;

        public void Execute()
        {
            var combinedFilterID = new CombinedFilterID(filterID, filterContextId);
            var filter           = filters.GetPersistentFilter<NativeSelfReferenceComponent>(combinedFilterID);

            for (int index = 0; index < 10; index++)
                filter.Add(new EGID((uint)index, group), (uint)index);
        }
    }

    static readonly ExclusiveGroupStruct TestGroupA;
    static readonly FilterContextID      _filterContextId;

    static TestsForBurstTeam()
    {
        TestGroupA       = new ExclusiveGroup();
        _filterContextId = EntitiesDB.SveltoFilters.GetNewContextID();
    }

    struct NativeSelfReferenceComponent : IEntityComponent
    {
        public EntityReference value;
    }

    class TestDescriptor : GenericEntityDescriptor<EGIDComponent, NativeSelfReferenceComponent>
    {
    }

    class TestEngine : IQueryingEntitiesEngine
    {
        public void Ready()
        {
        }

        public EntitiesDB entitiesDB { get; set; }
    }
}