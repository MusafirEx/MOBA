using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Vauxland.FusionBrawler
{
    public class NetworkObjectPooler : NetworkObjectProviderDefault
    {
        [Tooltip("Place network objects you want pooled. Leave it empty to pool all network objects.")]
        [SerializeField]
        private List<NetworkObject> _poolableObjects = new();


        private Dictionary<NetworkObjectTypeId, Queue<NetworkObject>> _objectPools = new();

        // create the objects
        protected override NetworkObject InstantiatePrefab(NetworkRunner runner, NetworkObject prefab)
        {
            if (CanBePooled(prefab))
            {
                var instance = RetrieveFromPool(prefab);
                if (instance != null)
                {
                    instance.transform.position = Vector3.zero;
                    SetParent(instance.transform);
                    return instance;
                }

                var newInstance = Instantiate(prefab);
                SetParent(newInstance.transform);
                return newInstance;
            }
            else
            {
                return Instantiate(prefab);
            }
            
        }

        protected override void DestroyPrefabInstance(NetworkRunner runner, NetworkPrefabId prefabId, NetworkObject instance)
        {
            if (_objectPools.TryGetValue(prefabId, out var pool))
            {
                instance.gameObject.SetActive(false);
                SetParent(instance.transform);// new
                pool.Enqueue(instance);
            }
            else
            {
                Destroy(instance.gameObject);
            }
        }
        // grab objects in the pool
        private NetworkObject RetrieveFromPool(NetworkObject prefab)
        {
            NetworkObject instance = null;

            if (_objectPools.TryGetValue(prefab.NetworkTypeId, out var pool) && pool.Count > 0)
            {
                instance = pool.Dequeue();
                instance.gameObject.SetActive(true);
                SetParent(instance.transform);
                return instance;
            }

            return CreateNewInstance(prefab);
        }
        // create new objects if not enough in pool
        private NetworkObject CreateNewInstance(NetworkObject prefab)
        {
            var instance = Instantiate(prefab);
            SetParent(instance.transform);
            if (!_objectPools.ContainsKey(prefab.NetworkTypeId))
            {
                _objectPools[prefab.NetworkTypeId] = new Queue<NetworkObject>();
            }

            return instance;
        }

        private bool CanBePooled(NetworkObject prefab)
        {
            return _poolableObjects.Count == 0 || _poolableObjects.Contains(prefab);
        }

        // keeps the objects under the pooled objects parent in the scene to keel the hierarchy clean
        private void SetParent(Transform instanceTransform)
        {
            var pooledObjectsParent = PooledObjectsManager.GetPooledObjectsParent();
            instanceTransform.SetParent(pooledObjectsParent);
        }
    }
}