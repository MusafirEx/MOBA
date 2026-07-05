using Fusion;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    [RequireComponent(typeof(PlayerManager))]
    [RequireComponent(typeof(MonsterWeaponLoadout))]
    public class MountedWeaponCombatController : NetworkBehaviour
    {
        [Header("One loader per mount")]
        [SerializeField] private NetworkProjectileLoader topProjectileLoader;
        [SerializeField] private NetworkProjectileLoader leftProjectileLoader;
        [SerializeField] private NetworkProjectileLoader rightProjectileLoader;

        [Header("Automatic Reload")]
        [SerializeField] private bool autoReload = true;

        [Networked] public int TopLoadedAmmo { get; private set; }
        [Networked] public int TopReserveAmmo { get; private set; }
        [Networked] public int LeftLoadedAmmo { get; private set; }
        [Networked] public int LeftReserveAmmo { get; private set; }
        [Networked] public int RightLoadedAmmo { get; private set; }
        [Networked] public int RightReserveAmmo { get; private set; }

        [Networked] private TickTimer TopCooldown { get; set; }
        [Networked] private TickTimer LeftCooldown { get; set; }
        [Networked] private TickTimer RightCooldown { get; set; }
        [Networked] private TickTimer TopReload { get; set; }
        [Networked] private TickTimer LeftReload { get; set; }
        [Networked] private TickTimer RightReload { get; set; }

        [Networked] private int TopFireCount { get; set; }
        [Networked] private int LeftFireCount { get; set; }
        [Networked] private int RightFireCount { get; set; }

        [Networked] private Vector3 NetworkAimPoint { get; set; }
        [Networked] private Vector3 NetworkAimDirection { get; set; }
        [Networked] private NetworkId LockedTargetId { get; set; }
        [Networked] private NetworkBool AmmoInitialized { get; set; }

        private int renderedTopFireCount;
        private int renderedLeftFireCount;
        private int renderedRightFireCount;

        private PlayerManager playerManager;
        private MonsterWeaponLoadout loadout;

        public override void Spawned()
        {
            playerManager = GetComponent<PlayerManager>();
            loadout = GetComponent<MonsterWeaponLoadout>();
        }

        public override void FixedUpdateNetwork()
        {
            if (playerManager == null || loadout == null || !loadout.IsReady)
                return;

            if (Object.HasStateAuthority && !AmmoInitialized)
                InitializeAmmo();

            ProcessReloads();

            if (!Runner.TryGetInputForPlayer<PlayerNetworkInputData>(Object.InputAuthority, out var input))
                return;

            NetworkAimPoint = input.aimPoint;
            NetworkAimDirection = input.aimDirection;
            LockedTargetId = input.lockedTargetId;

            if (playerManager._playerStats == null ||
                !playerManager._playerStats.AcceptPlayerInput ||
                playerManager._matchManager == null ||
                playerManager._matchManager.GameIsOver)
                return;

            bool topHeld = input.networkButtons.IsSet(NetInputButtons.Shoot);
            bool leftHeld = input.networkButtons.IsSet(NetInputButtons.FireLeft);
            bool rightHeld = input.networkButtons.IsSet(NetInputButtons.FireRight);

            TryFireMount(WeaponMountType.Top, topHeld);
            TryFireMount(WeaponMountType.Left, leftHeld);
            TryFireMount(WeaponMountType.Right, rightHeld);

            playerManager._playerStats.IsShooting = topHeld || leftHeld || rightHeld;
        }

        public void BotAimAndFire(
            Vector3 aimPoint,
            Vector3 aimDirection,
            NetworkId targetId,
            bool fireTop,
            bool fireLeft,
            bool fireRight)
        {
            if (!Object.HasStateAuthority ||
                playerManager == null ||
                loadout == null ||
                !loadout.IsReady)
                return;

            if (!AmmoInitialized)
                InitializeAmmo();

            ProcessReloads();

            NetworkAimPoint = aimPoint;
            NetworkAimDirection = aimDirection.sqrMagnitude > 0.0001f
                ? aimDirection.normalized
                : transform.forward;
            LockedTargetId = targetId;

            if (playerManager._playerStats == null ||
                !playerManager._playerStats.AcceptPlayerInput ||
                playerManager._matchManager == null ||
                playerManager._matchManager.GameIsOver)
            {
                if (playerManager._playerStats != null)
                    playerManager._playerStats.IsShooting = false;
                return;
            }

            TryFireMount(WeaponMountType.Top, fireTop);
            TryFireMount(WeaponMountType.Left, fireLeft);
            TryFireMount(WeaponMountType.Right, fireRight);

            playerManager._playerStats.IsShooting =
                fireTop || fireLeft || fireRight;
        }

        public bool CanBotFireMount(WeaponMountType mountType)
        {
            if (loadout == null || !loadout.IsReady)
                return false;

            WeaponConfig weapon = loadout.GetWeapon(mountType);
            if (weapon == null || weapon.projectile == null)
                return false;

            return !IsReloading(mountType) &&
                   CooldownReady(mountType) &&
                   GetLoaded(mountType) >= Mathf.Max(1, weapon.ammoAmountShotUse);
        }

        public override void Render()
        {
            if (playerManager == null || loadout == null || !loadout.IsReady)
                return;

            AimMountedWeapon(WeaponMountType.Top);
            AimMountedWeapon(WeaponMountType.Left);
            AimMountedWeapon(WeaponMountType.Right);

            PlayFireEffects(WeaponMountType.Top, ref renderedTopFireCount, TopFireCount);
            PlayFireEffects(WeaponMountType.Left, ref renderedLeftFireCount, LeftFireCount);
            PlayFireEffects(WeaponMountType.Right, ref renderedRightFireCount, RightFireCount);

            if (Object.HasInputAuthority && playerManager._matchUI != null)
            {
                WeaponConfig topWeapon = loadout.GetWeapon(WeaponMountType.Top);
                WeaponConfig leftWeapon = loadout.GetWeapon(WeaponMountType.Left);
                WeaponConfig rightWeapon = loadout.GetWeapon(WeaponMountType.Right);

                playerManager._matchUI.UpdateMountedWeaponUI(
                    topWeapon, TopLoadedAmmo, TopReserveAmmo,
                    IsReloading(WeaponMountType.Top), GetReloadProgress(WeaponMountType.Top, topWeapon),

                    leftWeapon, LeftLoadedAmmo, LeftReserveAmmo,
                    IsReloading(WeaponMountType.Left), GetReloadProgress(WeaponMountType.Left, leftWeapon),

                    rightWeapon, RightLoadedAmmo, RightReserveAmmo,
                    IsReloading(WeaponMountType.Right), GetReloadProgress(WeaponMountType.Right, rightWeapon));
            }
        }

        private void InitializeAmmo()
        {
            InitializeSlot(loadout.GetWeapon(WeaponMountType.Top), out int tl, out int tr);
            InitializeSlot(loadout.GetWeapon(WeaponMountType.Left), out int ll, out int lr);
            InitializeSlot(loadout.GetWeapon(WeaponMountType.Right), out int rl, out int rr);

            TopLoadedAmmo = tl;
            TopReserveAmmo = tr;
            LeftLoadedAmmo = ll;
            LeftReserveAmmo = lr;
            RightLoadedAmmo = rl;
            RightReserveAmmo = rr;

            ConfigureLoader(topProjectileLoader, loadout.GetWeapon(WeaponMountType.Top));
            ConfigureLoader(leftProjectileLoader, loadout.GetWeapon(WeaponMountType.Left));
            ConfigureLoader(rightProjectileLoader, loadout.GetWeapon(WeaponMountType.Right));

            AmmoInitialized = true;
        }

        private static void InitializeSlot(WeaponConfig weapon, out int loaded, out int reserve)
        {
            loaded = 0;
            reserve = 0;
            if (weapon == null || weapon.weaponStats == null)
                return;

            loaded = Mathf.Max(0, weapon.weaponStats.GetStatValue(StatType.LoadedAmmo));
            reserve = Mathf.Max(0, weapon.weaponStats.GetStatValue(StatType.ReserveAmmo));
        }

        private static void ConfigureLoader(NetworkProjectileLoader loader, WeaponConfig weapon)
        {
            if (loader != null && weapon != null && weapon.projectile != null)
                loader.SetProjectileObject(weapon.projectile);
        }

        private void TryFireMount(WeaponMountType mountType, bool held)
        {
            if (!held)
                return;

            WeaponConfig weapon = loadout.GetWeapon(mountType);
            if (weapon == null || weapon.projectile == null)
                return;

            if (IsReloading(mountType) || !CooldownReady(mountType))
                return;

            int loaded = GetLoaded(mountType);
            int ammoUse = Mathf.Max(1, weapon.ammoAmountShotUse);

            if (loaded < ammoUse)
            {
                if (autoReload)
                    BeginReload(mountType, weapon);
                return;
            }

            if (weapon.aimMode == WeaponAimMode.LockOnHoming &&
                LockedTargetId == default &&
                weapon.noLockBehaviour == NoLockFireBehaviour.PreventFire)
                return;

            WeaponModelComponent model = playerManager._playerVisuals != null
                ? playerManager._playerVisuals.GetMountedWeaponModel(mountType)
                : null;

            if (model == null)
                return;

            NetworkProjectileLoader loader = GetLoader(mountType);
            if (loader == null)
                return;

            Vector3 launchPosition = model.GetLaunchPosition(weapon.launchOffset);
            Vector3 muzzleForward = model.GetMuzzleForward();
            Vector3 launchForward = ResolveLaunchDirection(weapon, launchPosition, muzzleForward);

            int projectileCount = weapon.isShotgun ? Mathf.Max(1, weapon.shotgunPelletCount) : 1;
            for (int i = 0; i < projectileCount; i++)
            {
                Vector3 direction = launchForward;
                if (weapon.isShotgun)
                {
                    float h = Random.Range(-weapon.shotSpreadAngleHorizontal * 0.5f,
                                           weapon.shotSpreadAngleHorizontal * 0.5f);
                    float v = Random.Range(-weapon.shotSpreadAngleVertical * 0.5f,
                                           weapon.shotSpreadAngleVertical * 0.5f);
                    direction = Quaternion.Euler(v, h, 0f) * launchForward;
                }

                Projectile projectile = loader.Request<Projectile>(
                    launchPosition, Quaternion.LookRotation(direction), Object.InputAuthority);

                if (projectile != null)
                {
                    projectile.ShootProjectile(
                        launchPosition,
                        direction,
                        Object.InputAuthority,
                        playerManager._networkObject,
                        weapon,
                        playerManager._playerController,
                        LockedTargetId);
                }
            }

            SetLoaded(mountType, loaded - ammoUse);
            SetCooldown(mountType, TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.01f, weapon.delayBetweenShots)));
            IncrementFireCount(mountType);
        }

        private Vector3 ResolveLaunchDirection(WeaponConfig weapon, Vector3 launchPosition, Vector3 muzzleForward)
        {
            if (weapon.aimMode == WeaponAimMode.Fixed ||
                weapon.aimMode == WeaponAimMode.PitchOnly ||
                weapon.aimMode == WeaponAimMode.YawAndPitch)
                return muzzleForward.normalized;

            if (weapon.aimMode == WeaponAimMode.LockOnHoming &&
                LockedTargetId != default &&
                Runner.TryFindObject(LockedTargetId, out NetworkObject target))
            {
                Vector3 targetPoint = target.transform.position + Vector3.up * weapon.homingTargetOffsetY;
                return (targetPoint - launchPosition).normalized;
            }

            if (weapon.aimMode == WeaponAimMode.LockOnHoming &&
                weapon.noLockBehaviour == NoLockFireBehaviour.FireUnguided)
                return muzzleForward.normalized;

            Vector3 direction = NetworkAimPoint - launchPosition;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = NetworkAimDirection.sqrMagnitude > 0.0001f
                    ? NetworkAimDirection
                    : muzzleForward;

            return direction.normalized;
        }

        private void ProcessReloads()
        {
            CompleteReloadIfReady(WeaponMountType.Top, loadout.GetWeapon(WeaponMountType.Top));
            CompleteReloadIfReady(WeaponMountType.Left, loadout.GetWeapon(WeaponMountType.Left));
            CompleteReloadIfReady(WeaponMountType.Right, loadout.GetWeapon(WeaponMountType.Right));
        }

        private void BeginReload(WeaponMountType mountType, WeaponConfig weapon)
        {
            if (weapon == null || GetReserve(mountType) <= 0 || IsReloading(mountType))
                return;

            SetReloadTimer(mountType, TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.01f, weapon.reloadDuration)));
        }

        private void CompleteReloadIfReady(WeaponMountType mountType, WeaponConfig weapon)
        {
            TickTimer timer = GetReloadTimer(mountType);
            if (weapon == null || !timer.IsRunning || !timer.Expired(Runner))
                return;

            int loaded = GetLoaded(mountType);
            int reserve = GetReserve(mountType);
            int clipSize = Mathf.Max(1, weapon.reloadClipSize);
            int needed = Mathf.Max(0, clipSize - loaded);
            int transfer = weapon.reloadOneAmmoAtATime ? Mathf.Min(1, reserve) : Mathf.Min(needed, reserve);

            SetLoaded(mountType, loaded + transfer);
            SetReserve(mountType, reserve - transfer);

            if (weapon.reloadOneAmmoAtATime && GetLoaded(mountType) < clipSize && GetReserve(mountType) > 0)
                SetReloadTimer(mountType, TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.01f, weapon.reloadDuration)));
            else
                SetReloadTimer(mountType, TickTimer.None);
        }

        private void AimMountedWeapon(WeaponMountType mountType)
        {
            WeaponConfig weapon = loadout.GetWeapon(mountType);
            WeaponModelComponent model = playerManager._playerVisuals != null
                ? playerManager._playerVisuals.GetMountedWeaponModel(mountType)
                : null;

            if (weapon != null && model != null)
                model.AimAt(NetworkAimPoint, weapon, Time.deltaTime);
        }

        private void PlayFireEffects(WeaponMountType mountType, ref int renderedCount, int networkCount)
        {
            if (renderedCount >= networkCount)
                return;

            renderedCount = networkCount;
            WeaponConfig weapon = loadout.GetWeapon(mountType);
            WeaponModelComponent model = playerManager._playerVisuals != null
                ? playerManager._playerVisuals.GetMountedWeaponModel(mountType)
                : null;

            if (model != null && model.muzzleEffect != null)
                model.muzzleEffect.Play();

            if (weapon != null && weapon.shootFx != null &&
                playerManager._playerVisuals != null &&
                playerManager._playerVisuals.fxPlayer != null)
                playerManager._playerVisuals.fxPlayer.PlayOneShot(weapon.shootFx);
        }

        private float GetReloadProgress(WeaponMountType mountType, WeaponConfig weapon)
        {
            if (weapon == null)
                return 0f;

            TickTimer timer = GetReloadTimer(mountType);
            if (!timer.IsRunning)
                return 0f;

            float duration = Mathf.Max(0.01f, weapon.reloadDuration);
            float remaining = timer.RemainingTime(Runner) ?? 0f;
            return Mathf.Clamp01(1f - (remaining / duration));
        }

        private NetworkProjectileLoader GetLoader(WeaponMountType type)
        {
            switch (type)
            {
                case WeaponMountType.Top: return topProjectileLoader;
                case WeaponMountType.Left: return leftProjectileLoader;
                case WeaponMountType.Right: return rightProjectileLoader;
                default: return null;
            }
        }

        private bool CooldownReady(WeaponMountType type) => GetCooldown(type).ExpiredOrNotRunning(Runner);
        private bool IsReloading(WeaponMountType type)
        {
            TickTimer timer = GetReloadTimer(type);
            return timer.IsRunning && !timer.Expired(Runner);
        }

        private TickTimer GetCooldown(WeaponMountType type)
        {
            switch (type) {
                case WeaponMountType.Top: return TopCooldown;
                case WeaponMountType.Left: return LeftCooldown;
                case WeaponMountType.Right: return RightCooldown;
                default: return TickTimer.None;
            }
        }

        private void SetCooldown(WeaponMountType type, TickTimer value)
        {
            switch (type) {
                case WeaponMountType.Top: TopCooldown = value; break;
                case WeaponMountType.Left: LeftCooldown = value; break;
                case WeaponMountType.Right: RightCooldown = value; break;
            }
        }

        private TickTimer GetReloadTimer(WeaponMountType type)
        {
            switch (type) {
                case WeaponMountType.Top: return TopReload;
                case WeaponMountType.Left: return LeftReload;
                case WeaponMountType.Right: return RightReload;
                default: return TickTimer.None;
            }
        }

        private void SetReloadTimer(WeaponMountType type, TickTimer value)
        {
            switch (type) {
                case WeaponMountType.Top: TopReload = value; break;
                case WeaponMountType.Left: LeftReload = value; break;
                case WeaponMountType.Right: RightReload = value; break;
            }
        }

        private int GetLoaded(WeaponMountType type)
        {
            switch (type) {
                case WeaponMountType.Top: return TopLoadedAmmo;
                case WeaponMountType.Left: return LeftLoadedAmmo;
                case WeaponMountType.Right: return RightLoadedAmmo;
                default: return 0;
            }
        }

        private void SetLoaded(WeaponMountType type, int value)
        {
            value = Mathf.Max(0, value);
            switch (type) {
                case WeaponMountType.Top: TopLoadedAmmo = value; break;
                case WeaponMountType.Left: LeftLoadedAmmo = value; break;
                case WeaponMountType.Right: RightLoadedAmmo = value; break;
            }
        }

        private int GetReserve(WeaponMountType type)
        {
            switch (type) {
                case WeaponMountType.Top: return TopReserveAmmo;
                case WeaponMountType.Left: return LeftReserveAmmo;
                case WeaponMountType.Right: return RightReserveAmmo;
                default: return 0;
            }
        }

        private void SetReserve(WeaponMountType type, int value)
        {
            value = Mathf.Max(0, value);
            switch (type) {
                case WeaponMountType.Top: TopReserveAmmo = value; break;
                case WeaponMountType.Left: LeftReserveAmmo = value; break;
                case WeaponMountType.Right: RightReserveAmmo = value; break;
            }
        }

        private void IncrementFireCount(WeaponMountType type)
        {
            switch (type) {
                case WeaponMountType.Top: TopFireCount++; break;
                case WeaponMountType.Left: LeftFireCount++; break;
                case WeaponMountType.Right: RightFireCount++; break;
            }
        }
    }
}
