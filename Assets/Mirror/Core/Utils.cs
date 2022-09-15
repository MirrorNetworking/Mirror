using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine;

namespace Mirror
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkConnection conn, NetworkReader reader, int channelId);

    // Handles requests to spawn objects on the client
    public delegate GameObject SpawnDelegate(Vector3 position, uint assetId);

    public delegate GameObject SpawnHandlerDelegate(SpawnMessage msg);

    // Handles requests to unspawn objects on the client
    public delegate void UnSpawnDelegate(GameObject spawned);

    // channels are const ints instead of an enum so people can add their own
    // channels (can't extend an enum otherwise).
    //
    // note that Mirror is slowly moving towards quake style networking which
    // will only require reliable for handshake, and unreliable for the rest.
    // so eventually we can change this to an Enum and transports shouldn't
    // add custom channels anymore.
    public static class Channels
    {
        public const int Reliable = 0;   // ordered
        public const int Unreliable = 1; // unordered
    }

    public static class Utils
    {
        public static uint GetTrueRandomUInt()
        {
            // use Crypto RNG to avoid having time based duplicates
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
        }

        public static bool IsPrefab(GameObject obj)
        {
#if UNITY_EDITOR
            return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(obj);
#else
            return false;
#endif
        }

        // simplified IsSceneObject check from Mirror II
        public static bool IsSceneObject(NetworkIdentity identity)
        {
            // original UNET / Mirror still had the IsPersistent check.
            // it never fires though. even for Prefabs dragged to the Scene.
            // (see Scene Objects example scene.)
            // #if UNITY_EDITOR
            //             if (UnityEditor.EditorUtility.IsPersistent(identity.gameObject))
            //                 return false;
            // #endif

            return identity.gameObject.hideFlags != HideFlags.NotEditable &&
                identity.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                identity.sceneId != 0;
        }

        public static bool IsSceneObjectWithPrefabParent(GameObject gameObject, out GameObject prefab)
        {
            prefab = null;

#if UNITY_EDITOR
            if (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                return false;
            }
            prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#endif

            if (prefab == null)
            {
                Debug.LogError($"Failed to find prefab parent for scene object [name:{gameObject.name}]");
                return false;
            }
            return true;
        }

        // is a 2D point in screen? (from ummorpg)
        // (if width = 1024, then indices from 0..1023 are valid (=1024 indices)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPointInScreen(Vector2 point) =>
            0 <= point.x && point.x < Screen.width &&
            0 <= point.y && point.y < Screen.height;

        // pretty print bytes as KB/MB/GB/etc. from DOTSNET
        // long to support > 2GB
        // divides by floats to return "2.5MB" etc.
        public static string PrettyBytes(long bytes)
        {
            // bytes
            if (bytes < 1024)
                return $"{bytes} B";
            // kilobytes
            else if (bytes < 1024L * 1024L)
                return $"{(bytes / 1024f):F2} KB";
            // megabytes
            else if (bytes < 1024 * 1024L * 1024L)
                return $"{(bytes / (1024f * 1024f)):F2} MB";
            // gigabytes
            return $"{(bytes / (1024f * 1024f * 1024f)):F2} GB";
        }

        // pretty print seconds as hours:minutes:seconds(.milliseconds/100)s.
        // double for long running servers.
        public static string PrettySeconds(double seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            string res = "";
            if (t.Days > 0) res += $"{t.Days}d";
            if (t.Hours > 0) res += $"{(res.Length > 0 ? " " : "")}{t.Hours}h";
            if (t.Minutes > 0) res += $"{(res.Length > 0 ? " " : "")}{t.Minutes}m";
            // 0.5s, 1.5s etc. if any milliseconds. 1s, 2s etc. if any seconds
            if (t.Milliseconds > 0) res += $"{(res.Length > 0 ? " " : "")}{t.Seconds}.{(t.Milliseconds / 100)}s";
            else if (t.Seconds > 0) res += $"{(res.Length > 0 ? " " : "")}{t.Seconds}s";
            // if the string is still empty because the value was '0', then at least
            // return the seconds instead of returning an empty string
            return res != "" ? res : "0s";
        }

        // universal .spawned function
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkIdentity GetSpawnedInServerOrClient(uint netId)
        {
            // server / host mode: use the one from server.
            // host mode has access to all spawned.
            if (NetworkServer.active)
            {
                NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity entry);
                return entry;
            }

            // client
            if (NetworkClient.active)
            {
                NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity entry);
                return entry;
            }

            return null;
        }

        // keep a GUI window in screen.
        // for example. if it's at x=1000 and screen is resized to w=500,
        // it won't get lost in the invisible area etc.
        public static Rect KeepInScreen(Rect rect)
        {
            // ensure min
            rect.x = Math.Max(rect.x, 0);
            rect.y = Math.Max(rect.y, 0);

            // ensure max
            rect.x = Math.Min(rect.x, Screen.width - rect.width);
            rect.y = Math.Min(rect.y, Screen.width - rect.height);

            return rect;
        }
    }
}
