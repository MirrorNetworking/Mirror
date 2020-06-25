using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Mirror.Cloud.ListServerService
{
    public sealed class ListServerServerApi : ListServerBaseApi, IListServerServerApi
    {
        const int PingInterval = 15;
        const int MaxPingFails = 3;

        ServerJson currentServer;
        string serverId;

        Coroutine _pingCoroutine;
        /// <summary>
        /// If the server has already been added
        /// </summary>
        bool added;
        /// <summary>
        /// if a request is currently sending
        /// </summary>
        bool sending;
        /// <summary>
        /// If an update request was recently sent
        /// </summary>
        bool skipNextPing;
        /// <summary>
        /// How many failed pings in a row
        /// </summary>
        int pingFails = 0;

        public bool ServerInList => added;

        public ListServerServerApi(ICoroutineRunner runner, IRequestCreator requestCreator) : base(runner, requestCreator)
        {
        }

        public void Shutdown()
        {
            stopPingCoroutine();
            if (added)
            {
                removeServerWithoutCoroutine();
            }
            added = false;
        }

        public void AddServer(ServerJson server)
        {
            if (added) { Logger.LogWarning("AddServer called when server was already adding or added"); return; }
            bool valid = ValidateServerJson(server);
            if (!valid) { return; }

            runner.StartCoroutine(addServer(server));
        }

        bool ValidateServerJson(ServerJson server)
        {
            if (string.IsNullOrEmpty(server.protocol))
            {
                Logger.LogError("ServerJson should not have empty protocol");
                return false;
            }
            if (server.port == 0)
            {
                Logger.LogError("ServerJson should not have port equal 0");
                return false;
            }
            if (server.maxPlayerCount == 0)
            {
                Logger.LogError("ServerJson should not have maxPlayerCount equal 0");
                return false;
            }

            return true;
        }

        public void UpdateServer(int newPlayerCount)
        {
            if (!added) { Logger.LogWarning("UpdateServer called when before server was added"); return; }

            currentServer.playerCount = newPlayerCount;
            UpdateServer(currentServer);
        }

        public void UpdateServer(ServerJson server)
        {
            // TODO, use PartialServerJson as Arg Instead
            if (!added) { Logger.LogWarning("UpdateServer called when before server was added"); return; }

            PartialServerJson partialServer = new PartialServerJson
            {
                displayName = server.displayName,
                playerCount = server.playerCount,
                maxPlayerCount = server.maxPlayerCount,
            };

            runner.StartCoroutine(updateServer(partialServer));
        }

        public void RemoveServer()
        {
            if (!added) { return; }

            stopPingCoroutine();
            runner.StartCoroutine(removeServer());
        }

        void stopPingCoroutine()
        {
            if (_pingCoroutine != null)
            {
                runner.StopCoroutine(_pingCoroutine);
                _pingCoroutine = null;
            }
        }

        IEnumerator addServer(ServerJson server)
        {
            added = true;
            sending = true;
            currentServer = server;

            UnityWebRequest request = requestCreator.Post("servers", currentServer);
            yield return requestCreator.SendRequestEnumerator(request, onSuccess, onFail);
            sending = false;

            void onSuccess(string responseBody)
            {
                CreatedIdJson created = JsonUtility.FromJson<CreatedIdJson>(responseBody);
                serverId = created.id;

                // Start ping to keep server alive
                _pingCoroutine = runner.StartCoroutine(ping());
            }
            void onFail(string responseBody)
            {
                added = false;
            }
        }

        IEnumerator updateServer(PartialServerJson server)
        {
            // wait to not be sending
            while (sending)
            {
                yield return new WaitForSeconds(1);
            }

            // We need to check added incase Update is called soon after Add, and add failed
            if (!added) { Logger.LogWarning("UpdateServer called when before server was added"); yield break; }

            sending = true;
            UnityWebRequest request = requestCreator.Patch("servers/" + serverId, server);
            yield return requestCreator.SendRequestEnumerator(request, onSuccess);
            sending = false;

            void onSuccess(string responseBody)
            {
                skipNextPing = true;

                if (_pingCoroutine == null)
                {
                    _pingCoroutine = runner.StartCoroutine(ping());
                }
            }
        }

        /// <summary>
        /// Keeps server alive in database
        /// </summary>
        /// <returns></returns>
        IEnumerator ping()
        {
            while (pingFails <= MaxPingFails)
            {
                yield return new WaitForSeconds(PingInterval);
                if (skipNextPing) { continue; }

                sending = true;
                UnityWebRequest request = requestCreator.Patch("servers/" + serverId, new EmptyJson());
                yield return requestCreator.SendRequestEnumerator(request, onSuccess, onFail);
                sending = false;
            }

            Logger.LogWarning("Max ping fails reached, stoping to ping server");
            _pingCoroutine = null;


            void onSuccess(string responseBody)
            {
                pingFails = 0;
            }
            void onFail(string responseBody)
            {
                pingFails++;
            }
        }

        IEnumerator removeServer()
        {
            sending = true;
            UnityWebRequest request = requestCreator.Delete("servers/" + serverId);
            yield return requestCreator.SendRequestEnumerator(request);
            sending = false;

            added = false;
        }

        void removeServerWithoutCoroutine()
        {
            UnityWebRequest request = requestCreator.Delete("servers/" + serverId);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            operation.completed += (op) =>
            {
                Logger.LogResponse(request);
            };
        }
    }
}
