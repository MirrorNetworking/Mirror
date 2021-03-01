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

using System;
using UnityEngine;
using UnityEngine.Experimental.LowLevel;

namespace Mirror
{
    internal static class NetworkLoop
    {
        // AddSystemToPlayerLoopList from
        // com.unity.entities/ScriptBehaviourUpdateOrder (ECS)
        // => adds an update function to the Unity internal update loop.
        // => Unity has different update loops:
        //    https://medium.com/@thebeardphantom/unity-2018-and-playerloop-5c46a12a677
        //      EarlyUpdate
        //      FixedUpdate
        //      PreUpdate
        //      Update
        //      PreLateUpdate
        //      PostLateUpdate
        internal static bool AddSystemToPlayerLoopList(Action CustomLoop, ref PlayerLoopSystem playerLoop, Type playerLoopSystemType)
        {
            // did we find the type? e.g. EarlyUpdate/PreLateUpdate/etc.
            if (playerLoop.type == playerLoopSystemType)
            {
                /*DummyDelegateWrapper del = new DummyDelegateWrapper(system);
                int oldListLength = (playerLoop.subSystemList != null) ? playerLoop.subSystemList.Length : 0;
                PlayerLoopSystem[] newSubsystemList = new PlayerLoopSystem[oldListLength + 1];
                for (int i = 0; i < oldListLength; ++i)
                    newSubsystemList[i] = playerLoop.subSystemList[i];
                newSubsystemList[oldListLength].type = CustomLoop.GetType(); // TODO
                newSubsystemList[oldListLength].updateDelegate = del.TriggerUpdate;
                playerLoop.subSystemList = newSubsystemList;*/
                Debug.LogWarning($"Found playerLoop of type {playerLoop.type}");
                return true;
            }
            // recursively keep looking
            if (playerLoop.subSystemList != null)
            {
                for(int i=0; i<playerLoop.subSystemList.Length; ++i)
                {
                    if (AddSystemToPlayerLoopList(CustomLoop, ref playerLoop.subSystemList[i], playerLoopSystemType))
                        return true;
                }
            }
            return false;
        }
    }
}
