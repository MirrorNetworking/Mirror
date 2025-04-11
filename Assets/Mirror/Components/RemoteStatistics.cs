// remote statistics panel from Mirror II to show connections, load, etc.
// server syncs statistics to clients if authenticated.
//
// attach this to a player.
// requires NetworkStatistics component on the Network object.
//
// Unity's OnGUI is the easiest to use solution at the moment.
// * playfab is super complex to set up
// * http servers would be nice, but still need to open ports, live refresh, etc
//
// for safety reasons, let's keep this read-only.
// at least until there's safe authentication.
using System;
using System.IO;
using UnityEngine;

namespace Mirror
{
    // server -> client
    struct Stats
    {
        // general
        public int    connections;
        public double uptime;
        public int    configuredTickRate;
        public int    actualTickRate;

        // traffic
        public long sentBytesPerSecond;
        public long receiveBytesPerSecond;

        // cpu
        public float  serverTickInterval;
        public double fullUpdateAvg;
        public double serverEarlyAvg;
        public double serverLateAvg;
        public double transportEarlyAvg;
        public double transportLateAvg;

        // C# boilerplate
        public Stats(
            // general
            int connections,
            double uptime,
            int configuredTickRate,
            int actualTickRate,
            // traffic
            long sentBytesPerSecond,
            long receiveBytesPerSecond,
            // cpu
            float  serverTickInterval,
            double fullUpdateAvg,
            double serverEarlyAvg,
            double serverLateAvg,
            double transportEarlyAvg,
            double transportLateAvg
        )
        {
            // general
            this.connections        = connections;
            this.uptime             = uptime;
            this.configuredTickRate = configuredTickRate;
            this.actualTickRate     = actualTickRate;

            // traffic
            this.sentBytesPerSecond = sentBytesPerSecond;
            this.receiveBytesPerSecond = receiveBytesPerSecond;

            // cpu
            this.serverTickInterval = serverTickInterval;
            this.fullUpdateAvg = fullUpdateAvg;
            this.serverEarlyAvg = serverEarlyAvg;
            this.serverLateAvg = serverLateAvg;
            this.transportEarlyAvg = transportEarlyAvg;
            this.transportLateAvg = transportLateAvg;
        }
    }

    // [RequireComponent(typeof(NetworkStatistics))] <- needs to be on Network GO, not on NI
    public class RemoteStatistics : NetworkBehaviour
    {
        // components ("fake statics" for similar API)
        protected NetworkStatistics NetworkStatistics;

        // broadcast to client.
        // stats are quite huge, let's only send every few seconds via TargetRpc.
        // instead of sending multiple times per second via NB.OnSerialize.
        [Tooltip("Send stats every 'interval' seconds to client.")]
        public float sendInterval = 1;
        double           lastSendTime;

        [Header("GUI")]
        public bool showGui;
        public KeyCode hotKey     = KeyCode.BackQuote;
        Rect           windowRect = new Rect(0, 0, 400, 400);

        // password can't be stored in code or in Unity project.
        // it would be available in clients otherwise.
        // this is not perfectly secure. that's why RemoteStatistics is read-only.
        [Header("Authentication")]
        public string passwordFile = "remote_statistics.txt";
        [Tooltip("Set to false to skip password input and authentication on client and server.")]
        public bool requiresPasswordAuth = true; // false to skip password input and checks
        protected bool         serverAuthenticated;   // client needs to authenticate
        protected bool         clientAuthenticated;   // show GUI until authenticated
        protected string       serverPassword = null; // null means not found, auth impossible
        protected string       clientPassword = "";   // for GUI

        // statistics synced to client
        Stats stats;

        void LoadPassword()
        {
            // TODO only load once, not for all players?
            // let's avoid static state for now.

            // load the password
            string path = Path.GetFullPath(passwordFile);
            if (File.Exists(path))
            {
                // don't spam the server logs for every player's loaded file
                // Debug.Log($"RemoteStatistics: loading password file: {path}");
                try
                {
                    serverPassword = File.ReadAllText(path);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"RemoteStatistics: failed to read password file: {exception}");
                }
            }
            else
            {
                Debug.LogWarning($"RemoteStatistics: password file has not been created. Authentication will be impossible. Please save the password in: {path}");
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            syncMode = SyncMode.Owner;
        }

        // make sure to call base function when overwriting!
        // public so it can also be called from tests (and be overwritten by users)
        public override void OnStartServer()
        {
            NetworkStatistics = NetworkManager.singleton.GetComponent<NetworkStatistics>();
            if (NetworkStatistics == null) throw new Exception($"RemoteStatistics requires a NetworkStatistics component on {NetworkManager.singleton.name}!");

            if (!requiresPasswordAuth)
            {
                // auto authenticate if requiring password is false
                serverAuthenticated = true;
            }
            else
            {
                // server needs to load the password
                LoadPassword();
            }
        }

        public override void OnStartLocalPlayer()
        {
            // center the window initially
            windowRect.x = Screen.width  / 2 - windowRect.width  / 2;
            windowRect.y = Screen.height / 2 - windowRect.height / 2;

            if (!requiresPasswordAuth)
            {
                // auto authenticate if requiring password is false
                clientAuthenticated = true;
            }
        }

        [TargetRpc]
        void TargetRpcSync(Stats v)
        {
            // store stats and flag as authenticated
            clientAuthenticated = true;
            stats = v;
        }

        [Command]
        public void CmdAuthenticate(string v)
        {
            // was a valid password loaded on the server,
            // and did the client send the correct one?
            if (!string.IsNullOrWhiteSpace(serverPassword) &&
                serverPassword.Equals(v))
            {
                serverAuthenticated = true;
                Debug.Log($"RemoteStatistics: connectionId {connectionToClient.connectionId} authenticated with player {name}");
            }
        }

        void UpdateServer()
        {
            // only sync if client has authenticated on the server
            if (!serverAuthenticated) return;

            // NetworkTime.localTime has defines for 2019 / 2020 compatibility
            if (NetworkTime.localTime >= lastSendTime + sendInterval)
            {
                lastSendTime = NetworkTime.localTime;

                // target rpc to owner client
                TargetRpcSync(new Stats(
                    // general
                    NetworkServer.connections.Count,
                    NetworkTime.time,
                    NetworkServer.tickRate,
                    NetworkServer.actualTickRate,

                    // traffic
                    NetworkStatistics.serverSentBytesPerSecond,
                    NetworkStatistics.serverReceivedBytesPerSecond,

                    // cpu
                    NetworkServer.tickInterval,
                    NetworkServer.fullUpdateDuration.average,
                    NetworkServer.earlyUpdateDuration.average,
                    NetworkServer.lateUpdateDuration.average,
                    0, // TODO ServerTransport.earlyUpdateDuration.average,
                    0 // TODO ServerTransport.lateUpdateDuration.average
                ));
            }
        }

        void UpdateClient()
        {
            if (Input.GetKeyDown(hotKey))
                showGui = !showGui;
        }

        void Update()
        {
            if (isServer)      UpdateServer();
            if (isLocalPlayer) UpdateClient();
        }

#if !UNITY_SERVER
        void OnGUI()
        {
            if (!isLocalPlayer) return;
            if (!showGui) return;

            windowRect = GUILayout.Window(0, windowRect, OnWindow, "Remote Statistics");
            windowRect = Utils.KeepInScreen(windowRect);
        }

        // Text: value
        void GUILayout_TextAndValue(string text, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value);
            GUILayout.EndHorizontal();
        }

        // fake a progress bar via horizontal scroll bar with ratio as width
        void GUILayout_ProgressBar(double ratio, int width)
        {
            // clamp ratio, otherwise >1 would make it extremely large
            ratio = Mathd.Clamp01(ratio);
            GUILayout.HorizontalScrollbar(0, (float)ratio, 0, 1, GUILayout.Width(width));
        }

        // need to specify progress bar & caption width,
        // otherwise differently sized captions would always misalign the
        // progress bars.
        void GUILayout_TextAndProgressBar(string text, double ratio, int progressbarWidth, string caption, int captionWidth, Color captionColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout_ProgressBar(ratio, progressbarWidth);

            // coloring the caption is enough. otherwise it's too much.
            GUI.color = captionColor;
            GUILayout.Label(caption, GUILayout.Width(captionWidth));
            GUI.color = Color.white;

            GUILayout.EndHorizontal();
        }

        void GUI_Authenticate()
        {
            GUILayout.BeginVertical("Box"); // start general
            GUILayout.Label("<b>Authentication</b>");

            // warning if insecure connection
            // if (ClientTransport.IsEncrypted())
            // {
            //     GUILayout.Label("<i>Connection is encrypted!</i>");
            // }
            // else
            // {
                GUILayout.Label("<i>Connection is not encrypted. Use with care!</i>");
            // }

            // input
            clientPassword = GUILayout.PasswordField(clientPassword, '*');

            // button
            GUI.enabled = !string.IsNullOrWhiteSpace(clientPassword);
            if (GUILayout.Button("Authenticate"))
            {
                CmdAuthenticate(clientPassword);
            }
            GUI.enabled = true;

            GUILayout.EndVertical(); // end general
        }

        void GUI_General(
            int connections,
            double uptime,
            int configuredTickRate,
            int actualTickRate)
        {
            GUILayout.BeginVertical("Box"); // start general
            GUILayout.Label("<b>General</b>");

            // connections
            GUILayout_TextAndValue("Connections:", $"<b>{connections}</b>");

            // uptime
            GUILayout_TextAndValue("Uptime:", $"<b>{Utils.PrettySeconds(uptime)}</b>"); // TODO

            // tick rate
            // might be lower under heavy load.
            // might be higher in editor if targetFrameRate can't be set.
            GUI.color = actualTickRate < configuredTickRate ? Color.red : Color.green;
            GUILayout_TextAndValue("Tick Rate:", $"<b>{actualTickRate} Hz / {configuredTickRate} Hz</b>");
            GUI.color = Color.white;

            GUILayout.EndVertical(); // end general
        }

        void GUI_Traffic(
            long serverSentBytesPerSecond,
            long serverReceivedBytesPerSecond)
        {
            GUILayout.BeginVertical("Box");
            GUILayout.Label("<b>Network</b>");

            GUILayout_TextAndValue("Outgoing:", $"<b>{Utils.PrettyBytes(serverSentBytesPerSecond)    }/s</b>");
            GUILayout_TextAndValue("Incoming:", $"<b>{Utils.PrettyBytes(serverReceivedBytesPerSecond)}/s</b>");

            GUILayout.EndVertical();
        }

        void GUI_Cpu(
            float serverTickInterval,
            double fullUpdateAvg,
            double serverEarlyAvg,
            double serverLateAvg,
            double transportEarlyAvg,
            double transportLateAvg)
        {
            const int barWidth = 120;
            const int captionWidth = 90;

            GUILayout.BeginVertical("Box");
            GUILayout.Label("<b>CPU</b>");

            // unity update
            // happens every 'tickInterval'. progress bar shows it in relation.
            // <= 90% load is green, otherwise red
            double fullRatio = fullUpdateAvg / serverTickInterval;
            GUILayout_TextAndProgressBar(
                "World Update Avg:",
                fullRatio,
                barWidth, $"<b>{fullUpdateAvg * 1000:F1} ms</b>",
                captionWidth,
                fullRatio <= 0.9 ? Color.green : Color.red);

            // server update
            // happens every 'tickInterval'. progress bar shows it in relation.
            // <= 90% load is green, otherwise red
            double serverRatio = (serverEarlyAvg + serverLateAvg) / serverTickInterval;
            GUILayout_TextAndProgressBar(
                "Server Update Avg:",
                serverRatio,
                barWidth, $"<b>{serverEarlyAvg * 1000:F1} + {serverLateAvg * 1000:F1} ms</b>",
                captionWidth,
                serverRatio <= 0.9 ? Color.green : Color.red);

            // transport: early + late update milliseconds.
            // for threaded transport, this is the thread's update time.
            // happens every 'tickInterval'. progress bar shows it in relation.
            // <= 90% load is green, otherwise red
            // double transportRatio = (transportEarlyAvg + transportLateAvg) / serverTickInterval;
            // GUILayout_TextAndProgressBar(
            //     "Transport Avg:",
            //     transportRatio,
            //     barWidth,
            //     $"<b>{transportEarlyAvg * 1000:F1} + {transportLateAvg * 1000:F1} ms</b>",
            //     captionWidth,
            //     transportRatio <= 0.9 ? Color.green : Color.red);

            GUILayout.EndVertical();
        }

        void GUI_Notice()
        {
            // for security reasons, let's keep this read-only for now.

            // single line keeps input & visuals simple
            // GUILayout.BeginVertical("Box");
            // GUILayout.Label("<b>Global Notice</b>");
            // notice = GUILayout.TextField(notice);
            // if (GUILayout.Button("Send"))
            // {
            //     // TODO
            // }
            // GUILayout.EndVertical();
        }

        void OnWindow(int windowID)
        {
            if (!clientAuthenticated)
            {
                GUI_Authenticate();
            }
            else
            {
                GUI_General(
                    stats.connections,
                    stats.uptime,
                    stats.configuredTickRate,
                    stats.actualTickRate
                );

                GUI_Traffic(
                    stats.sentBytesPerSecond,
                    stats.receiveBytesPerSecond
                );

                GUI_Cpu(
                    stats.serverTickInterval,
                    stats.fullUpdateAvg,
                    stats.serverEarlyAvg,
                    stats.serverLateAvg,
                    stats.transportEarlyAvg,
                    stats.transportLateAvg
                );

                GUI_Notice();
            }

            // dragable window in any case
            GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        }
#endif
    }
}
