using System;
using System.Collections;
using Mirror.KCP;
using UnityEngine;
using Cysharp.Threading.Tasks;
#if IGNORANCE
using Mirror.ENet;
#endif

namespace Mirror.HeadlessBenchmark
{
    public class HeadlessBenchmark : MonoBehaviour
    {
        public NetworkServer server;
        public ServerObjectManager serverObjectManager;
        public GameObject MonsterPrefab;
        public GameObject PlayerPrefab;
        public string editorArgs;
        public Transport transport;

        string[] cachedArgs;
        string port;

        void Start()
        {
            cachedArgs = Environment.GetCommandLineArgs();

#if UNITY_EDITOR
            cachedArgs = editorArgs.Split(' ');
#endif

            HeadlessStart();

        }
        private IEnumerator DisplayFramesPerSecons()
        {
            int previousFrameCount = Time.frameCount;
            long previousMessageCount = 0;

            while (true)
            {
                yield return new WaitForSeconds(1);
                int frameCount = Time.frameCount;
                int frames = frameCount - previousFrameCount;

                long messageCount = 0;
                if (transport is KcpTransport)
                {
                    messageCount = transport != null ? ((KcpTransport)transport).ReceivedMessageCount : 0;
                }
                
                long messages = messageCount - previousMessageCount;

#if UNITY_EDITOR
                Debug.LogFormat("{0} FPS {1} messages {2} clients", frames, messages, server.NumPlayers);
#else
                Console.WriteLine("{0} FPS {1} messages {2} clients", frames, messages, server.NumPlayers);
#endif
                previousFrameCount = frameCount;
                previousMessageCount = messageCount;
            }
        }

        void HeadlessStart()
        {
            //Try to find port
            port = GetArgValue("-port");

            //Try to find Transport
            ParseForTransport();

            //Server mode?
            ParseForServerMode();

            //Or client mode?
            StartClients().Forget();

            ParseForHelp();
        }

        void OnServerStarted()
        {
            StartCoroutine(DisplayFramesPerSecons());

            string monster = GetArgValue("-monster");
            if (!string.IsNullOrEmpty(monster))
            {
                for (int i = 0; i < int.Parse(monster); i++)
                    SpawnMonsters(i);
            }
        }

        void SpawnMonsters(int i)
        {
            GameObject monster = Instantiate(MonsterPrefab);
            monster.gameObject.name = $"Monster {i}";
            serverObjectManager.Spawn(monster.gameObject);
        }

        void ParseForServerMode()
        {
            if (!string.IsNullOrEmpty(GetArg("-server")))
            {
                var serverGo = new GameObject($"Server", typeof(NetworkServer), typeof(ServerObjectManager), typeof(NetworkSceneManager), typeof(PlayerSpawner));

                server = serverGo.GetComponent<NetworkServer>();
                server.MaxConnections = 9999;
                server.Transport = transport;
                serverObjectManager = serverGo.GetComponent<ServerObjectManager>();

                NetworkSceneManager networkSceneManager = serverGo.GetComponent<NetworkSceneManager>();
                networkSceneManager.Server = server;

                serverObjectManager.Server = server;
                serverObjectManager.NetworkSceneManager = networkSceneManager;
                serverObjectManager.Start();

                PlayerSpawner spawner = serverGo.GetComponent<PlayerSpawner>();
                spawner.PlayerPrefab = PlayerPrefab.GetComponent<NetworkIdentity>();
                spawner.ServerObjectManager = serverObjectManager;
                spawner.Server = server;
                spawner.Start();

                server.Started.AddListener(OnServerStarted);
                server.Authenticated.AddListener(conn => serverObjectManager.SetClientReady(conn));
                _ = server.ListenAsync();
                Console.WriteLine("Starting Server Only Mode");
            }
        }

        async UniTaskVoid StartClients()
        {
            if (!string.IsNullOrEmpty(GetArg("-server")))
                throw new InvalidOperationException("Cannot run server and client in this benchmark. Run them in seperate instances.");

            string clientArg = GetArg("-client");
            if (!string.IsNullOrEmpty(clientArg))
            {
                //network address provided?
                string address = GetArgValue("-address");
                if (string.IsNullOrEmpty(address))
                {
                    address = "localhost";
                }

                //nested clients
                int clonesCount = 1;
                string clonesString = GetArgValue("-client");
                if (!string.IsNullOrEmpty(clonesString))
                {
                    clonesCount = int.Parse(clonesString);
                }

                Console.WriteLine("Starting {0} clients", clonesCount);

                // connect from a bunch of clients
                for (int i = 0; i < clonesCount; i++)
                {
                    await StartClient(i, address);
                    await UniTask.Delay(500);

                    Debug.LogFormat("Started {0} clients", i + 1);
                }
            }
        }

        async UniTask StartClient(int i, string networkAddress)
        {
            var clientGo = new GameObject($"Client {i}", typeof(NetworkClient), typeof(ClientObjectManager), typeof(PlayerSpawner), typeof(NetworkSceneManager));
            NetworkClient client = clientGo.GetComponent<NetworkClient>();
            ClientObjectManager objectManager = clientGo.GetComponent<ClientObjectManager>();
            PlayerSpawner spawner = clientGo.GetComponent<PlayerSpawner>();
            NetworkSceneManager networkSceneManager = clientGo.GetComponent<NetworkSceneManager>();
            networkSceneManager.Client = client;

            objectManager.Client = client;
            objectManager.NetworkSceneManager = networkSceneManager;
            objectManager.Start();
            objectManager.RegisterPrefab(MonsterPrefab.GetComponent<NetworkIdentity>());
            objectManager.RegisterPrefab(PlayerPrefab.GetComponent<NetworkIdentity>());

            spawner.Client = client;
            spawner.PlayerPrefab = PlayerPrefab.GetComponent<NetworkIdentity>();
            spawner.ClientObjectManager = objectManager;
            spawner.SceneManager = networkSceneManager;
            spawner.Start();

            client.Transport = transport;

            try
            {
                await client.ConnectAsync(networkAddress);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void ParseForHelp()
        {
            if (!string.IsNullOrEmpty(GetArg("-help")))
            {
                Console.WriteLine("--==MirrorNG HeadlessClients Benchmark==--");
                Console.WriteLine("Please start your standalone application with the -nographics and -batchmode options");
                Console.WriteLine("Also provide these arguments to control the autostart process:");
                Console.WriteLine("-server (will run in server only mode)");
                Console.WriteLine("-client 1234 (will run the specified number of clients)");
                Console.WriteLine("-transport tcp (transport to be used in test. add more by editing HeadlessBenchmark.cs)");
                Console.WriteLine("-address example.com (will run the specified number of clients)");
                Console.WriteLine("-port 1234 (port used by transport)");
                Console.WriteLine("-monster 100 (number of monsters to spawn on the server)");

                Application.Quit();
            }
        }

        void ParseForTransport()
        {
            string transport = GetArgValue("-transport");
            if (string.IsNullOrEmpty(transport) || transport.Equals("kcp"))
            {
                KcpTransport newTransport = gameObject.AddComponent<KcpTransport>();
                this.transport = newTransport;

                //Try to apply port if exists and needed by transport.
                if (!string.IsNullOrEmpty(port))
                {
                    newTransport.Port = ushort.Parse(port);
                }
            }

#if IGNORANCE
            if (string.IsNullOrEmpty(transport) || transport.Equals("ignorance"))
            {
                IgnoranceNG newTransport = networkManager.gameObject.AddComponent<IgnoranceNG>();

                //Try to apply port if exists and needed by transport.
                if (!string.IsNullOrEmpty(port))
                {
                    newTransport.Config.CommunicationPort = ushort.Parse(port);
                }
                networkManager.server.Transport = newTransport;
                networkManager.client.Transport = newTransport;

                this.transport = newTransport;
            }
#endif
        }

        string GetArgValue(string name)
        {
            for (int i = 0; i < cachedArgs.Length; i++)
            {
                if (cachedArgs[i] == name && cachedArgs.Length > i + 1)
                {
                    return cachedArgs[i + 1];
                }
            }
            return null;
        }

        string GetArg(string name)
        {
            for (int i = 0; i < cachedArgs.Length; i++)
            {
                if (cachedArgs[i] == name)
                {
                    return cachedArgs[i];
                }
            }
            return null;
        }
    }
}
