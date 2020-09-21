using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Mirror.Cloud.ListServerService
{
    public sealed class ListServerServerApi : ListServerBaseApi, IListServerServerApi
    {
        const int PingInterval = 20;
        const int MaxPingFails = 15;

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
        int pingFails;

        public bool ServerInList => added;

        public ListServerServerApi(ICoroutineRunner runner, IRequestCreator requestCreator) : base(runner, requestCreator)
        {
        }

        public void Shutdown()
        {
            StopPingCoroutine();
            if (added)
            {
                InternalRemoveServerWithoutCoroutine();
            }
            added = false;
        }

        public void AddServer(ServerJson server)
        {
            if (added) { Logger.LogWarning("AddServer called when server was already adding or added"); return; }
            bool valid = server.Validate();
            if (!valid) { return; }

            runner.StartCoroutine(InternalAddServer(server));
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
                customData = server.customData,
            };
            partialServer.Validate();

            runner.StartCoroutine(InternalUpdateServer(partialServer));
        }

        public void RemoveServer()
        {
            if (!added) { return; }

            if (string.IsNullOrEmpty(serverId))
            {
                Logger.LogWarning("Can not remove server because serverId was empty");
                return;
            }
            StopPingCoroutine();
            runner.StartCoroutine(InternalRemoveServer());
        }

        void StopPingCoroutine()
        {
            if (_pingCoroutine != null)
            {
                runner.StopCoroutine(_pingCoroutine);
                _pingCoroutine = null;
            }
        }

        IEnumerator InternalAddServer(ServerJson server)
        {
            added = true;
            sending = true;
            currentServer = server;

            UnityWebRequest request = requestCreator.Post("servers", currentServer);
            yield return requestCreator.SendRequestEnumerator(request, OnSuccess, OnFail);
            sending = false;

            void OnSuccess(string responseBody)
            {
                CreatedIdJson created = JsonUtility.FromJson<CreatedIdJson>(responseBody);
                serverId = created.id;

                // Start ping to keep server alive
                _pingCoroutine = runner.StartCoroutine(InternalPing());
            }
            void OnFail(string responseBody)
            {
                added = false;
            }
        }

        IEnumerator InternalUpdateServer(PartialServerJson server)
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
            yield return requestCreator.SendRequestEnumerator(request, OnSuccess);
            sending = false;

            void OnSuccess(string responseBody)
            {
                skipNextPing = true;

                if (_pingCoroutine == null)
                {
                    _pingCoroutine = runner.StartCoroutine(InternalPing());
                }
            }
        }

        /// <summary>
        /// Keeps server alive in database
        /// </summary>
        /// <returns></returns>
        IEnumerator InternalPing()
        {
            while (pingFails <= MaxPingFails)
            {
                yield return new WaitForSeconds(PingInterval);
                if (skipNextPing)
                {
                    skipNextPing = false;
                    continue;
                }

                sending = true;
                UnityWebRequest request = requestCreator.Patch("servers/" + serverId, new EmptyJson());
                yield return requestCreator.SendRequestEnumerator(request, OnSuccess, OnFail);
                sending = false;
            }

            Logger.LogWarning("Max ping fails reached, stoping to ping server");
            _pingCoroutine = null;


            void OnSuccess(string responseBody)
            {
                pingFails = 0;
            }
            void OnFail(string responseBody)
            {
                pingFails++;
            }
        }

        IEnumerator InternalRemoveServer()
        {
            sending = true;
            UnityWebRequest request = requestCreator.Delete("servers/" + serverId);
            yield return requestCreator.SendRequestEnumerator(request);
            sending = false;

            added = false;
        }

        void InternalRemoveServerWithoutCoroutine()
        {
            if (string.IsNullOrEmpty(serverId))
            {
                Logger.LogWarning("Can not remove server becuase serverId was empty");
                return;
            }

            UnityWebRequest request = requestCreator.Delete("servers/" + serverId);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            operation.completed += (op) =>
            {
                Logger.LogResponse(request);
            };
        }
    }
}
