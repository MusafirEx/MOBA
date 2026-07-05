/*
 * Copyright (c) 2024 VAUXLAND
 * Modified with third-person weapon aiming options.
 */
using Fusion;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public enum WeaponAimMode
    {
        Fixed,
        PitchOnly,
        YawAndPitch,
        CameraRaycast,
        LockOnHoming
    }

    public enum NoLockFireBehaviour
    {
        PreventFire,
        FireUnguided,
        UseCameraRaycast
    }

    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "FusionBrawler/WeaponConfig")]
    public class WeaponConfig : ScriptableObject
    {
        [Header("Weapon Setup")]
        public string weaponName;
        public Sprite weaponIcon;
        public GameObject weaponModel;
        public NetworkObject projectile;
        public float delayBetweenShots;
        public float delayBeforeShooting;
        public float launchOffset;
        public int destructibleDamage;
        public int animID;

        [Header("Mount Compatibility")]
        public WeaponMountType requiredMount = WeaponMountType.Top;
        public WeaponSizeClass sizeClass = WeaponSizeClass.Light;
        [Tooltip("Leave empty to allow this weapon on every character that has a compatible mount.")]
        public CharacterConfig[] compatibleCharacters;

        [Header("Aim Behaviour")]
        public WeaponAimMode aimMode = WeaponAimMode.CameraRaycast;
        public float yawSpeed = 90f;
        public float pitchSpeed = 60f;
        public float minimumYaw = -90f;
        public float maximumYaw = 90f;
        public float minimumPitch = -20f;
        public float maximumPitch = 45f;
        public NoLockFireBehaviour noLockBehaviour = NoLockFireBehaviour.UseCameraRaycast;

        [Header("Homing Settings")]
        public float homingTurnSpeed = 120f;
        public float homingTargetOffsetY = 1f;

        [Header("Shotgun Setup")]
        public bool isShotgun;
        public int shotgunPelletCount = 5;
        public float shotSpreadAngleHorizontal = 10f;
        public float shotSpreadAngleVertical = 5f;

        [Header("Weapon Stats")]
        public StatDictionary weaponStats;

        [Header("ReloadingSettings")]
        public bool reloadOneAmmoAtATime;
        public float reloadDuration;
        public int reloadClipSize;
        public int ammoAmountShotUse;

        [Header("SFX")]
        public AudioClip shootFx;
        public AudioClip reloadFx;
        public AudioClip emptyFx;
    }
}
