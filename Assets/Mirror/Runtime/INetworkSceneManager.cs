namespace Mirror
{
    public interface INetworkSceneManager
    {
        void ChangeServerScene(string newSceneName);

        void Ready(INetworkConnection conn);
    }
}
