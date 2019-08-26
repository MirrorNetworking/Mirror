# Custom Spawn Functions

You can use spawn handler functions to customize the default behavior when creating spawned game objects on the client. Spawn handler functions ensure you have full control of how you spawn the game object, as well as how you destroy it.

Use `ClientScene.RegisterSpawnHandler` to register functions to spawn and destroy client game objects. The server creates game objects directly, and then spawns them on the clients through this functionality. This function takes the asset ID of the game object and two function delegates: one to handle creating game objects on the client, and one to handle destroying game objects on the client. The asset ID can be a dynamic one, or just the asset ID found on the prefab game object you want to spawn (if you have one).

The spawn / unspawn delegates need to have this game object signature. This is defined in the high level API.

``` cs
// Handles requests to spawn game objects on the client
public delegate GameObject SpawnDelegate(Vector3 position, System.Guid assetId);

// Handles requests to unspawn game objects on the client
public delegate void UnSpawnDelegate(GameObject spawned);
```

The asset ID passed to the spawn function can be found on `NetworkIdentity.assetId` for prefabs, where it is populated automatically. The registration for a dynamic asset ID is handled like this:

``` cs
// generate a new unique assetId 
System.Guid creatureAssetId = System.Guid.NewGuid();

// register handlers for the new assetId
ClientScene.RegisterSpawnHandler(creatureAssetId, SpawnCreature, UnSpawnCreature);

// get assetId on an existing prefab
System.Guid coinAssetId = coinPrefab.GetComponent<NetworkIdentity>().assetId;

// register handlers for an existing prefab you'd like to custom spawn
ClientScene.RegisterSpawnHandler(coinAssetId, SpawnCoin, UnSpawnCoin);

// spawn a coin - SpawnCoin is called on client
NetworkServer.Spawn(gameObject, coinAssetId);
```

The spawn functions themselves are implemented with the delegate signature. Here is the coin spawner. The `SpawnCreature` would look the same, but have different spawn logic:

``` cs
public GameObject SpawnCoin(Vector3 position, System.Guid assetId)
{
    return Instantiate(m_CoinPrefab, position, Quaternion.identity);
}
public void UnSpawnCoin(GameObject spawned)
{
    Destroy(spawned);
}
```

When using custom spawn functions, it is sometimes useful to be able to unspawn game objects without destroying them. This can be done by calling `NetworkServer.UnSpawn`. This causes a message to be sent to clients to un-spawn the game object, so that the custom unspawn function will be called on the clients. The game object is not destroyed when this function is called.

Note that on the host, game objects are not spawned for the local client, because they already exist on the server. This also means that no spawn handler functions are called.

## Setting Up a Game Object Pool with Custom Spawn Handlers

Here is an example of how you might set up a very simple game object pooling system with custom spawn handlers. Spawning and unspawning then puts game objects in or out of the pool.

``` cs
using UnityEngine;
using Mirror;
using System.Collections;

public class SpawnManager : MonoBehaviour
{
    public int m_ObjectPoolSize = 5;
    public GameObject m_Prefab;
    public GameObject[] m_Pool;

    public System.Guid assetId { get; set;
}
    
    public delegate GameObject SpawnDelegate(Vector3 position, System.Guid assetId);
    public delegate void UnSpawnDelegate(GameObject spawned);

    void Start()
    {
        assetId = m_Prefab.GetComponent<NetworkIdentity> ().assetId;
        m_Pool = new GameObject[m_ObjectPoolSize];
        for (int i = 0; i < m_ObjectPoolSize; ++i)
        {
            m_Pool[i] = Instantiate(m_Prefab, Vector3.zero, Quaternion.identity);
            m_Pool[i].name = "PoolObject" + i;
            m_Pool[i].SetActive(false);
        }
        
        ClientScene.RegisterSpawnHandler(assetId, SpawnObject, UnSpawnObject);
    }

    public GameObject GetFromPool(Vector3 position)
    {
        foreach (var obj in m_Pool)
        {
            if (!obj.activeInHierarchy)
            {
                Debug.Log("Activating GameObject " + obj.name + " at " + position);
                obj.transform.position = position;
                obj.SetActive (true);
                return obj;
            }
        }
        Debug.LogError ("Could not grab game object from pool, nothing available");
        return null;
    }
    
    public GameObject SpawnObject(Vector3 position, System.Guid assetId)
    {
        return GetFromPool(position);
    }
    
    public void UnSpawnObject(GameObject spawned)
    {
        Debug.Log ("Re-pooling game object " + spawned.name);
        spawned.SetActive (false);
    }
}
```

To use this manager, create a new empty game object and name it “SpawnManager”. Create a new script called *SpawnManager,* copy in the code sample above, and attach it to the new SpawnManager game object. Next, drag a prefab you want to spawn multiple times to the Prefab field, and set the Object Pool Size (default is 5).

Finally, set up a reference to the SpawnManager in the script you are using for player movement:

``` cs
SpawnManager spawnManager;

void Start()
{
    spawnManager = GameObject.Find("SpawnManager").GetComponent<SpawnManager> ();
}
```

Your player logic might contain something like this, which moves and fires coins:

``` cs
void Update()
{
    if (!isLocalPlayer)
        return;
    
    var x = Input.GetAxis("Horizontal")*0.1f;
    var z = Input.GetAxis("Vertical")*0.1f;
    
    transform.Translate(x, 0, z);

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
    // Set up coin on server
    var coin = spawnManager.GetFromPool(transform.position + transform.forward);  
    coin.GetComponent<Rigidbody>().velocity = transform.forward*4;
    
    // spawn coin on client, custom spawn handler is called
    NetworkServer.Spawn(coin, spawnManager.assetId);
    
    // when the coin is destroyed on the server, it is automatically destroyed on clients
    StartCoroutine (Destroy (coin, 2.0f));
}

public IEnumerator Destroy(GameObject go, float timer)
{
    yield return new WaitForSeconds (timer);
    spawnManager.UnSpawnObject(go);
    NetworkServer.UnSpawn(go);
}
```

The automatic destruction shows how the game objects are returned to the pool and re-used when you fire again.
