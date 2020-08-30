# Custom Spawn Functions

You can use spawn handler functions to customize the default behavior when creating spawned game objects on the client. Spawn handler functions ensure you have full control of how you spawn the game object, as well as how you destroy it.

Use `ClientScene.RegisterSpawnHandler` or `ClientScene.RegisterPrefab` to register functions to spawn and destroy client game objects. The server creates game objects directly, and then spawns them on the clients through this functionality. This functions takes either the asset ID or a prefab and two function delegates: one to handle creating game objects on the client, and one to handle destroying game objects on the client. The asset ID can be a dynamic one, or just the asset ID found on the prefab game object you want to spawn.

The spawn / unspawn delegates will look something like this:

**Spawn Handler**
``` cs
GameObject SpawnDelegate(Vector3 position, System.Guid assetId) 
{
    // do stuff here
}
```
or 
``` cs
GameObject SpawnDelegate(SpawnMessage msg) 
{
    // do stuff here
}
```

**UnSpawn Handler**
```cs
void UnSpawnDelegate(GameObject spawned) 
{
    // do stuff here
}
```

When a prefab is saved its `assetId` field will be automatically set. If you want to create prefabs at runtime you will have to generate a new GUID.

**Generate prefab at runtime**
``` cs
// generate a new unique assetId 
System.Guid creatureAssetId = System.Guid.NewGuid();

// register handlers for the new assetId
ClientScene.RegisterSpawnHandler(creatureAssetId, SpawnCreature, UnSpawnCreature);
```

**Use existing prefab**
```cs
// register prefab you'd like to custom spawn and pass in handlers
ClientScene.RegisterPrefab(coinAssetId, SpawnCoin, UnSpawnCoin);
```

**Spawn on Server**
```cs
// spawn a coin - SpawnCoin is called on client
NetworkServer.Spawn(gameObject, coinAssetId);
```

The spawn functions themselves are implemented with the delegate signature. Here is the coin spawner. The `SpawnCreature` would look the same, but have different spawn logic:

``` cs
public GameObject SpawnCoin(SpawnMessage msg)
{
    return Instantiate(m_CoinPrefab, msg.position, msg.rotation);
}
public void UnSpawnCoin(GameObject spawned)
{
    Destroy(spawned);
}
```

When using custom spawn functions, it is sometimes useful to be able to unspawn game objects without destroying them. This can be done by calling `NetworkServer.UnSpawn`. This causes the object to be `Reset` on the server and sends a `ObjectDestroyMessage` to clients. The `ObjectDestroyMessage` will cause the custom unspawn function to be called on the clients. If there is no unspawn function the object will instead be `Destroy`

Note that on the host, game objects are not spawned for the local client, because they already exist on the server. This also means that no spawn or unspawn handler functions are called.

## Setting Up a Game Object Pool with Custom Spawn Handlers

Here is an example of how you might set up a simple game object pooling system with custom spawn handlers. Spawning and unspawning then puts game objects in or out of the pool.

``` cs
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Mirror.Examples
{
    public class PrefabPoolManager : MonoBehaviour
    {
        [Header("Settings")]
        public int startSize = 5;
        public int maxSize = 20;
        public GameObject prefab;

        [Header("Debug")]
        [SerializeField] Queue<GameObject> pool;
        [SerializeField] int currentCount;


        void Start()
        {
            InitializePool();

            ClientScene.RegisterPrefab(prefab, SpawnHandler, UnspawnHandler);
        }

        void OnDestroy()
        {
            ClientScene.UnregisterPrefab(prefab);
        }

        private void InitializePool()
        {
            pool = new Queue<GameObject>();
            for (int i = 0; i < startSize; i++)
            {
                GameObject next = CreateNew();

                pool.Enqueue(next);
            }
        }

        GameObject CreateNew()
        {
            if (currentCount > maxSize)
            {
                Debug.LogError($"Pool has reached max size of {maxSize}");
                return null;
            }

            // use this object as parent so that objects dont crowd hierarchy
            GameObject next = Instantiate(prefab, transform);
            next.name = $"{prefab.name}_pooled_{currentCount}";
            next.SetActive(false);
            currentCount++;
            return next;
        }

        // used by ClientScene.RegisterPrefab
        GameObject SpawnHandler(SpawnMessage msg)
        {
            return GetFromPool(msg.position, msg.rotation);
        }

        // used by ClientScene.RegisterPrefab
        void UnspawnHandler(GameObject spawned)
        {
            PutBackInPool(spawned);
        }

        /// <summary>
        /// Used to take Object from Pool.
        /// <para>Should be used on server to get the next Object</para>
        /// <para>Used on client by ClientScene to spawn objects</para>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public GameObject GetFromPool(Vector3 position, Quaternion rotation)
        {
            GameObject next = pool.Count > 0
                ? pool.Dequeue() // take from pool
                : CreateNew(); // create new because pool is empty

            // CreateNew might return null if max size is reached
            if (next == null) { return null; }

            // set position/rotation and set active
            next.transform.position = position;
            next.transform.rotation = rotation;
            next.SetActive(true);
            return next;
        }

        /// <summary>
        /// Used to put object back into pool so they can b
        /// <para>Should be used on server after unspawning an object</para>
        /// <para>Used on client by ClientScene to unspawn objects</para>
        /// </summary>
        /// <param name="spawned"></param>
        public void PutBackInPool(GameObject spawned)
        {
            // disable object
            spawned.SetActive(false);

            // add back to pool
            pool.Enqueue(spawned);
        }
    }
}
```

To use this manager, create a new empty game object and add the `PrefabPoolManager` component (code above). Next, drag a prefab you want to spawn multiple times to the Prefab field, and set `startSize` and `maxSize` fields. `startSize` is how many will be spawned when your game starts. `maxSize` is the max number that can be spawned, if this number is reached then an error will be given when trying to more new objects.

Finally, set up a reference to the PrefabPoolManager in the script you are using for player movement:

``` cs
PrefabPoolManager prefabPoolManager;

void Start()
{
    prefabPoolManager = FindObjectOfType<PrefabPoolManager>();
}
```

Your player logic might contain something like this, which moves and fires coins:

``` cs
void Update()
{
    if (!isLocalPlayer)
        return;
    
    // move
    var x = Input.GetAxis("Horizontal") * 0.1f;
    var z = Input.GetAxis("Vertical") * 0.1f;
    transform.Translate(x, 0, z);

    // shoot
    if (Input.GetKeyDown(KeyCode.Space))
    {
        // Command function is called on the client, but invoked on the server
        CmdFire();
    }
}
```

In the fire logic on the player, make it use the game object pool:

``` cs
[Command]
void CmdFire()
{
    // Set up bullet on server
    GameObject bullet = prefabPoolManager.GetFromPool(transform.position + transform.forward, Quaternion.identity);
    bullet.GetComponent<Rigidbody>().velocity = transform.forward * 4;

    // tell server to send SpawnMessage, which will call SpawnHandler on client
    NetworkServer.Spawn(bullet);

    // destroy bullet after 2 seconds
    StartCoroutine(Destroy(bullet, 2.0f));
}

public IEnumerator Destroy(GameObject go, float delay)
{
    yield return new WaitForSeconds(delay);

    // return object to pool on server
    prefabPoolManager.PutBackInPool(go);

    // tell server to send ObjectDestroyMessage, which will call UnspawnHandler on client
    NetworkServer.UnSpawn(go);
}
```

The Destroy method above shows how to return game objects to the pool so that they can be re-used when you fire again
