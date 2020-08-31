using UnityEngine;

public class DontDestroyOnLoad : MonoBehaviour
{
    void Awake()
    {
        //Used in examples
        DontDestroyOnLoad(gameObject);
    }
}
