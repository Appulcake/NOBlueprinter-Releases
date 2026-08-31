using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Blueprinter
{
    public class LiveryAddressableLocation : ResourceLocationBase
    {
        public readonly UnityEngine.Object Asset;

        public LiveryAddressableLocation(string guid, UnityEngine.Object asset)
            : base(guid, guid, LiveryAddressableProvider.Id, asset.GetType())
        {
            Asset = asset;
        }
    }
}
