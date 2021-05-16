using UnityEngine;

public class Tree : MonoBehaviour
{
    void OnTriggerEnter(Collider col)
    {
        Debug.LogWarning($"Tree [{gameObject.scene.name}] OnTrigger: {col.name}[{col.gameObject.scene.name}]");
    }
}
