#if UNITY_NATIVE
using System.Runtime.CompilerServices;
using Svelto.ECS.DataStructures;

namespace Svelto.ECS.Native
{
    public readonly struct NativeEntityFactory
    {
        internal NativeEntityFactory(AtomicNativeBags addOperationQueues, int index, EntityReferenceMap entityLocator)
        {
            _index             = index;
            _addOperationQueues = addOperationQueues;
            _entityLocator     = entityLocator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeEntityInitializer BuildEntity
            (uint eindex, ExclusiveBuildGroup exclusiveBuildGroup, int threadIndex)
        {
            EntityReference reference = _entityLocator.ClaimReference();
            var             egid      = new EGID(eindex, exclusiveBuildGroup);

            NativeBag bagPerEntityPerThread = _addOperationQueues.GetBag(threadIndex + 1); //fetch the Queue linked to this thread

            bagPerEntityPerThread.Enqueue(_index); //store the index to the descriptor of the entity we are building. the descriptor is stored in the _nativeAddOperations array 
            bagPerEntityPerThread.Enqueue(egid);
            bagPerEntityPerThread.Enqueue(reference);
            
            //NativeEntityInitializer is quite a complex beast. It holds the initialization values of the component set by the user. These components must be later dequeued and in order to know how many components
            //must be dequeued, a count must be used. The space to hold the count is reserved and countPosition will be used to access the count through NativeEntityInitializer.
            //the count value is not the number of components of the entity, it's just the number of components that the user decides to initialise
            bagPerEntityPerThread.ReserveEnqueue<uint>(out var countPosition) = 0;

            return new NativeEntityInitializer(bagPerEntityPerThread, countPosition, reference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeEntityInitializer BuildEntity(EGID egid, int threadIndex)
        {
            return BuildEntity(egid.entityID, egid.groupID, threadIndex);
        }

        readonly EntityReferenceMap  _entityLocator;
        readonly AtomicNativeBags        _addOperationQueues;
        readonly int                     _index;
    }
}
#endif