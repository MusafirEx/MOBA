/*
 * Copyright (c) 2024 VAUXLAND
 * Part of the "Fusion Shooter Brawler" Asset.
 * You shall not license, sublicense, sell, resell, transfer, assign, distribute or
 * otherwise make available to any third party the Service or the Content of this Asset.
 * Use of this asset is governed by the Unity Asset Store End User License Agreement.
 * See https://unity3d.com/legal/as_terms for more information.
 */

using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vauxland.FusionBrawler
{
    public class PlayerVisualsController : NetworkBehaviour
    {
        [Header("StatUIElements")]
        public TMP_Text playerNickNameText; // the floating name text ui above the player object
        public GameObject hpHolder; // the UI object above the player object that holds the fills of HP and BodyArmor
        public GameObject reloadBar; // the floating yellow image fill object above the player objec that sets active when reloading
        public Image hpFill; // the floating red image fill ui above the player object that shows the players current health amount
        public Image bodyArmorFill; // the floating blue image fill ui above the player object that shows the players current body armor amount
        public Image reloadFill; // the fill image of the reload bar
        public TMP_Text hpAmount; // the text UI above the player object that shows the HP Amount

        [Header("EmotesSetup")]
        public GameObject emoteHolder; // the players emotes ui holder where the emotes show when used
        public List<GameObject> emoteImages; // list of emote images
        [Networked] private TickTimer emoteTimer { get; set; } // timer to hide the emote after a duration
        [Networked] private int currentEmoteIndex { get; set; } // the current emote index being displayed

        [Header("PlayerEffects")]
        public GameObject deathEffect; // the effect that plays when the player dies
        public GameObject shieldEffect; // the shield effect that plays when the player gets a shield on respawn
        public AudioSource fxPlayer; // the audio source to play sound effects from the player

        [Header("PlayerSetup")]
        [HideInInspector] public CharacterModelConfig playerModel; // the players model to show
        [HideInInspector] public WeaponModelComponent weaponModelComponent; // the weapon model
        public Transform effectPlayer; // the transform you can use to spawm effects from on the player
        public Transform playerModelHolder; // the transform where the selected player model will spawn

        public GameObject playerUIHolder; // the players floating UI holder to control the visibility when entering and exiting hiding zones
        public GameObject aimingVisual; // the players aiming UI when shooting
        public GameObject noTeamVisual; // the no team UI visual seen in deathmatch
        public GameObject[] teamRedVisuals; // team reds visuals when on team red
        public GameObject[] teamBlueVisuals; // team blues visuals when on team blue

        [Header("World Space Player UI")]
        [Tooltip("Root transform containing the floating HP/name UI. If empty, Player UI Holder is used.")]
        public Transform worldSpaceUIRoot;
        [Tooltip("Enable this when the front of the world-space canvas points along local -Z.")]
        public bool worldSpaceCanvasFacesNegativeZ = true;

        private Animator cacheAnimator; // the players character model configs' animator
        protected PlayerManager _playerManager;
        protected PlayerMovementManager _playerMovement;
        protected MonsterWeaponLoadout _weaponLoadout;
        private Camera mainCamera;

        [HideInInspector][Networked] public NetworkBool AnimatorSet { get; set; }

        public override void Spawned()
        {
            _playerManager = GetComponent<PlayerManager>();
            _playerMovement = GetComponent<PlayerMovementManager>();
            _weaponLoadout = GetComponent<MonsterWeaponLoadout>();
            mainCamera = Camera.main;
            AnimatorSet = false;

            if (worldSpaceUIRoot == null)
            {
                if (playerUIHolder != null)
                    worldSpaceUIRoot = playerUIHolder.transform;
                else if (hpHolder != null)
                    worldSpaceUIRoot = hpHolder.transform.parent;
            }

            ApplyWorldSpaceUIVisibility();
        }

        // sets our models for the selected character, weapon and cosmetic
        public virtual void OnSetPlayerModel()
        {
            if (_playerManager == null || _playerManager._playerStats == null)
                return;

            var weaponConfig = _playerManager._playerStats.playerWeapon;
            var characterConfig = _playerManager._playerStats.playerCharacter;
            var cosmeticConfig = _playerManager._playerStats.playerCosmetic;

            if (characterConfig == null || characterConfig.characterModel == null || playerModelHolder == null)
                return;

            // Character, weapon, cosmetic and mounted-loadout synchronization can all request
            // a visual refresh during the same frame. Remove every previously spawned model
            // from the holder before creating the replacement so only one visible model remains.
            ClearExistingPlayerModels();

            playerModel = Instantiate(characterConfig.characterModel, playerModelHolder);
            if (playerModel != null)
            {
                playerModel.ClearAllWeapons();

                if (_weaponLoadout != null && _weaponLoadout.IsReady)
                {
                    WeaponModelComponent top = AttachMountedWeapon(_weaponLoadout.GetWeapon(WeaponMountType.Top), characterConfig);
                    WeaponModelComponent left = AttachMountedWeapon(_weaponLoadout.GetWeapon(WeaponMountType.Left), characterConfig);
                    WeaponModelComponent right = AttachMountedWeapon(_weaponLoadout.GetWeapon(WeaponMountType.Right), characterConfig);
                    weaponModelComponent = top != null ? top : (left != null ? left : right);
                }
                else if (weaponConfig != null)
                {
                    // Temporary fallback while the three networked IDs are synchronizing.
                    // MonsterWeaponLoadout.Render() rebuilds the model as soon as LoadoutReady becomes true.
                    weaponModelComponent = playerModel.SetWeaponConfig(weaponConfig, characterConfig);
                }
            }
            if (cosmeticConfig != null && playerModel != null)
                playerModel.SetCosmeticModel(cosmeticConfig.cosmeticModel);
            if (playerModel != null && weaponModelComponent == null)
                weaponModelComponent = playerModel.GetComponentInChildren<WeaponModelComponent>();

            if (playerModel != null)
            {
                playerModel.gameObject.SetActive(true);
                if (Object.HasInputAuthority)
                {
                    RpcSetUpAnimator();
                }

                SetUpAnimator();

            }
        }


        private void ClearExistingPlayerModels()
        {
            AnimatorSet = false;
            cacheAnimator = null;
            weaponModelComponent = null;

            if (playerModelHolder == null)
            {
                playerModel = null;
                return;
            }

            // The spawned character model is expected to be a direct child of playerModelHolder.
            // Detach and disable it immediately, then use Destroy for normal runtime cleanup.
            // This prevents several model refreshes in one frame from appearing together.
            for (int i = playerModelHolder.childCount - 1; i >= 0; i--)
            {
                Transform child = playerModelHolder.GetChild(i);
                if (child == null)
                    continue;

                CharacterModelConfig existingModel = child.GetComponent<CharacterModelConfig>();
                if (existingModel == null)
                    continue;

                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            playerModel = null;
        }


        private WeaponModelComponent AttachMountedWeapon(WeaponConfig config, CharacterConfig character)
        {
            if (config == null || playerModel == null)
                return null;

            return playerModel.SetWeaponConfig(config, character);
        }


        public WeaponModelComponent GetMountedWeaponModel(WeaponMountType mountType)
        {
            if (playerModel == null)
                return null;

            WeaponMountPoint mount = playerModel.GetMount(mountType);
            return mount != null ? mount.currentWeaponModel : null;
        }

        // sets up the character models animator
        protected virtual void SetUpAnimator()
        {
            if (playerModel == null)
                return;

            cacheAnimator = playerModel._cacheAnimator;

            if (cacheAnimator == null)
                return;

            AnimatorSet = true;
        }

        public override void Render()
        {
            if (AnimatorSet)
            {
                UpdatePlayerAnims();
            }

            // Enforce local/remote UI visibility every render because respawn,
            // hiding zones, and setup scripts may enable the objects again.
            ApplyWorldSpaceUIVisibility();

            if (_playerManager._playerStats.PlayerIsReady)
            {
                if (Object.HasInputAuthority)
                {
                    UpdateLocalMatchUI();
                }
                else
                {
                    UpdateWorldSpaceUI();
                    FaceWorldSpaceUIToCamera();
                }
            }

            UpdateEmoteDisplay();
        }

        // Updates floating health UI for remote players only.
        private void UpdateWorldSpaceUI()
        {
            if (hpAmount != null)
                hpAmount.text = _playerManager._playerStats.Hp.ToString();

            if (hpFill != null)
            {
                hpFill.fillAmount = _playerManager._playerStats.TotalHp > 0
                    ? Mathf.Clamp01(
                        _playerManager._playerStats.Hp /
                        (float)_playerManager._playerStats.TotalHp)
                    : 0f;
            }

            if (bodyArmorFill != null)
            {
                bodyArmorFill.fillAmount =
                    _playerManager._playerStats.TotalBodyArmor > 0
                    ? Mathf.Clamp01(
                        _playerManager._playerStats.BodyArmor /
                        (float)_playerManager._playerStats.TotalBodyArmor)
                    : 0f;
            }
        }

        // Updates the local player's health in MatchUIHandler.
        private void UpdateLocalMatchUI()
        {
            if (_playerManager._matchUI == null)
                return;

            _playerManager._matchUI.UpdateLocalPlayerHealth(
                _playerManager._playerStats.Hp,
                _playerManager._playerStats.TotalHp,
                _playerManager._playerStats.BodyArmor,
                _playerManager._playerStats.TotalBodyArmor);
        }

        private void ApplyWorldSpaceUIVisibility()
        {
            bool showFloatingUI =
                !Object.HasInputAuthority &&
                _playerManager != null &&
                _playerManager._playerStats != null &&
                !_playerManager._playerStats.IsRespawning;

            if (playerUIHolder != null)
                playerUIHolder.SetActive(showFloatingUI);

            if (hpHolder != null)
                hpHolder.SetActive(showFloatingUI);

            if (playerNickNameText != null)
                playerNickNameText.gameObject.SetActive(showFloatingUI);

            // The old floating reload bar must also be hidden for the local player.
            if (reloadBar != null && Object.HasInputAuthority)
                reloadBar.SetActive(false);
        }

        // Makes remote players' world-space UI face the local main camera.
        private void FaceWorldSpaceUIToCamera()
        {
            if (Object.HasInputAuthority)
                return;

            if (mainCamera == null || !mainCamera.isActiveAndEnabled)
                mainCamera = Camera.main;

            if (mainCamera == null)
                return;

            Transform uiRoot = worldSpaceUIRoot;

            if (uiRoot == null)
            {
                if (playerUIHolder != null)
                    uiRoot = playerUIHolder.transform;
                else if (hpHolder != null)
                    uiRoot = hpHolder.transform.parent != null
                        ? hpHolder.transform.parent
                        : hpHolder.transform;
            }

            if (uiRoot == null)
                return;

            Vector3 towardCamera =
                mainCamera.transform.position - uiRoot.position;

            if (towardCamera.sqrMagnitude <= 0.0001f)
                return;

            Quaternion lookRotation =
                Quaternion.LookRotation(towardCamera.normalized, Vector3.up);

            // Most Unity world-space canvases display their front on local -Z.
            if (worldSpaceCanvasFacesNegativeZ)
                lookRotation *= Quaternion.Euler(0f, 180f, 0f);

            uiRoot.rotation = lookRotation;
        }

        private void UpdateEmoteDisplay()
        {
            // hide all emote images
            foreach (var emote in emoteImages)
            {
                emote.SetActive(false);
            }

            if (emoteTimer.ExpiredOrNotRunning(Runner))
            {
                return;
            }

            // show the current emote
            if (currentEmoteIndex >= 0 && currentEmoteIndex < emoteImages.Count)
            {
                emoteImages[currentEmoteIndex].SetActive(true);
            }
        }

        // updates the player's animations
        protected virtual void UpdatePlayerAnims()
        {
            if (cacheAnimator == null) return;

            if (Runner.IsForward == false)
                return;

            if (_playerManager._playerStats.Hp > 0)
            {
                Vector3 velocity = _playerMovement.GetCurrentVelocity();
                var xzMagnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
                cacheAnimator.SetBool("IsDead", false);
                cacheAnimator.SetFloat("MoveSpeed", xzMagnitude);
                cacheAnimator.SetBool("IsShooting", _playerManager._playerStats.IsShooting && _playerManager._playerStats.CanShoot);
            }

            cacheAnimator.SetInteger("AnimID", _playerManager._playerStats.playerWeapon != null ? _playerManager._playerStats.playerWeapon.animID : 0);

            cacheAnimator.SetBool("IsIdle", !cacheAnimator.GetBool("IsDead") || !cacheAnimator.GetBool("IsShooting"));
        }

        // changes the visibility of the player
        public void SetVisible(bool isVisible)
        {
            if (playerModel != null)
            {
                playerModel.gameObject.SetActive(isVisible);
            }
            if (!_playerManager._playerStats.IsRespawning)
            {
                bool showWorldSpaceUI =
                    isVisible && !Object.HasInputAuthority;

                if (hpHolder != null)
                    hpHolder.SetActive(showWorldSpaceUI);

                if (playerNickNameText != null)
                    playerNickNameText.gameObject.SetActive(showWorldSpaceUI);

                if (playerUIHolder != null)
                    playerUIHolder.SetActive(showWorldSpaceUI);
            }

            // Reapply because local floating UI must never be enabled.
            ApplyWorldSpaceUIVisibility();
           
        }

        // adjust our Health bar based on the damage taken
        public void PlayerHpEffected(int oldHp, int newHp, int totalHp)
        {
            //Debug.Log($"[Host: {Object.HasStateAuthority}] PlayerHpEffected called. Old HP: {oldHp}, New HP: {newHp}, Total HP: {totalHp}");

            // Debug.Log($"PlayerHpEffected called. Old HP: {oldHp}, New HP: {newHp}, Total HP: {totalHp}"); // can test with these debugs if you need to

            if (cacheAnimator == null)
                return;

            if (newHp > 0 && oldHp <= 0)
            {
                // player has just respawned
                cacheAnimator.SetBool("IsDead", false);
            }
            else if (newHp <= 0 && oldHp > 0)
            {
                // player has just died
                cacheAnimator.SetBool("IsDead", true);
                cacheAnimator.SetFloat("MoveSpeed", 0);
                cacheAnimator.SetBool("IsShooting", false);
                cacheAnimator.SetBool("IsIdle", false);
            }

            if (newHp != oldHp)
            {
                if (Object.HasInputAuthority)
                {
                    UpdateLocalMatchUI();
                }
                else
                {
                    if (hpAmount != null)
                        hpAmount.text = newHp.ToString();

                    if (hpFill != null)
                    {
                        hpFill.fillAmount = totalHp > 0
                            ? Mathf.Clamp01(newHp / (float)totalHp)
                            : 0f;
                    }
                }

                if (newHp < oldHp)
                    PlayerReceiveDamageEffects();
            }
        }

        private void PlayerReceiveDamageEffects()
        {
            // play damage effects here, sounds etc.
        }

        public void PlayBodyArmorDamageEffects()
        {
            // play bodyarmor loss effects and sounds here
        }

        public void ShowEmote(int emoteIndex)
        {
            if (Object.HasInputAuthority)
            {
                RpcShowEmote(emoteIndex);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        public void RpcShowEmote(int emoteIndex)
        {
            currentEmoteIndex = emoteIndex;
            emoteTimer = TickTimer.CreateFromSeconds(Runner, 3f);
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        private void RpcSetUpAnimator()
        {
            SetUpAnimator();
        }
    }
}

