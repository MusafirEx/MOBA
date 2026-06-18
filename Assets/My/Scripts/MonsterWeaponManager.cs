using Fusion;
using UnityEngine;

public class MonsterWeaponManager : NetworkBehaviour
{
    public ProjectileWeapon mainWeapon;
    public ProjectileWeapon subWeapon;

    public void ProcessInput(MonsterInputData input)
    {
        if (!Object.HasStateAuthority) return;

        if (input.IsDown(MonsterButtons.FireMain) && mainWeapon != null)
            mainWeapon.TryFire(input.AimWorldPoint);

        if (input.IsDown(MonsterButtons.FireSub) && subWeapon != null)
            subWeapon.TryFire(input.AimWorldPoint);
    }
}
