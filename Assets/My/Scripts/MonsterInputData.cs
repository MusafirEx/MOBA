using Fusion;
using UnityEngine;

public struct MonsterInputData : INetworkInput
{
    public Vector2 Move;
    public Vector2 Look;
    public Vector3 AimWorldPoint;
    public NetworkButtons Buttons;

    public bool IsDown(MonsterButtons button)
    {
        return Buttons.IsSet((int)button);
    }
}
