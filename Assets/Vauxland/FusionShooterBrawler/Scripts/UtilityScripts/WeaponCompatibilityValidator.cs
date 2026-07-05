using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public static class WeaponCompatibilityValidator
    {
        public static bool IsCompatible(WeaponConfig weapon, WeaponMountPoint mount, CharacterConfig character)
        {
            if (weapon == null || mount == null)
                return false;

            if (weapon.requiredMount != mount.mountType)
                return false;

            if ((int)weapon.sizeClass > (int)mount.maximumSize)
                return false;

            if (weapon.compatibleCharacters == null || weapon.compatibleCharacters.Length == 0)
                return true;

            if (character == null)
                return false;

            for (int i = 0; i < weapon.compatibleCharacters.Length; i++)
            {
                if (weapon.compatibleCharacters[i] == character)
                    return true;
            }

            return false;
        }
    }
}
