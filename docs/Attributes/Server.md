# Server

General description of Server

```
public class Ball : NetworkBehaviour
{
    public float Speed = 30;

    [Server] // only call this on server
    void Start()
    {
        // Initial Velocity
        GetComponent<Rigidbody2D>().velocity = Vector2.right * Speed;
    }

    [Server] // only call this on server
    void OnCollisionEnter2D(Collision2D col)
    {
        ...
    }
}
```
