using System;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpAddWeaponMountPayload
    {
        public AssetRef bundleAsset;
        public WeaponManagerTarget[] weaponManagers;
    }

    [Serializable]
    public class WeaponManagerTarget
    {
        public AssetRef gameAsset;
        public int[] hardpointSetIndices;
    }

    // Legacy Op kept for compatibility with existing Blueprinter content.
    public static class OpAddWeaponMountToWeaponManagerHandler
    {
        public const string OpId = "OpAddWeaponMountToWeaponManager";

        public static void Handle(LoadedBundle bundle, OpAddWeaponMountPayload payload)
        {
            if (payload.bundleAsset == null || payload.weaponManagers == null || payload.weaponManagers.Length == 0)
                return;

            var mount = ResourcesAssetResolver.ResolveBundleAsset(bundle, payload.bundleAsset) as WeaponMount;
            if (mount == null)
                return;

            foreach (var target in payload.weaponManagers)
            {
                if (target?.gameAsset == null)
                    continue;

                var weaponManager = ResourcesAssetResolver.ResolveGameAsset(target.gameAsset) as WeaponManager;
                if (weaponManager?.hardpointSets == null)
                    continue;

                foreach (var hardpointIndex in target.hardpointSetIndices ?? Array.Empty<int>())
                {
                    if (hardpointIndex < 0 || hardpointIndex >= weaponManager.hardpointSets.Length)
                        continue;

                    var set = weaponManager.hardpointSets[hardpointIndex];
                    if (set == null)
                        continue;

                    set.weaponOptions ??= [];
                    if (!set.weaponOptions.Contains(mount))
                        set.weaponOptions.Add(mount);
                }
            }
        }
    }
}
