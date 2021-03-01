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
// => we want to add to the end of EarlyUpdate and to the beginning of
//    PostLateUpdate. we DO NOT want to add to the end of PostLateUpdate
//    after Unity's magic systems like MemoryFrameMaintenance ran.

using System;
using UnityEngine;
using UnityEngine.Experimental.LowLevel;

namespace Mirror
{
    internal static class NetworkLoop
    {
        // helper enum to add loop to begin/end of subSystemList

        // AddSystemToPlayerLoopList from Unity.Entities.ScriptBehaviourUpdateOrder (ECS)
        // => adds an update function to the END of the Unity internal update type.
        // => Unity has different update loops:
        //    https://medium.com/@thebeardphantom/unity-2018-and-playerloop-5c46a12a677
        //      EarlyUpdate
        //      FixedUpdate
        //      PreUpdate
        //      Update
        //      PreLateUpdate
        //      PostLateUpdate
        internal static bool AppendSystemToPlayerLoopList(Action CustomLoop, ref PlayerLoopSystem playerLoop, Type playerLoopSystemType)
        {
            // did we find the type? e.g. EarlyUpdate/PreLateUpdate/etc.
            if (playerLoop.type == playerLoopSystemType)
            {
                Debug.Log($"Found playerLoop of type {playerLoop.type}");

                // resize & expand subSystemList to fit one more entry
                // TODO use Array.Resize later if it's safe
                int oldListLength = (playerLoop.subSystemList != null) ? playerLoop.subSystemList.Length : 0;
                PlayerLoopSystem[] newSubsystemList = new PlayerLoopSystem[oldListLength + 1];
                for (int i = 0; i < oldListLength; ++i)
                    newSubsystemList[i] = playerLoop.subSystemList[i];

                // add our custom loop at the end
                // TODO optional 'add to beginning' for prelateupdate!
                //DummyDelegateWrapper del = new DummyDelegateWrapper(system);
                //newSubsystemList[oldListLength].type = CustomLoop.GetType(); // TODO
                //newSubsystemList[oldListLength].updateDelegate = del.TriggerUpdate;

                // assign the new subSystemList
                playerLoop.subSystemList = newSubsystemList;
                return true;
            }
            // recursively keep looking
            if (playerLoop.subSystemList != null)
            {
                for(int i=0; i<playerLoop.subSystemList.Length; ++i)
                {
                    if (AppendSystemToPlayerLoopList(CustomLoop, ref playerLoop.subSystemList[i], playerLoopSystemType))
                        return true;
                }
            }
            return false;
        }
    }
}
