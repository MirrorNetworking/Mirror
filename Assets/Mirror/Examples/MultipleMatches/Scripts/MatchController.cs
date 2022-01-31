using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.MultipleMatch
{
    [RequireComponent(typeof(NetworkMatch))]
    public class MatchController : NetworkBehaviour
    {
        internal readonly SyncDictionary<NetworkIdentity, MatchPlayerData> matchPlayerData = new SyncDictionary<NetworkIdentity, MatchPlayerData>();
        internal readonly Dictionary<CellValue, CellGUI> MatchCells = new Dictionary<CellValue, CellGUI>();

        CellValue boardScore = CellValue.None;
        bool playAgain = false;

        [Header("GUI References")]
        public CanvasGroup canvasGroup;
        public Text gameText;
        public Button exitButton;
        public Button playAgainButton;
        public Text winCountLocal;
        public Text winCountOpponent;

        [Header("Diagnostics - Do Not Modify")]
        public CanvasController canvasController;
        public NetworkIdentity player1;
        public NetworkIdentity player2;
        public NetworkIdentity startingPlayer;

        [SyncVar(hook = nameof(UpdateGameUI))]
        public NetworkIdentity currentPlayer;

        void Awake()
        {
            canvasController = FindObjectOfType<CanvasController>();
        }

        public override void OnStartServer()
        {
            StartCoroutine(AddPlayersToMatchController());
        }

        // For the SyncDictionary to properly fire the update callback, we must
        // wait a frame before adding the players to the already spawned MatchController
        IEnumerator AddPlayersToMatchController()
        {
            yield return null;

            matchPlayerData.Add(player1, new MatchPlayerData { playerIndex = CanvasController.playerInfos[player1.connectionToClient].playerIndex });
            matchPlayerData.Add(player2, new MatchPlayerData { playerIndex = CanvasController.playerInfos[player2.connectionToClient].playerIndex });
        }


        public override void OnStartClient()
        {
            matchPlayerData.Callback += UpdateWins;

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            exitButton.gameObject.SetActive(false);
            playAgainButton.gameObject.SetActive(false);
        }

        public void UpdateGameUI(NetworkIdentity _, NetworkIdentity newPlayerTurn)
        {
            if (!newPlayerTurn) return;

            if (newPlayerTurn.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                gameText.text = "Your Turn";
                gameText.color = Color.blue;
            }
            else
            {
                gameText.text = "Their Turn";
                gameText.color = Color.red;
            }
        }

        public void UpdateWins(SyncDictionary<NetworkIdentity, MatchPlayerData>.Operation op, NetworkIdentity key, MatchPlayerData matchPlayerData)
        {
            if (key.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                winCountLocal.text = $"Player {matchPlayerData.playerIndex}\n{matchPlayerData.wins}";
            }
            else
            {
                winCountOpponent.text = $"Player {matchPlayerData.playerIndex}\n{matchPlayerData.wins}";
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdMakePlay(CellValue cellValue, NetworkConnectionToClient sender = null)
        {
            // If wrong player or cell already taken, ignore
            if (sender.identity != currentPlayer || MatchCells[cellValue].playerIdentity != null)
                return;

            MatchCells[cellValue].playerIdentity = currentPlayer;
            RpcUpdateCell(cellValue, currentPlayer);

            MatchPlayerData mpd = matchPlayerData[currentPlayer];
            mpd.currentScore = mpd.currentScore | cellValue;
            matchPlayerData[currentPlayer] = mpd;

            boardScore = boardScore | cellValue;

            if (CheckWinner(mpd.currentScore))
            {
                mpd.wins += 1;
                matchPlayerData[currentPlayer] = mpd;
                RpcShowWinner(currentPlayer);
                currentPlayer = null;
            }
            else if (boardScore == CellValue.Full)
            {
                RpcShowWinner(null);
                currentPlayer = null;
            }
            else
            {
                // Set currentPlayer SyncVar so clients know whose turn it is
                currentPlayer = currentPlayer == player1 ? player2 : player1;
            }

        }

        bool CheckWinner(CellValue currentScore)
        {
            if ((currentScore & CellValue.TopRow) == CellValue.TopRow)
                return true;
            if ((currentScore & CellValue.MidRow) == CellValue.MidRow)
                return true;
            if ((currentScore & CellValue.BotRow) == CellValue.BotRow)
                return true;
            if ((currentScore & CellValue.LeftCol) == CellValue.LeftCol)
                return true;
            if ((currentScore & CellValue.MidCol) == CellValue.MidCol)
                return true;
            if ((currentScore & CellValue.RightCol) == CellValue.RightCol)
                return true;
            if ((currentScore & CellValue.Diag1) == CellValue.Diag1)
                return true;
            if ((currentScore & CellValue.Diag2) == CellValue.Diag2)
                return true;

            return false;
        }

        [ClientRpc]
        public void RpcUpdateCell(CellValue cellValue, NetworkIdentity player)
        {
            MatchCells[cellValue].SetPlayer(player);
        }

        [ClientRpc]
        public void RpcShowWinner(NetworkIdentity winner)
        {

            foreach (CellGUI cellGUI in MatchCells.Values)
                cellGUI.GetComponent<Button>().interactable = false;

            if (winner == null)
            {
                gameText.text = "Draw!";
                gameText.color = Color.yellow;
            }
            else if (winner.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                gameText.text = "Winner!";
                gameText.color = Color.blue;
            }
            else
            {
                gameText.text = "Loser!";
                gameText.color = Color.red;
            }
            exitButton.gameObject.SetActive(true);
            playAgainButton.gameObject.SetActive(true);
        }

        // Assigned in inspector to ReplayButton::OnClick
        [Client]
        public void RequestPlayAgain()
        {
            playAgainButton.gameObject.SetActive(false);
            CmdPlayAgain();
        }

        [Command(requiresAuthority = false)]
        public void CmdPlayAgain(NetworkConnectionToClient sender = null)
        {
            if (!playAgain)
            {
                playAgain = true;
            }
            else
            {
                playAgain = false;
                RestartGame();
            }
        }

        [Server]
        public void RestartGame()
        {
            foreach (CellGUI cellGUI in MatchCells.Values)
                cellGUI.SetPlayer(null);

            boardScore = CellValue.None;

            NetworkIdentity[] keys = new NetworkIdentity[matchPlayerData.Keys.Count];
            matchPlayerData.Keys.CopyTo(keys, 0);

            foreach (NetworkIdentity identity in keys)
            {
                MatchPlayerData mpd = matchPlayerData[identity];
                mpd.currentScore = CellValue.None;
                matchPlayerData[identity] = mpd;
            }

            RpcRestartGame();

            startingPlayer = startingPlayer == player1 ? player2 : player1;
            currentPlayer = startingPlayer;
        }

        [ClientRpc]
        public void RpcRestartGame()
        {
            foreach (CellGUI cellGUI in MatchCells.Values)
                cellGUI.SetPlayer(null);

            exitButton.gameObject.SetActive(false);
            playAgainButton.gameObject.SetActive(false);
        }

        // Assigned in inspector to BackButton::OnClick
        [Client]
        public void RequestExitGame()
        {
            exitButton.gameObject.SetActive(false);
            playAgainButton.gameObject.SetActive(false);
            CmdRequestExitGame();
        }

        [Command(requiresAuthority = false)]
        public void CmdRequestExitGame(NetworkConnectionToClient sender = null)
        {
            StartCoroutine(ServerEndMatch(sender, false));
        }

        public void OnPlayerDisconnected(NetworkConnection conn)
        {
            // Check that the disconnecting client is a player in this match
            if (player1 == conn.identity || player2 == conn.identity)
            {
                StartCoroutine(ServerEndMatch(conn, true));
            }
        }

        public IEnumerator ServerEndMatch(NetworkConnection conn, bool disconnected)
        {
            canvasController.OnPlayerDisconnected -= OnPlayerDisconnected;

            RpcExitGame();

            // Skip a frame so the message goes out ahead of object destruction
            yield return null;

            // Mirror will clean up the disconnecting client so we only need to clean up the other remaining client.
            // If both players are just returning to the Lobby, we need to remove both connection Players

            if (!disconnected)
            {
                NetworkServer.RemovePlayerForConnection(player1.connectionToClient, true);
                CanvasController.waitingConnections.Add(player1.connectionToClient);

                NetworkServer.RemovePlayerForConnection(player2.connectionToClient, true);
                CanvasController.waitingConnections.Add(player2.connectionToClient);
            }
            else if (conn == player1.connectionToClient)
            {
                // player1 has disconnected - send player2 back to Lobby
                NetworkServer.RemovePlayerForConnection(player2.connectionToClient, true);
                CanvasController.waitingConnections.Add(player2.connectionToClient);
            }
            else if (conn == player2.connectionToClient)
            {
                // player2 has disconnected - send player1 back to Lobby
                NetworkServer.RemovePlayerForConnection(player1.connectionToClient, true);
                CanvasController.waitingConnections.Add(player1.connectionToClient);
            }

            // Skip a frame to allow the Removal(s) to complete
            yield return null;

            // Send latest match list
            canvasController.SendMatchList();

            NetworkServer.Destroy(gameObject);
        }

        [ClientRpc]
        public void RpcExitGame()
        {
            canvasController.OnMatchEnded();
        }
    }
}
