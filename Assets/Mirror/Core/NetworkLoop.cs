// our ideal update looks like this:
//   transport.process_incoming()
//   update_world()
//   transport.process_outgoing()
//
// this way we avoid unnecessary latency for low-ish server tick rates.
// for example, if we were to use this tick:
//   transport.process_incoming/outgoing()
//   update_world()
//
// then anything sent in update_world wouldn't be actually sent out by the
// transport until the next frame. if server runs at 60Hz, then this can add
// 16ms latency for every single packet.
//
// => instead we process incoming, update world, process_outgoing in the same
//    frame. it's more clear (no race conditions) and lower latency.
// => we need to add custom Update functions to the Unity engine:
//      NetworkEarlyUpdate before Update()/FixedUpdate()
//      NetworkLateUpdate after LateUpdate()
//    this way the user can update the world in Update/FixedUpdate/LateUpdate
//    and networking still runs before/after those functions no matter what!
// => see also: https://docs.unity3d.com/Manual/ExecutionOrder.html
// => update order:
//    * we add to the end of EarlyUpdate so it runs after any Unity initializations
//    * we add to the end of PreLateUpdate so it runs after LateUpdate(). adding
//      to the beginning of PostLateUpdate doesn't actually work.
using System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Mirror
{
    public static class NetworkLoop
    {
        // helper enum to add loop to begin/end of subSystemList
        internal enum AddMode { Beginning, End }

        // callbacks for others to hook into if they need Early/LateUpdate.
        public static Action OnEarlyUpdate;
        public static Action OnLateUpdate;

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetStatics()
        {
            OnEarlyUpdate = null;
            OnLateUpdate = null;
        }

        // helper function to find an update function's index in a player loop
        // type. this is used for testing to guarantee our functions are added
        // at the beginning/end properly.
        internal static int FindPlayerLoopEntryIndex(PlayerLoopSystem.UpdateFunction function, PlayerLoopSystem playerLoop, Type playerLoopSystemType)
        {
            // did we find the type? e.g. EarlyUpdate/PreLateUpdate/etc.
            if (playerLoop.type == playerLoopSystemType)
                return Array.FindIndex(playerLoop.subSystemList, (elem => elem.updateDelegate == function));

            // recursively keep looking
            if (playerLoop.subSystemList != null)
            {
                for (int i = 0; i < playerLoop.subSystemList.Length; ++i)
                {
                    int index = FindPlayerLoopEntryIndex(function, playerLoop.subSystemList[i], playerLoopSystemType);
                    if (index != -1) return index;
                }
            }
            return -1;
        }

        // MODIFIED AddSystemToPlayerLoopList from Unity.Entities.ScriptBehaviourUpdateOrder (ECS)
        //
        // => adds an update function to the Unity internal update type.
        // => Unity has different update loops:
        //    https://medium.com/@thebeardphantom/unity-2018-and-playerloop-5c46a12a677
        //      EarlyUpdate
        //      FixedUpdate
        //      PreUpdate
        //      Update
        //      PreLateUpdate
        //      PostLateUpdate
        //
        // function: the custom update function to add
        //           IMPORTANT: according to a comment in Unity.Entities.ScriptBehaviourUpdateOrder,
        //                      the UpdateFunction can not be virtual because
        //                      Mono 4.6 has problems invoking virtual methods
        //                      as delegates from native!
        // ownerType: the .type to fill in so it's obvious who the new function
        //            belongs to. seems to be mostly for debugging. pass any.
        // addMode: prepend or append to update list
        internal static bool AddToPlayerLoop(PlayerLoopSystem.UpdateFunction function, Type ownerType, ref PlayerLoopSystem playerLoop, Type playerLoopSystemType, AddMode addMode)
        {
            // did we find the type? e.g. EarlyUpdate/PreLateUpdate/etc.
            if (playerLoop.type == playerLoopSystemType)
            {
                // debugging
                //Debug.Log($"Found playerLoop of type {playerLoop.type} with {playerLoop.subSystemList.Length} Functions:");
                //foreach (PlayerLoopSystem sys in playerLoop.subSystemList)
                //    Debug.Log($"  ->{sys.type}");

                // make sure the function wasn't added yet.
                // with domain reload disabled, it would otherwise be added twice:
                // fixes: https://github.com/MirrorNetworking/Mirror/issues/3392
                if (Array.FindIndex(playerLoop.subSystemList, (s => s.updateDelegate == function)) != -1)
                {
                    // loop contains the function, so return true.
                    return true;
                }

                // resize & expand subSystemList to fit one more entry
                int oldListLength = (playerLoop.subSystemList != null) ? playerLoop.subSystemList.Length : 0;
                Array.Resize(ref playerLoop.subSystemList, oldListLength + 1);

                // IMPORTANT: always insert a FRESH PlayerLoopSystem!
                // We CAN NOT resize and then OVERWRITE an entry's type/loop.
                // => PlayerLoopSystem has native IntPtr loop members
                // => forgetting to clear those would cause undefined behaviour!
                // see also: https://github.com/vis2k/Mirror/pull/2652
                PlayerLoopSystem system = new PlayerLoopSystem {
                    type = ownerType,
                    updateDelegate = function
                };

                // prepend our custom loop to the beginning
                if (addMode == AddMode.Beginning)
                {
                    // shift to the right, write into first array element
                    Array.Copy(playerLoop.subSystemList, 0, playerLoop.subSystemList, 1, playerLoop.subSystemList.Length - 1);
                    playerLoop.subSystemList[0] = system;
                }
                // append our custom loop to the end
                else if (addMode == AddMode.End)
                {
                    // simply write into last array element
                    playerLoop.subSystemList[oldListLength] = system;
                }

                // debugging
                //Debug.Log($"New playerLoop of type {playerLoop.type} with {playerLoop.subSystemList.Length} Functions:");
                //foreach (PlayerLoopSystem sys in playerLoop.subSystemList)
                //    Debug.Log($"  ->{sys.type}");

                return true;
            }

            // recursively keep looking
            if (playerLoop.subSystemList != null)
            {
                for (int i = 0; i < playerLoop.subSystemList.Length; ++i)
                {
                    if (AddToPlayerLoop(function, ownerType, ref playerLoop.subSystemList[i], playerLoopSystemType, addMode))
                        return true;
                }
            }
            return false;
        }

        // hook into Unity runtime to actually add our custom functions
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RuntimeInitializeOnLoad()
        {
            //Debug.Log("Mirror: adding Network[Early/Late]Update to Unity...");

            // get loop
            // 2019 has GetCURRENTPlayerLoop which is safe to use without
            // breaking other custom system's custom loops.
            // see also: https://github.com/vis2k/Mirror/pull/2627/files
            PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();

            // add NetworkEarlyUpdate to the end of EarlyUpdate so it runs after
            // any Unity initializations but before the first Update/FixedUpdate
            AddToPlayerLoop(NetworkEarlyUpdate, typeof(NetworkLoop), ref playerLoop, typeof(EarlyUpdate), AddMode.End);

            // add NetworkLateUpdate to the end of PreLateUpdate so it runs after
            // LateUpdate(). adding to the beginning of PostLateUpdate doesn't
            // actually work.
            AddToPlayerLoop(NetworkLateUpdate, typeof(NetworkLoop), ref playerLoop, typeof(PreLateUpdate), AddMode.End);

            // set the new loop
            PlayerLoop.SetPlayerLoop(playerLoop);
        }

        static void NetworkEarlyUpdate()
        {
            // loop functions run in edit mode and in play mode.
            // however, we only want to call NetworkServer/Client in play mode.
            if (!Application.isPlaying) return;

            //Debug.Log($"NetworkEarlyUpdate {Time.time}");
            NetworkServer.NetworkEarlyUpdate();
            NetworkClient.NetworkEarlyUpdate();
            // invoke event after mirror has done it's early updating.
            OnEarlyUpdate?.Invoke();
        }

        static void NetworkLateUpdate()
        {
            // loop functions run in edit mode and in play mode.
            // however, we only want to call NetworkServer/Client in play mode.
            if (!Application.isPlaying) return;

            //Debug.Log($"NetworkLateUpdate {Time.time}");
            // invoke event before mirror does its final late updating.
            OnLateUpdate?.Invoke();
            NetworkServer.NetworkLateUpdate();
            NetworkClient.NetworkLateUpdate();
        }
    }
}
