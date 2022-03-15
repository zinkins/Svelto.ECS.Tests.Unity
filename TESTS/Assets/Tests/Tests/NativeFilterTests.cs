//#define ENABLE_COMPILING_ERROR

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
#if ENABLE_COMPILING_ERROR
    [Test]
    public void TestBurstFailsCompileCode()
    {
        new ThisCodeWontCompile
        {
        }.Run();
        
        Assert.Pass();
    }
#endif

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

        new CreateFilterAndAddEntitiesInFilters
        {
            filters         = filters, group = TestGroupA,
            typeRef         = new NativeRefWrapperType(new RefWrapperType(typeof(NativeSelfReferenceComponent))),
            filterContextId = _filterContextId
        }.Run();

        EntityFilterCollection filter = filters.GetPersistentFilter<NativeSelfReferenceComponent>(0, _filterContextId);

        Assert.That(filter.GetGroupFilter(TestGroupA).count, Is.EqualTo(10));
    }

    [Test]
    //This test will succeed, this means that allocating native memory outside the job and then fetching
    //it inside the job will work.1
    public void TestModifyingExistingFiltersInsideJobs()
    {
        new CreateEntitiesJob
        {
            factory = _factory.ToNative<TestDescriptor>("TestNative"), group = TestGroupA,
        }.Run();

        _scheduler.SubmitEntities();
        var filters = _engine.entitiesDB.GetFilters();
        filters.GetOrCreatePersistentFilter<NativeSelfReferenceComponent>(0, _filterContextId);

        new AddEntitiesInFilters
        {
            filters = filters, group = TestGroupA, filterContextId = _filterContextId
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

    //This code will generate this error (you can check it in the inspector):
    //todo:warningwarningwarningwarningwarningwarningwarningwarningwarningwarningwarningwarning
    //please be sure to disable it in order to run the other tests
    //todo:warningwarningwarningwarningwarningwarningwarningwarningwarningwarningwarningwarning
    //     Unexpected exception System.Exception: Error while hashing 0x60002D1 in Svelto.ECS, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null ---> System.NotSupportedException: Specified method is not supported.
    //   at Burst.Compiler.IL.Hashing.CacheRuntime.ILFinalHashCalculator.VisitILType (Burst.Compiler.IL.Hashing.Types.ILType ilType) [0x00193] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Hashing.CacheRuntime.ILFinalHashCalculator.VisitTypeSpecification (Burst.Compiler.IL.Hashing.CacheRuntime.Metadata.CachedTypeSpecification& cachedTypeSpecification, Burst.Compiler.IL.Hashing.ILGenericContext genericContext) [0x00008] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Hashing.CacheRuntime.ILFinalHashCalculator.HashMethodDefinition (Burst.Compiler.IL.Hashing.CacheRuntime.ToVisit& itemToVisit) [0x001cb] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Hashing.CacheRuntime.ILFinalHashCalculator.VisitItem (Burst.Compiler.IL.Hashing.CacheRuntime.ToVisit& itemToVisit) [0x000c7] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //    --- End of inner exception stack trace ---
    //   at Burst.Compiler.IL.Hashing.CacheRuntime.ILFinalHashCalculator.VisitItem (Burst.Compiler.IL.Hashing.CacheRuntime.ToVisit& itemToVisit) [0x000ff] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Hashing.CacheRuntime.ILFinalHashCalculator.GetHashImpl () [0x00126] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Hashing.CacheRuntime.ILFinalHashCalculator.GetHash (Mono.Cecil.MethodReference[] methodReferences, Burst.Compiler.IL.Hashing.CacheRuntime.HashCacheAssemblyStore assemblyStore, zzzUnity.Burst.CodeGen.AssemblyLoader assemblyLoader, Burst.Compiler.IL.NativeCompilerOptions options, System.Action`2[T1,T2] onVisitItem) [0x0000c] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.NativeCompiler.ComputeHash () [0x0004e] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Jit.JitCompiler.CompileMethodInternal (Burst.Compiler.IL.Jit.JitResult result, System.Collections.Generic.List`1[T] methodsToCompile, Burst.Compiler.IL.Jit.JitOptions jitOptions) [0x0017f] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Jit.JitCompiler.CompileMethods (Burst.Compiler.IL.Jit.JitMethodGroupRequest& request, Burst.Compiler.IL.Jit.JitCompilationRequestType requestType) [0x00209] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Jit.JitCompiler.CompileMethod (Burst.Compiler.IL.Jit.MethodReferenceWithMethodRefString method, Burst.Compiler.IL.Jit.JitOptions jitOptions, Burst.Compiler.IL.Jit.JitCompilationRequestType requestType) [0x00023] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //   at Burst.Compiler.IL.Jit.JitCompilerService+CompilerThreadContext.Compile (Burst.Compiler.IL.Jit.JitCompilerService+CompileJob job, Burst.Compiler.IL.Jit.JitCompilationRequestType requestType) [0x00491] in <b6aa8fdf6d8144e588cc59c2555fb2d5>:0 
    //
    // While compiling job: System.Void Unity.Jobs.IJobForExtensions/ForJobStruct`1<TestsForBurstTeam/ThisCodeWontCompile>::Execute(T&,System.IntPtr,System.IntPtr,Unity.Jobs.LowLevel.Unsafe.JobRanges&,System.Int32)
    // at <empty>:line 0
#if ENABLE_COMPILING_ERROR
    [BurstCompile]
    struct ThisCodeWontCompile : IJob
    {
        public ExclusiveGroupStruct     @group;
        public EntitiesDB.SveltoFilters filters;
        public NativeRefWrapperType     typeRef;
    
        public void Execute()
        {
            var filter = filters.GetOrCreatePersistentFilter<NativeSelfReferenceComponent>(
                new EntitiesDB.SveltoFilters.CombinedFilterID(0, new EntitiesDB.SveltoFilters.ContextID(1)), typeRef);
            
            filter.Add(new EGID((uint)0, TestGroupA), (uint)0);
        }
    }
#endif

    [BurstCompile]
    //This job will actually allocate the filter it's working with and then it will populate it.
    //However after the first time this job runs, for some reason the filter is found empty. 
    struct CreateFilterAndAddEntitiesInFilters : IJob
    {
        public ExclusiveGroupStruct     @group;
        public EntitiesDB.SveltoFilters filters;
        public NativeRefWrapperType     typeRef;
        public FilterContextID          filterContextId;

        public void Execute()
        {
            var filter = filters.GetOrCreatePersistentFilter<NativeSelfReferenceComponent>(0, filterContextId, typeRef);

            for (int index = 0; index < 10; index++)
                filter.Add(new EGID((uint)index, group), (uint)index);
        }
    }

    [BurstCompile]
    //This job assumes that the filter has been previously allocated. Populating it works as expected.
    struct AddEntitiesInFilters : IJob
    {
        public ExclusiveGroupStruct     @group;
        public EntitiesDB.SveltoFilters filters;
        public FilterContextID          filterContextId;

        public void Execute()
        {
            var combinedFilterID = new CombinedFilterID(0, filterContextId);
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