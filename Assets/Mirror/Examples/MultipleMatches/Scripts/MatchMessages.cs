using System;

namespace Mirror.Examples.MultipleMatch
{
    /// <summary>
    /// Match message to be sent to the server
    /// </summary>
    public struct ServerMatchMessage : NetworkMessage
    {
        public ServerMatchOperation serverMatchOperation;
        public Guid matchId;
    }

    /// <summary>
    /// Match message to be sent to the client
    /// </summary>
    public struct ClientMatchMessage : NetworkMessage
    {
        public ClientMatchOperation clientMatchOperation;
        public Guid matchId;
        public MatchInfo[] matchInfos;
        public PlayerInfo[] playerInfos;
    }

    /// <summary>
    /// Information about a match
    /// </summary>
    [Serializable]
    public struct MatchInfo
    {
        public Guid matchId;
        public byte players;
        public byte maxPlayers;
    }

    /// <summary>
    /// Information about a player
    /// </summary>
    [Serializable]
    public struct PlayerInfo
    {
        public int playerIndex;
        public bool ready;
        public Guid matchId;
    }

    [Serializable]
    public struct MatchPlayerData
    {
        public int playerIndex;
        public int wins;
        public CellValue currentScore;
    }

    /// <summary>
    /// Match operation to execute on the server
    /// </summary>
    public enum ServerMatchOperation : byte
    {
        None,
        Create,
        Cancel,
        Start,
        Join,
        Leave,
        Ready
    }

    /// <summary>
    /// Match operation to execute on the client
    /// </summary>
    public enum ClientMatchOperation : byte
    {
        None,
        List,
        Created,
        Cancelled,
        Joined,
        Departed,
        UpdateRoom,
        Started
    }

    //     A1 | B1 | C1
    //     ---+----+---
    //     A2 | B2 | C2
    //     ---+----+---
    //     A3 | B3 | C3

    [Flags]
    public enum CellValue : ushort
    {
        None,
        A1 = 1 << 0,
        B1 = 1 << 1,
        C1 = 1 << 2,
        A2 = 1 << 3,
        B2 = 1 << 4,
        C2 = 1 << 5,
        A3 = 1 << 6,
        B3 = 1 << 7,
        C3 = 1 << 8,

        // winning combinations
        TopRow = A1 + B1 + C1,
        MidRow = A2 + B2 + C2,
        BotRow = A3 + B3 + C3,
        LeftCol = A1 + A2 + A3,
        MidCol = B1 + B2 + B3,
        RightCol = C1 + C2 + C3,
        Diag1 = A1 + B2 + C3,
        Diag2 = A3 + B2 + C1,

        // board is full (winner / draw)
        Full = TopRow + MidRow + BotRow
    }
}
