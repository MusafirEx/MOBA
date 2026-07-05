using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class SpawnPoint : MonoBehaviour
    {
        // The size of the spawn area
        public Vector2 spawnAreaSize = new Vector2(5, 5);  // adjust as needed
        public bool teamSpawnPointRed;
        public bool teamSpawnPointBlue;
        public bool pickUpSpawnPoint;

        private void OnDrawGizmos()
        {
            // set the gizmo color to distinguish it
            if (teamSpawnPointRed)
            {
                Gizmos.color = Color.red;
            }
            else if (teamSpawnPointBlue)
            {
                Gizmos.color = Color.cyan;
            }
            else if (pickUpSpawnPoint)
            {
                Gizmos.color = Color.yellow;
            }
            else
            {
                Gizmos.color = Color.green;
            }

            // draw a wire cube that represents the spawn area
            Gizmos.DrawWireCube(transform.position + (Vector3.up * 1f), new Vector3(spawnAreaSize.x, 0.1f, spawnAreaSize.y));
        }

    }
}

