namespace Svelto.ECS
{
    public static class EntityDescriptorExtension
    {
        public static bool IsUnmanaged(this IEntityDescriptor descriptor)
        {
            foreach (IComponentBuilder component in descriptor.componentsToBuild)
                if (typeof(EntityInfoComponent).IsAssignableFrom(component.GetEntityComponentType()) == false &&
                    component.isUnmanaged == false)
                    return false;
            
            return true;
        }
    }
}