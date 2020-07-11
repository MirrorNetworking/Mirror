namespace Mirror
{
    public interface INetworkSceneManager
    {
        void ChangeServerScene(string newSceneName);

        void SetClientReady(INetworkConnection conn);
    }
}
