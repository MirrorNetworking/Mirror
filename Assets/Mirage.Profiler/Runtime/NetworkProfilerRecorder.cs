using UnityEngine;
using Mirror;

#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace Mirage.NetworkProfiler
{
    [DefaultExecutionOrder(int.MaxValue)] // last
    public class NetworkProfilerRecorder : MonoBehaviour
    {
        // singleton because unity only has 1 profiler
        public static NetworkProfilerRecorder Instance { get; private set; }

        internal static CountRecorder _sentCounter;
        internal static CountRecorder _receivedCounter;
        internal const int FRAME_COUNT = 300; // todo find a way to get real frame count

        public delegate void FrameUpdate(int tick);
        public static event FrameUpdate AfterSample;

        private int _lastProcessedFrame = -1;

        private void Start()
        {
            if (Instance == null)
            {
#if UNITY_EDITOR
                _lastProcessedFrame = ProfilerDriver.lastFrameIndex;
#endif
                
                var provider = new NetworkInfoProvider();
                _sentCounter = new CountRecorder(null, provider, Counters.SentCount, Counters.SentBytes, Counters.SentPerSecond);
                _receivedCounter = new CountRecorder(null, provider, Counters.ReceiveCount, Counters.ReceiveBytes, Counters.ReceivePerSecond);
                NetworkDiagnostics.InMessageEvent += _receivedCounter.OnMessage;
                NetworkDiagnostics.OutMessageEvent += _sentCounter.OnMessage;

                Instance = this;
                DontDestroyOnLoad(this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (_receivedCounter != null)
                    NetworkDiagnostics.InMessageEvent -= _receivedCounter.OnMessage;
                if (_sentCounter != null)
                    NetworkDiagnostics.OutMessageEvent -= _sentCounter.OnMessage;
                
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (!NetworkServer.active && !NetworkClient.active)
                return;

#if UNITY_EDITOR
            if (!ProfilerDriver.enabled)
                return;

            // once a frame, ever frame, no matter what lastFrameIndex is
            SampleCounts();

            // unity sometimes skips a profiler frame, because unity
            // so we have to check if that happens and then sample the missing frame
            while (_lastProcessedFrame < ProfilerDriver.lastFrameIndex)
            {
                _lastProcessedFrame++;

                //Debug.Log($"Sample: [LateUpdate, enabled { ProfilerDriver.enabled}, first {ProfilerDriver.firstFrameIndex}, last {ProfilerDriver.lastFrameIndex}, lastProcessed {lastProcessedFrame}]");

                var lastFrame = _lastProcessedFrame;
                // not sure why frame is offset, but +2 fixes it
                SampleMessages(lastFrame + 2);
            }
#else
            // in player, just use ProfilerCounter (frameCount only used by messages) 
            SampleCounts();
            SampleMessages(0);
#endif
        }

        /// <summary>
        /// call this every frame to sample number of players and objects
        /// </summary>
        private void SampleCounts()
        {
            if (!NetworkServer.active)
                return;

            Counters.PlayerCount.Sample(NetworkServer.connections.Count);
            Counters.PlayerCount.Sample(NetworkManager.singleton.numPlayers);
            Counters.ObjectCount.Sample(NetworkServer.spawned.Count);
        }

        /// <summary>
        /// call this when ProfilerDriver shows it is next frame
        /// </summary>
        /// <param name="frame"></param>
        private void SampleMessages(int frame)
        {
            _sentCounter.EndFrame(frame);
            _receivedCounter.EndFrame(frame);
            AfterSample?.Invoke(frame);
        }
    }
}
