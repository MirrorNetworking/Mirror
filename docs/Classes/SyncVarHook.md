# SyncVar Hook

The hook attribute can be used to specify a function to be called when the SyncVar changes value on the client.

This ensures that all clients receive the proper variables from other clients.

```cs
using UnityEngine;
using UnityEngine.Networking;

public class Health : NetworkBehaviour
{
    public const int m_MaxHealth = 100;

    //Detects when a health change happens and calls the appropriate function
    [SyncVar(hook = nameof(OnChangeHealth))]
    public int m_CurrentHealth = m_MaxHealth;
    public RectTransform healthBar;

    public void TakeDamage(int amount)
    {
        if (!isServer)
            return;

        //Decrease the "health" of the GameObject
        m_CurrentHealth -= amount;
        //Make sure the health doesn't go below 0
        if (m_CurrentHealth <= 0)
        {
            m_CurrentHealth = 0;
        }
    }

    void Update()
    {
        //If the space key is pressed, decrease the GameObject's own "health"
        if (Input.GetKey(KeyCode.Space))
        {
            if (isLocalPlayer)
                CmdTakeHealth();
        }
    }

    void OnChangeHealth(int health)
    {
        healthBar.sizeDelta = new Vector2(health, healthBar.sizeDelta.y);
    }

    //This is a Network command, so the damage is done to the relevant GameObject
    [Command]
    void CmdTakeHealth()
    {
        //Apply damage to the GameObject
        TakeDamage(2);
    }
}
```
