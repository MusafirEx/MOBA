/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using System.Collections;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class ProjectileController : NetworkBehaviour
    {
        public ProjectileLauncherTransform projectileLauncher; // the transform the projectile will launch from
        private PlayerManager _playerManager = null;
        private NetworkObject _projectile = null;
        private CameraFollow cameraFollow;

        [Networked] private NetworkButtons _buttonsPrevious { get; set; }
        [Networked] private TickTimer _shootCooldown { get; set; }

        private float _shootDelay = .2f; // delay before shooting
        private float launchOffset;
        private Coroutine _shootCoroutine;
        private bool _initialDelayApplied = false;

        [SerializeField]
        private NetworkProjectileLoader _projectileLoader;

        [Networked] private int _fireCount { get; set; }
        [Networked] private Vector3 _networkAimPoint { get; set; }
        [Networked] private Vector3 _networkAimDirection { get; set; }
        [Networked] private NetworkId _lockedTargetId { get; set; }
        private int _currentFireCount;

        public override void Spawned()
        {
            _playerManager = GetComponent<PlayerManager>();
        }
        
        // sets the loaded weapons info for the weapons set projectile
        public void SetWeaponInfo()
        {
            var playerWeapon = _playerManager._playerStats.playerWeapon;
            _projectile = playerWeapon.projectile;
            _shootDelay = playerWeapon.delayBeforeShooting;
            launchOffset = playerWeapon.launchOffset;
            _projectileLoader.SetProjectileObject(_projectile); // setting the projectile prefab from the weapon data
        }

        public override void Render()
        {
            // Phase 3C controller handles mounted weapon aiming/effects.
            if (GetComponent<MountedWeaponCombatController>() != null)
                return;
            if (!_playerManager.PlayerSetUp) return;

            var weaponModel = _playerManager._playerVisuals.weaponModelComponent;
            var weaponConfig = _playerManager._playerStats.playerWeapon;
            if (weaponModel != null && weaponConfig != null)
                weaponModel.AimAt(_networkAimPoint, weaponConfig, Time.deltaTime);

            // check if a new shot was fired // this allows effects like muzzle effect to show on all clients
            if (_currentFireCount < _fireCount)
            {
                var fxPlayer = _playerManager._playerVisuals.fxPlayer;
                AudioClip shootFx = null;

                if (_playerManager._playerStats.playerWeapon != null)
                    shootFx = _playerManager._playerStats.playerWeapon.shootFx;

                if (_playerManager._playerVisuals.weaponModelComponent != null &&
                    _playerManager._playerVisuals.weaponModelComponent.muzzleEffect != null)
                    _playerManager._playerVisuals.weaponModelComponent.muzzleEffect.Play();

                if (fxPlayer != null && shootFx != null)
                {
                    _playerManager._playerVisuals.fxPlayer.PlayOneShot(shootFx);
                }

                if (Object.HasInputAuthority)
                {
                    // trigger camera shake
                    if (cameraFollow == null)
                    {
                        cameraFollow = Camera.main.GetComponent<CameraFollow>();
                    }

                    if (cameraFollow != null && cameraFollow.useCameraShake)
                    {
                        cameraFollow.StartShake();
                    }
                }
            }

            // keep track of fire count for proxies
            _currentFireCount = _fireCount;

            // only local player should see the aiming UI
            if (Object.HasInputAuthority)
                _playerManager._playerVisuals.aimingVisual.SetActive(_playerManager._playerStats.IsShooting);

        }

        public override void FixedUpdateNetwork()
        {
            // Phase 3C: MountedWeaponCombatController owns Top/Left/Right firing.
            if (GetComponent<MountedWeaponCombatController>() != null)
                return;
            if (Runner.TryGetInputForPlayer<PlayerNetworkInputData>(Object.InputAuthority, out var inputData) && _playerManager._playerStats.AcceptPlayerInput
                && _playerManager._playerStats.CanShoot && !_playerManager._matchManager.GameIsOver)
            {
                _networkAimPoint = inputData.aimPoint;
                _networkAimDirection = inputData.aimDirection;
                _lockedTargetId = inputData.lockedTargetId;
                ShootWeapon(inputData);
            }

            if (_playerManager._matchManager.GameIsOver)
            {
                _playerManager._playerStats.IsShooting = false;
            }
        }

        // called when players weapon is changed
        public void OnWeaponChanged()
        {
            // stop shooting
            _playerManager._playerStats.IsShooting = false;

            // stop any shooting coroutine
            if (_shootCoroutine != null)
            {
                StopCoroutine(_shootCoroutine);
                _shootCoroutine = null;
            }

            // reset the initial delay flag
            _initialDelayApplied = false;

            // reset shoot cooldown
            _shootCooldown = TickTimer.None;

            // reset fire count
            _fireCount = 0;
            _currentFireCount = 0;
        }

        // bot's shoot method
        public void BotShoot(PlayerNetworkInputData inputData)
        {
            _networkAimPoint = inputData.aimPoint;
            _networkAimDirection = inputData.aimDirection;
            _lockedTargetId = inputData.lockedTargetId;
            ShootWeapon(inputData);
        }

        // players shoot method
        private void ShootWeapon(PlayerNetworkInputData input)
        {
            _playerManager._playerStats.IsShooting = false;

            // an extra check here to stop the shooting coroutine if player is reloading
            if (_playerManager._playerStats.IsReloading || !_playerManager._playerStats.CanShoot)
            {
                if (_shootCoroutine != null)
                {
                    StopCoroutine(_shootCoroutine);
                    _shootCoroutine = null;
                }

                // reset the initial delay flag
                _initialDelayApplied = false;
            }

            // prevent shooting if not allowed
            if (!_playerManager._playerStats.CanShoot || !_playerManager._playerStats.AcceptPlayerInput)
            {
                _playerManager._playerStats.IsShooting = false;
                return;
            }

            var weapon = _playerManager._playerStats.playerWeapon;
            if (weapon != null && weapon.aimMode == WeaponAimMode.LockOnHoming &&
                _lockedTargetId == default && weapon.noLockBehaviour == NoLockFireBehaviour.PreventFire)
            {
                _playerManager._playerStats.IsShooting = false;
                return;
            }

            // if holding down the shoot button
            if (input.networkButtons.IsSet(NetInputButtons.Shoot))
            {
                if (_playerManager._playerStats.LoadedAmmo > 0 && !_playerManager._playerStats.IsReloading)
                {
                    _playerManager._playerStats.IsShooting = true;
                    if (_initialDelayApplied)
                    {
                        LaunchProjectileWithLoader();
                    }
                    else
                    {
                        // start the Coroutine for the initial shooting delay
                        if (_shootCoroutine == null)
                        {
                            _shootCoroutine = StartCoroutine(ShootAfterInitialDelay());
                        }
                    }
                }
                else
                {
                    //Debug.Log("No ammo loaded, need to reload."); uncomment for debugging
                    _playerManager._playerStats.StartReloading();
                    _playerManager._playerStats.IsShooting = false;
                }
            }
            else
            {
                _playerManager._playerStats.IsShooting = false;

                // stop the shooting coroutine if the shoot button is released
                if (_shootCoroutine != null)
                {
                    StopCoroutine(_shootCoroutine);
                    _shootCoroutine = null;
                }

                // reset the initial delay flag
                _initialDelayApplied = false;
            }

            _buttonsPrevious = input.networkButtons;
        }

        private IEnumerator ShootAfterInitialDelay()
        {
            yield return new WaitForSeconds(_shootDelay); // waiting for the shoot delay this allows proper aiming and animation to play       

            LaunchProjectileWithLoader(); // the method we use to launch the projectile

            _initialDelayApplied = true; // sets the flag to indicate the initial delay has been applied
            _shootCoroutine = null; // reset coroutine reference after shooting

        }

        // Clients use this to mitigate lag when spawning projectiles.
        private void LaunchProjectileWithLoader()
        {
            if (_shootCooldown.ExpiredOrNotRunning(Runner) == false)
                return;

            WeaponConfig weapon = _playerManager._playerStats.playerWeapon;
            if (weapon == null)
                return;

            int ammoUseAmount = weapon.ammoAmountShotUse;
            WeaponModelComponent model = _playerManager._playerVisuals.weaponModelComponent;

            Vector3 launchPosition;
            Vector3 muzzleForward;

            if (model != null)
            {
                launchPosition = model.GetLaunchPosition(weapon.launchOffset);
                muzzleForward = model.GetMuzzleForward();
            }
            else
            {
                Transform fallback = projectileLauncher != null && projectileLauncher.launcherTransform != null
                    ? projectileLauncher.launcherTransform
                    : projectileLauncher.transform;

                launchPosition = fallback.position + fallback.forward * weapon.launchOffset;
                muzzleForward = fallback.forward;
            }

            Vector3 launchForward = ResolveLaunchDirection(weapon, launchPosition, muzzleForward);
            Quaternion launchRotation = Quaternion.LookRotation(launchForward);

            if (weapon.isShotgun)
            {
                for (int i = 0; i < weapon.shotgunPelletCount; i++)
                {
                    float horizontalAngle = Random.Range(-weapon.shotSpreadAngleHorizontal * 0.5f,
                                                         weapon.shotSpreadAngleHorizontal * 0.5f);
                    float verticalAngle = Random.Range(-weapon.shotSpreadAngleVertical * 0.5f,
                                                       weapon.shotSpreadAngleVertical * 0.5f);

                    Vector3 pelletDirection = Quaternion.Euler(verticalAngle, horizontalAngle, 0f) * launchForward;
                    var projectile = _projectileLoader.Request<Projectile>(
                        launchPosition, Quaternion.LookRotation(pelletDirection), Object.InputAuthority);

                    if (projectile != null)
                    {
                        projectile.ShootProjectile(
                            launchPosition,
                            pelletDirection,
                            Object.InputAuthority,
                            _playerManager._networkObject,
                            weapon,
                            _playerManager._playerController,
                            _lockedTargetId);
                    }
                }
            }
            else
            {
                var projectile = _projectileLoader.Request<Projectile>(
                    launchPosition, launchRotation, Object.InputAuthority);

                if (projectile != null)
                {
                    projectile.ShootProjectile(
                        launchPosition,
                        launchForward,
                        Object.InputAuthority,
                        _playerManager._networkObject,
                        weapon,
                        _playerManager._playerController,
                        _lockedTargetId);
                }
            }

            _fireCount++;
            _playerManager._playerStats.LoadedAmmo -= ammoUseAmount;
            _shootCooldown = TickTimer.CreateFromSeconds(Runner, weapon.delayBetweenShots);
        }

        private Vector3 ResolveLaunchDirection(WeaponConfig weapon, Vector3 launchPosition, Vector3 muzzleForward)
        {
            switch (weapon.aimMode)
            {
                case WeaponAimMode.Fixed:
                    return muzzleForward.normalized;

                case WeaponAimMode.PitchOnly:
                case WeaponAimMode.YawAndPitch:
                    return muzzleForward.normalized;

                case WeaponAimMode.LockOnHoming:
                    if (_lockedTargetId != default && Runner.TryFindObject(_lockedTargetId, out NetworkObject target))
                    {
                        Vector3 targetPoint = target.transform.position + Vector3.up * weapon.homingTargetOffsetY;
                        return (targetPoint - launchPosition).normalized;
                    }

                    if (weapon.noLockBehaviour == NoLockFireBehaviour.FireUnguided)
                        return muzzleForward.normalized;

                    return (_networkAimPoint - launchPosition).normalized;

                case WeaponAimMode.CameraRaycast:
                default:
                    Vector3 direction = _networkAimPoint - launchPosition;
                    if (direction.sqrMagnitude <= 0.0001f)
                        direction = _networkAimDirection.sqrMagnitude > 0.0001f
                            ? _networkAimDirection
                            : muzzleForward;
                    return direction.normalized;
            }
        }
    }
}
