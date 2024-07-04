using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.Shooter
{
    public class Bot : MonoBehaviour
    {
        // a bot script to fake input
        // ideal for player host and quick testing
        // not yet made for server-only mode

        private Player player;
        public Player targetPlayer;
        public bool canMove = false;
        public float moveSpeed = 5f;
        public float minDistance = 4f;
        public float rotationSpeed = 5f;
        public float aggroTime = 3f;
        // skip raycast if aggrod and go after closest, stops AI looking dumb and standing still, if shot
        public bool inAggro = false; 

        private void Start()
        {
            player = GetComponent<Player>();
            if (player.isServer)
            {
                player = GetComponent<Player>();
                player.bot = this;
                player.playerName = "Bot: " + player.netId;
                player.playerCharacter = Random.Range(1, player.characterData.characterPrefabs.Length);
                player.playerColour = Random.ColorHSV(0f, 1f, 1f, 1f, 0f, 1f);

                InvokeRepeating("Repeater", 1.0f, 1.0f);
            }
        }

        public void Update()
        {
            if (player == null)
            {
                return;
            }

            // set to defalt values
            canMove = false;
            player.playerMovement.horizontal = 0.0f;
            player.playerMovement.vertical = 0.0f;

            if (targetPlayer)
            {
                // Calculate direction towards the player
                Vector3 directionToPlayer = targetPlayer.transform.position - this.transform.position;
                directionToPlayer.y = 0; // Optional: Keep the movement in 2D plane

                // Check if the distance is greater than the minimum distance
                if (directionToPlayer.magnitude > minDistance)
                {
                    // Move towards the player
                    // transformToMove.Translate(directionToPlayer.normalized * moveSpeed * Time.deltaTime, Space.World);
                    canMove = true;
                }

                if (canMove)
                {
                    player.playerMovement.horizontal = CalculateHorizontalInput();
                    player.playerMovement.vertical = CalculateVerticalInput();

                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    this.transform.rotation = Quaternion.Slerp(this.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }

        private float CalculateHorizontalInput()
        {
            // Simulate horizontal movement
            // Example: Move left for half a second, then right for half a second
            float time = Time.time % 1; // Repeat every second
            if (time < 0.5f)
                return -0.2f; // Move left
            else
                return 0.2f;  // Move right
        }

        private float CalculateVerticalInput()
        {
            // Simulate vertical movement
            // Example: Move forward constantly
            //return 1f; // Move forward

            float time = Time.time % 3; // Repeat every second
            if (time < 0.5f)
                return -0.25f; // Move Back
            else
                return 0.4f;  // Move Forward
        }

        private void Repeater()
        {
            targetPlayer = null;
            targetPlayer = FindClosestUnobstructedPlayer();
        }

        public Player FindClosestUnobstructedPlayer()
        {
            Player closestPlayer = null;
            float closestDistance = Mathf.Infinity;

            foreach (Player player in Player.playersList)
            {
                if (player != null && player != this.player)
                {
                    // Calculate distance between target and player
                    float distance = Vector3.Distance(player.transform.position, this.transform.position);

                    // Perform a raycast from target to player
                    RaycastHit hit;
                    bool obstructed = Physics.Raycast(this.transform.position, player.transform.position - this.transform.position, out hit, distance);

                    if (inAggro || (!obstructed || hit.collider.gameObject == player.gameObject))
                    {
                        // No obstruction or obstruction by the player itself
                        if (distance < closestDistance)
                        {
                            closestPlayer = player;
                            closestDistance = distance;
                        }
                    }
                }
            }

            return closestPlayer;
        }

        public void ActivateAggro()
        {
            if (inAggro == false)
            {
                StartCoroutine(Aggro());
            }
        }

        IEnumerator Aggro()
        {
            inAggro = true;
            yield return new WaitForSeconds(aggroTime);
            inAggro = false;
        }
    }
}