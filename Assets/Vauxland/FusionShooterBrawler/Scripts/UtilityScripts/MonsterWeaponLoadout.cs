using Fusion;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    [RequireComponent(typeof(PlayerManager))]
    public class MonsterWeaponLoadout : NetworkBehaviour
    {
        [Networked] public int TopWeaponID { get; private set; }
        [Networked] public int LeftWeaponID { get; private set; }
        [Networked] public int RightWeaponID { get; private set; }
        [Networked] public NetworkBool LoadoutReady { get; private set; }

        public bool IsReady => LoadoutReady;

        private PlayerManager playerManager;
        private int lastAppliedTop = int.MinValue;
        private int lastAppliedLeft = int.MinValue;
        private int lastAppliedRight = int.MinValue;
        private CharacterConfig lastAppliedCharacter;

        public override void Spawned()
        {
            playerManager = GetComponent<PlayerManager>();

            if (Object.HasStateAuthority)
            {
                TopWeaponID = -1;
                LeftWeaponID = -1;
                RightWeaponID = -1;
                LoadoutReady = false;
            }

            if (Object.HasInputAuthority && PlayerGameData.PlayerData != null)
            {
                RpcSetLoadout(
                    PlayerGameData.PlayerData.GetSelectedTopWeapon(),
                    PlayerGameData.PlayerData.GetSelectedLeftWeapon(),
                    PlayerGameData.PlayerData.GetSelectedRightWeapon());
            }
        }

        public override void Render()
        {
            if (!LoadoutReady || playerManager == null || playerManager._playerVisuals == null)
                return;

            CharacterConfig character =
                playerManager._playerStats != null ? playerManager._playerStats.playerCharacter : null;

            if (character == null)
                return;

            bool changed =
                TopWeaponID != lastAppliedTop ||
                LeftWeaponID != lastAppliedLeft ||
                RightWeaponID != lastAppliedRight ||
                character != lastAppliedCharacter;

            if (!changed)
                return;

            lastAppliedTop = TopWeaponID;
            lastAppliedLeft = LeftWeaponID;
            lastAppliedRight = RightWeaponID;
            lastAppliedCharacter = character;

            playerManager._playerVisuals.OnSetPlayerModel();
        }


        public void EnsureBotLoadout(CharacterConfig character)
        {
            if (!Object.HasStateAuthority || LoadoutReady)
                return;

            TopWeaponID = GetRandomCompatibleWeaponId(WeaponMountType.Top, character);
            LeftWeaponID = GetRandomCompatibleWeaponId(WeaponMountType.Left, character);
            RightWeaponID = GetRandomCompatibleWeaponId(WeaponMountType.Right, character);
            LoadoutReady = true;
        }

        private static int GetRandomCompatibleWeaponId(
            WeaponMountType mountType,
            CharacterConfig character)
        {
            System.Collections.Generic.List<int> candidates =
                new System.Collections.Generic.List<int>();

            foreach (var pair in PlayerGameData.Weapons)
            {
                WeaponConfig weapon = pair.Value;
                if (weapon == null || weapon.requiredMount != mountType)
                    continue;

                if (weapon.compatibleCharacters != null &&
                    weapon.compatibleCharacters.Length > 0)
                {
                    bool found = false;
                    for (int i = 0; i < weapon.compatibleCharacters.Length; i++)
                    {
                        if (weapon.compatibleCharacters[i] == character)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        continue;
                }

                candidates.Add(pair.Key);
            }

            if (candidates.Count == 0)
                return -1;

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        public WeaponConfig GetWeapon(WeaponMountType mountType)
        {
            int id = GetWeaponId(mountType);
            return id >= 0 ? PlayerGameData.GetWeapon(id) : null;
        }

        public int GetWeaponId(WeaponMountType mountType)
        {
            switch (mountType)
            {
                case WeaponMountType.Top: return TopWeaponID;
                case WeaponMountType.Left: return LeftWeaponID;
                case WeaponMountType.Right: return RightWeaponID;
                default: return -1;
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcSetLoadout(int topId, int leftId, int rightId)
        {
            TopWeaponID = Validate(topId, WeaponMountType.Top);
            LeftWeaponID = Validate(leftId, WeaponMountType.Left);
            RightWeaponID = Validate(rightId, WeaponMountType.Right);
            LoadoutReady = true;
        }

        private static int Validate(int id, WeaponMountType mountType)
        {
            WeaponConfig config = id >= 0 ? PlayerGameData.GetWeapon(id) : null;
            return config != null && config.requiredMount == mountType ? id : -1;
        }
    }
}
