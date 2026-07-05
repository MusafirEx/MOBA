using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class CharacterModelConfig : MonoBehaviour
    {
        [Header("Weapon Mount Points")]
        [Tooltip("Optional manual list. If empty, mount points are discovered automatically from this character prefab.")]
        public WeaponMountPoint[] weaponMountPoints;

        [Header("Legacy Weapon Holder")]
        [Tooltip("Used only when this character has no WeaponMountPoint components at all.")]
        public Transform weaponHolder;

        [Header("Cosmetic Model Holder")]
        public Transform cosmeticHolder;

        [Header("Character's Animator")]
        public Animator _cacheAnimator;

        private GameObject legacyWeaponModel;
        private GameObject cosmeticModel;

        private void Awake()
        {
            RefreshMountPoints();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshMountPoints();
        }
#endif

        [ContextMenu("Refresh Weapon Mount Points")]
        public void RefreshMountPoints()
        {
            if (weaponMountPoints == null || weaponMountPoints.Length == 0)
            {
                weaponMountPoints = GetComponentsInChildren<WeaponMountPoint>(true);
            }
        }

        public WeaponModelComponent SetWeaponConfig(WeaponConfig weapon, CharacterConfig character)
        {
            if (weapon == null)
                return null;

            RefreshMountPoints();

            WeaponMountPoint mount = GetMount(weapon.requiredMount);
            if (mount != null)
            {
                if (!mount.CanAccept(weapon, character))
                {
                    Debug.LogWarning(
                        $"Weapon '{weapon.weaponName}' is not compatible with {weapon.requiredMount} mount on '{name}'.",
                        this);
                    return null;
                }

                Debug.Log(
                    $"Mounting weapon '{weapon.weaponName}' on {weapon.requiredMount} mount '{mount.name}'.",
                    this);

                return mount.Attach(weapon);
            }

            // Only use the old holder when this character has no mount system at all.
            if (weaponMountPoints == null || weaponMountPoints.Length == 0)
            {
                Debug.LogWarning(
                    $"Character '{name}' has no WeaponMountPoint components. Using legacy weapon holder.",
                    this);

                SetWeaponModel(weapon.weaponModel);
                return legacyWeaponModel != null
                    ? legacyWeaponModel.GetComponentInChildren<WeaponModelComponent>(true)
                    : null;
            }

            // The character has mount points, but not the requested category.
            // Do not silently place the weapon in the legacy holder.
            Debug.LogError(
                $"Character '{name}' has weapon mounts, but no '{weapon.requiredMount}' mount. " +
                $"Weapon '{weapon.weaponName}' was not attached.",
                this);

            return null;
        }

        public WeaponMountPoint GetMount(WeaponMountType mountType)
        {
            RefreshMountPoints();

            if (weaponMountPoints == null)
                return null;

            for (int i = 0; i < weaponMountPoints.Length; i++)
            {
                WeaponMountPoint mount = weaponMountPoints[i];
                if (mount != null && mount.mountType == mountType)
                    return mount;
            }

            return null;
        }

        public void ClearWeaponMount(WeaponMountType mountType)
        {
            WeaponMountPoint mount = GetMount(mountType);
            if (mount != null)
                mount.Clear();
        }

        public void ClearAllWeaponMounts()
        {
            RefreshMountPoints();

            if (weaponMountPoints == null)
                return;

            for (int i = 0; i < weaponMountPoints.Length; i++)
            {
                if (weaponMountPoints[i] != null)
                    weaponMountPoints[i].Clear();
            }
        }


        public void ClearAllWeapons()
        {
            ClearAllWeaponMounts();

            if (weaponHolder != null)
            {
                for (int i = weaponHolder.childCount - 1; i >= 0; i--)
                    Destroy(weaponHolder.GetChild(i).gameObject);
            }

            if (legacyWeaponModel != null)
            {
                Destroy(legacyWeaponModel);
                legacyWeaponModel = null;
            }
        }

        // Legacy API retained for old character prefabs only.
        public void SetWeaponModel(GameObject model)
        {
            if (weaponHolder == null)
            {
                Debug.LogError($"Legacy weapon holder is missing on '{name}'.", this);
                return;
            }

            for (int i = weaponHolder.childCount - 1; i >= 0; i--)
                Destroy(weaponHolder.GetChild(i).gameObject);

            if (legacyWeaponModel != null)
                Destroy(legacyWeaponModel);

            legacyWeaponModel = AddModel(model, weaponHolder);
        }

        public void SetCosmeticModel(GameObject model)
        {
            if (cosmeticHolder == null)
                return;

            for (int i = cosmeticHolder.childCount - 1; i >= 0; i--)
                Destroy(cosmeticHolder.GetChild(i).gameObject);

            if (cosmeticModel != null)
                Destroy(cosmeticModel);

            cosmeticModel = AddModel(model, cosmeticHolder);
        }

        private static GameObject AddModel(GameObject model, Transform parent)
        {
            if (model == null || parent == null)
                return null;

            GameObject instance = Instantiate(model, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);
            return instance;
        }
    }
}
