using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class UIOrientationManager : MonoBehaviour
    {
        // this class keeps our players UI above the player object oriented correctly

        private Vector3 _InitialPosition;
        private Quaternion _InitialRotation;

        private void Start()
        {
            _InitialPosition = transform.localPosition;
            _InitialRotation = transform.localRotation;
        }

        public void LateUpdate()
        {
            transform.rotation = _InitialRotation;

            transform.position = transform.parent.position + _InitialPosition;
        }
    }

}

