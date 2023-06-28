// standalone lag compensation algorithm
// based on the Valve Networking Model:
// https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking
using System.Collections.Generic;

namespace Mirror
{
    public static class LagCompensation
    {
        // TODO ringbuffer
        public static void InsertCapture<T>(List<T> history, T capture) where T : Capture
        {
            // TODO
        }
    }
}
