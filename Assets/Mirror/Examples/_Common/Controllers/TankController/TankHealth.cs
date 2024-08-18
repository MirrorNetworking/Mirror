using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class TankHealth : NetworkBehaviour
    {
        [Header("Components")]
        public TextMesh healthBar;

        [Header("Stats")]
        public byte maxHealth = 5;
        [SyncVar(hook = nameof(OnHealthChanged))]
        public byte health = 5;

        [Header("Respawn")]
        public bool respawn = true;
        public byte respawnTime = 3;

        void OnHealthChanged(byte oldHealth, byte newHealth)
        {
            healthBar.text = new string('-', newHealth);

            if (newHealth >= maxHealth)
                healthBar.color = Color.green;
            if (newHealth < 4)
                healthBar.color = Color.yellow;
            if (newHealth < 2)
                healthBar.color = Color.red;
            if (newHealth < 1)
                healthBar.color = Color.black;
        }

        #region Unity Callbacks

        protected override void OnValidate()
        {
            // Skip if Editor is in Play mode
            if (Application.isPlaying) return;

            base.OnValidate();
            Reset();
        }

        public void Reset()
        {
            if (healthBar == null)
                healthBar = transform.Find("HealthBar").GetComponent<TextMesh>();
        }

        #endregion

        public override void OnStartServer()
        {
            health = maxHealth;
        }

        [ServerCallback]
        public void TakeDamage(byte damage)
        {
            // Skip if health is already 0
            if (health == 0) return;

            if (damage > health)
                health = 0;
            else
                health -= damage;

            if (health == 0)
            {
                if (connectionToClient != null)
                    Respawn.RespawnPlayer(respawn, respawnTime, connectionToClient);
                else if (netIdentity.sceneId != 0)
                    NetworkServer.UnSpawn(gameObject);
                else
                    Destroy(gameObject);
            }
        }
    }
}