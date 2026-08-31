using System;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Blueprinter
{
    public class LiveryAddressableProvider : IResourceProvider
    {
        public const string Id = "Blueprinter.LiveryAddressableProvider";

        public string ProviderId => Id;
        public ProviderBehaviourFlags BehaviourFlags => ProviderBehaviourFlags.None;

        public Type GetDefaultType(IResourceLocation location)
        {
            return location?.ResourceType ?? typeof(UnityEngine.Object);
        }

        public bool CanProvide(Type type, IResourceLocation location)
        {
            return location is LiveryAddressableLocation;
        }

        public void Provide(ProvideHandle handle)
        {
            var location = (LiveryAddressableLocation)handle.Location;
            handle.Complete(location.Asset, true, null);
        }

        public void Release(IResourceLocation location, object obj)
        {
        }
    }
}
