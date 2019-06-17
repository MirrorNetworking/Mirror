using UnityEngine;
using Mirror;
using UnityEditor;

public class Test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // get some bytes from transport
        byte[] bytes = new byte[100 * 1024];

        // throw into some readers
        for (int i = 0; i < 1000; ++i)
        {
            NetworkReader reader = new NetworkReader(bytes);
        }
    }
}
