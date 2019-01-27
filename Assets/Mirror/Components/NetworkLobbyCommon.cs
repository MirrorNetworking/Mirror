using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mirror.Components.NetworkLobby
{
	public enum MsgType : short
	{
		LobbyReadyToBegin = Mirror.MsgType.Highest + 1,
		LobbySceneLoaded = Mirror.MsgType.Highest + 2,
		LobbyReturnToLobby = Mirror.MsgType.Highest + 3,
		LobbyAddPlayerFailed = Mirror.MsgType.Highest + 4
	}
}
