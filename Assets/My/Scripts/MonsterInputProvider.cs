using UnityEngine;

/// <summary>
/// Static local input collection. FusionLauncher calls this in OnInput().
/// Replace old Input Manager with Input System later if needed.
/// </summary>
public class MonsterInputProvider : MonoBehaviour
{
    public static MonsterInputData CollectInput()
    {
        MonsterInputData data = new MonsterInputData();

        data.Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        data.Look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        data.AimWorldPoint = GetMouseAimPoint();

        var buttons = new Fusion.NetworkButtons();
        buttons.Set((int)MonsterButtons.FireMain, Input.GetMouseButton(0));
        buttons.Set((int)MonsterButtons.FireSub, Input.GetMouseButton(1));
        buttons.Set((int)MonsterButtons.Dash, Input.GetKey(KeyCode.LeftShift));
        buttons.Set((int)MonsterButtons.Jump, Input.GetKey(KeyCode.Space));
        buttons.Set((int)MonsterButtons.Skill1, Input.GetKey(KeyCode.Q));
        buttons.Set((int)MonsterButtons.Skill2, Input.GetKey(KeyCode.E));
        buttons.Set((int)MonsterButtons.LockOn, Input.GetKey(KeyCode.R));
        data.Buttons = buttons;

        return data;
    }

    private static Vector3 GetMouseAimPoint()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            return hit.point;

        return ray.origin + ray.direction * 100f;
    }
}
