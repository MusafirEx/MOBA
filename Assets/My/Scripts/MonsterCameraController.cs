using UnityEngine;

public class MonsterCameraController : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 6f, -10f);
    public float followSpeed = 10f;
    public float rotateSensitivity = 3f;

    private float _yaw;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) _yaw = target.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        _yaw += Input.GetAxis("Mouse X") * rotateSensitivity;
        Quaternion rotation = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 desiredPos = target.position + rotation * offset;

        transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 2f);
    }
}
