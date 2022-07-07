using NUnit.Framework;
using Svelto.ECS;
using Svelto.ECS.Schedulers;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

public class SveltoUnitTest
{
    static class Group
    {
        public static ExclusiveGroup TestGroupA = new ExclusiveGroup();
    }
    
    SimpleEntitiesSubmissionScheduler _scheduler;
    EnginesRoot                       _enginesRoot;
    IEntityFactory                    _factory;
    [NativeSetThreadIndex] int        threadIndex;

    [SetUp]
    public void Init()
    {
        _scheduler   = new SimpleEntitiesSubmissionScheduler();
        _enginesRoot = new EnginesRoot(_scheduler);
        _factory     = _enginesRoot.GenerateEntityFactory();
    }

    [Test]
    public void test_order1()
    {
        var nativeFactory = _factory.ToNative<TestDescriptor>();
        var entity1Init   = nativeFactory.BuildEntity(new EGID(1, Group.TestGroupA), threadIndex);
        ref var selfReference = ref entity1Init.Init(new NativeSelfReferenceComponent());
        
        var entity2Init   = nativeFactory.BuildEntity(new EGID(2, Group.TestGroupA), threadIndex);
        selfReference.value = entity2Init.reference;

        Assert.DoesNotThrow(_scheduler.SubmitEntities);
    }

    [Test]
    public void test_order2()
    {
        var nativeFactory = _factory.ToNative<TestDescriptor>();
        var entity1Init   = nativeFactory.BuildEntity(new EGID(1, Group.TestGroupA), threadIndex);
        var entity2Init   = nativeFactory.BuildEntity(new EGID(2, Group.TestGroupA), threadIndex);

        entity2Init.Init(new NativeSelfReferenceComponent()
        {
            value = entity1Init.reference
        });

        Assert.DoesNotThrow(_scheduler.SubmitEntities);
    }

    struct NativeSelfReferenceComponent : IEntityComponent
    {
        public EntityReference value;
    }

    class TestDescriptor : GenericEntityDescriptor<NativeSelfReferenceComponent> { }
}