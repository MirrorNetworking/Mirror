using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace Mirror.Cloud.ListServerService
{
    public sealed class ListServerClientApi : ListServerBaseApi, IListServerClientApi
    {
        readonly ServerListEvent _onServerListUpdated;

        Coroutine getServerListRepeatCoroutine;

        public event UnityAction<ServerCollectionJson> onServerListUpdated
        {
            add => _onServerListUpdated.AddListener(value);
            remove => _onServerListUpdated.RemoveListener(value);
        }

        public ListServerClientApi(ICoroutineRunner runner, IRequestCreator requestCreator, ServerListEvent onServerListUpdated) : base(runner, requestCreator)
        {
            _onServerListUpdated = onServerListUpdated;
        }

        public void Shutdown()
        {
            StopGetServerListRepeat();
        }

        public void GetServerList()
        {
            runner.StartCoroutine(getServerList());
        }

        public void StartGetServerListRepeat(int interval)
        {
            getServerListRepeatCoroutine = runner.StartCoroutine(GetServerListRepeat(interval));
        }

        public void StopGetServerListRepeat()
        {
            // if runner is null it has been destroyed and will alraedy be null
            if (runner.IsNotNull() && getServerListRepeatCoroutine != null)
            {
                runner.StopCoroutine(getServerListRepeatCoroutine);
            }
        }

        IEnumerator GetServerListRepeat(int interval)
        {
            while (true)
            {
                yield return getServerList();

                yield return new WaitForSeconds(interval);
            }
        }
        IEnumerator getServerList()
        {
            UnityWebRequest request = requestCreator.Get("servers");
            yield return requestCreator.SendRequestEnumerator(request, onSuccess);

            void onSuccess(string responseBody)
            {
                ServerCollectionJson serverlist = JsonUtility.FromJson<ServerCollectionJson>(responseBody);
                _onServerListUpdated?.Invoke(serverlist);
            }
        }
    }
}
