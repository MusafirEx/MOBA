using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class WeaponMountPoint : MonoBehaviour
    {
        [Header("Mount Rules")]
        public WeaponMountType mountType;
        public WeaponSizeClass maximumSize = WeaponSizeClass.Heavy;

        [Header("Attachment")]
        [Tooltip("Weapon model is instantiated under this transform. Uses this transform when empty.")]
        public Transform modelParent;

        [HideInInspector] public WeaponModelComponent currentWeaponModel;

        private void Reset()
        {
            modelParent = transform;
        }

        public bool CanAccept(WeaponConfig weapon, CharacterConfig character)
        {
            return WeaponCompatibilityValidator.IsCompatible(weapon, this, character);
        }

        public WeaponModelComponent Attach(WeaponConfig weapon)
        {
            Clear();

            if (weapon == null || weapon.weaponModel == null)
                return null;

            Transform parent = modelParent != null ? modelParent : transform;
            GameObject instance = Instantiate(weapon.weaponModel, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            currentWeaponModel = instance.GetComponentInChildren<WeaponModelComponent>(true);
            return currentWeaponModel;
        }

        public void Clear()
        {
            Transform parent = modelParent != null ? modelParent : transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);

            currentWeaponModel = null;
        }
    }
}
