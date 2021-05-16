using UnityEngine;

public class Player : MonoBehaviour
{
    public float speed = 1;

    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;
        transform.position += direction * speed * Time.deltaTime;
    }
}
