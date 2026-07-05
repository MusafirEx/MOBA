using UnityEngine;
using Fusion;

// This script is based off of photon fusion's 'NetworkObjectBuffer' in their projectiles essential example
// You can learn more about projectile handling practices in their examples and documentation

namespace Vauxland.FusionBrawler
{
    public class NetworkProjectileLoader : NetworkBehaviour
    {
        // total pool size
        public const int MAX_POOL_SIZE = 5;
   
        [SerializeField]
        private NetworkObject _objectPrefab; // the projectile to pool which is set in 'SetProjectileObject'
        [SerializeField, Range(1, MAX_POOL_SIZE)] // adjustable amount of objects to spawn at first limited by max pool size
        private int _poolSize = MAX_POOL_SIZE; 

        [Networked, Capacity(MAX_POOL_SIZE)]
        private NetworkArray<NetworkObject> _objectPool { get; }
        [Networked]
        private int _nextObjectIndex { get; set; }

        private NetworkObject[] _localObjects = new NetworkObject[MAX_POOL_SIZE];

        
        // called from the projectile controller sets the current weapons projectile
        public void SetProjectileObject(NetworkObject obj)
        {
            EmptyPool();
            _objectPrefab = obj;
            ReplenishPool();
        }

        // grab from the preloaded projectiles
        public T Request<T>(Vector3 position, Quaternion rotation, PlayerRef inputAuthority) where T : NetworkBehaviour
        {
            var obj = Request(position, rotation, inputAuthority);
            return obj != null ? obj.GetComponent<T>() : null;
        }

        public NetworkObject Request(Vector3 position, Quaternion rotation, PlayerRef inputAuthority)
        {
            var obj = _objectPool[_nextObjectIndex];

            if (obj == null)
                return null;

            Runner.SetIsSimulated(obj, true);
            obj.AssignInputAuthority(inputAuthority);

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.gameObject.SetActive(true);


            // keep the projectile in the pooled object parent
            SetParent(obj.transform);

            _objectPool.Set(_nextObjectIndex, null);
            ReplenishPool();

            _nextObjectIndex = (_nextObjectIndex + 1) % _poolSize;

            return obj;
        }
        // despawn the pooled objects
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            EmptyPool();
        }

        public override void Render()
        {
            if (HasStateAuthority == true) return;

            for (int i = 0; i < _poolSize; i++)
            {
                var networkedObject = _objectPool[i];
                var localObject = _localObjects[i];

                if (localObject == networkedObject)
                    continue;

                if (localObject != null && localObject.IsValid == true)
                {
                    localObject.gameObject.SetActive(true);
                    SetParent(localObject.transform);

#if UNITY_EDITOR
                    localObject.name = _objectPrefab.name;
#endif
                }

                _localObjects[i] = networkedObject;

                if (networkedObject != null)
                {
                    networkedObject.gameObject.SetActive(false);
                    SetParent(networkedObject.transform); // add to pooled parent when inactive

#if UNITY_EDITOR
                    networkedObject.name = $"(Pooled) {networkedObject.name}";
#endif
                }
            }
        }

        // add more projectiles to the pool
        private void ReplenishPool()
        {
            if (HasStateAuthority == false)
                return;

            for (int i = 0; i < _poolSize; i++)
            {
                if (_objectPool[i] == null)
                {
                    _objectPool.Set(i, CreateNewObject());
                }
            }
        }

        // clear our pool
        private void EmptyPool()
        {
            if (HasStateAuthority == false)
            {
                System.Array.Clear(_localObjects, 0, _localObjects.Length);
                return;
            }

            for (int i = 0; i < _poolSize; i++)
            {
                if (_objectPool[i] != null)
                {
                    Runner.Despawn(_objectPool[i]);
                }
            }

            _objectPool.Clear();
        }

        private NetworkObject CreateNewObject()
        {
            var obj = Runner.Spawn(_objectPrefab, new Vector3(0f, -1000f, 0f));

            Runner.SetIsSimulated(obj, false);
            obj.gameObject.SetActive(false);
            SetParent(obj.transform);
            return obj;
        }

        // method to set the parent to the pooled objects parent // keeps the hierarchy clean
        private void SetParent(Transform instanceTransform)
        {
            var pooledObjectsParent = PooledObjectsManager.GetPooledObjectsParent();
            instanceTransform.SetParent(pooledObjectsParent);
        }

        // overloaded method to set a specific parent or remove parent when passing null
        private void SetParent(Transform instanceTransform, Transform newParent)
        {
            instanceTransform.SetParent(newParent);
        }
    }

}
