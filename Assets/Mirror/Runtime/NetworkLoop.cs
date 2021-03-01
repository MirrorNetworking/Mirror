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
using UnityEngine;

namespace Mirror
{
    internal static class NetworkLoop
    {
    }
}
