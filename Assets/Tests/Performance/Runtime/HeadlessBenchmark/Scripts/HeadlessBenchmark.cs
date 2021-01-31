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
        public NetworkManager networkManager;
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
                Debug.LogFormat("{0} FPS {1} messages {2} clients", frames, messages, networkManager.Server.NumPlayers);
#else
                Console.WriteLine("{0} FPS {1} messages {2} clients", frames, messages, networkManager.Server.NumPlayers);
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

        async UniTask StartClient(int i, Transport transport, string networkAddress)
        {
            var clientGo = new GameObject($"Client {i}", typeof(NetworkClient), typeof(ClientObjectManager));
            NetworkClient client = clientGo.GetComponent<NetworkClient>();
            ClientObjectManager objectManager = clientGo.GetComponent<ClientObjectManager>();
            objectManager.Client = client;
            objectManager.Start();
            client.Transport = transport;

            objectManager.RegisterPrefab(MonsterPrefab.GetComponent<NetworkIdentity>());
            objectManager.RegisterPrefab(PlayerPrefab.GetComponent<NetworkIdentity>());

            try
            {
                await client.ConnectAsync(networkAddress);
                client.Send(new AddPlayerMessage());

            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

        }

        void ParseForServerMode()
        {
            if (!string.IsNullOrEmpty(GetArg("-server")))
            {
                networkManager.Server.Started.AddListener(OnServerStarted);
                networkManager.Server.Authenticated.AddListener(conn => serverObjectManager.SetClientReady(conn));
                _ = networkManager.Server.ListenAsync();
                Console.WriteLine("Starting Server Only Mode");
            }
        }

        async UniTaskVoid StartClients()
        {
            string client = GetArg("-client");
            if (!string.IsNullOrEmpty(client))
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
                    await StartClient(i, networkManager.Client.Transport, address);
                    await UniTask.Delay(500);

                    Debug.LogFormat("Started {0} clients", i + 1);
                }
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
                KcpTransport newTransport = networkManager.gameObject.AddComponent<KcpTransport>();

                //Try to apply port if exists and needed by transport.
                if (!string.IsNullOrEmpty(port))
                {
                    newTransport.Port = ushort.Parse(port);
                }
                networkManager.Server.Transport = newTransport;
                networkManager.Client.Transport = newTransport;

                this.transport = newTransport;
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
