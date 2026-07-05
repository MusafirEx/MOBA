/*
 * Modified network input for third-person aiming and target lock.
 */
using Fusion;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public struct PlayerNetworkInputData : INetworkInput
    {
        public NetworkButtons networkButtons;

        public Vector3 inputPlayerMove;
        public Vector3 inputPlayerDirection;
        public Vector3 mousePosition;
        public Vector2 inputLook;

        // Separate combat aim data. Movement direction remains WASD-based.
        public Vector3 aimPoint;
        public Vector3 aimDirection;
        public NetworkId lockedTargetId;

        public NetworkBool inputPlayerJump;
        public NetworkBool inputPlayerShoot;
    }

    public enum NetInputButtons
    {
        Shoot,      // Top weapon
        Jump,
        Boost,
        FireLeft,
        FireRight,
        LockOn
    }
}
