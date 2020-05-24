using System;

namespace Mirror
{
    public interface INetworkManager
    {
        void StartClient(Uri uri);

        void StopHost();

        void StopServer();

        void StopClient();

        void OnDestroy();

        void ServerChangeScene(string newSceneName);
    }
}
