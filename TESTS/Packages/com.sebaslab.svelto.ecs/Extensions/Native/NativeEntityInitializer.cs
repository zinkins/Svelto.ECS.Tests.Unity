#if UNITY_NATIVE //at the moment I am still considering NativeOperations useful only for Unity
using Svelto.ECS.DataStructures;

namespace Svelto.ECS.Native
{
    public readonly ref struct NativeEntityInitializer
    {
        readonly NativeBag        _unsafeBuffer;
        readonly UnsafeArrayIndex _index;
        readonly EntityReference  _reference;

        public NativeEntityInitializer(in NativeBag unsafeBuffer, UnsafeArrayIndex index, EntityReference reference)
        {
            _unsafeBuffer = unsafeBuffer;
            _index        = index;
            _reference    = reference;
        }

        public ref T Init<T>(in T component) where T : unmanaged, IEntityComponent
        {
            uint componentID = EntityComponentID<T>.ID.Data;

            _unsafeBuffer.AccessReserved<uint>(_index)++; //number of components initialised by the user so far

            //Since NativeEntityInitializer is a ref struct, it guarantees that I am enqueueing components of the
            //last entity built
            _unsafeBuffer.Enqueue(componentID); //to know what component it's being stored
            _unsafeBuffer.ReserveEnqueue<T>(out var index) = component;

            return ref _unsafeBuffer.AccessReserved<T>(index);
        }

        public EntityReference reference => _reference;
    }
}
#endif