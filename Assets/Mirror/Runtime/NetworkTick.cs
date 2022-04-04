using UnityEngine;

namespace Mirror
{
     public class NetworkTick {
    /* Configuratuins */

    //private static int _ticksPerSecond = Mathf.RoundToInt(1 / Time.deltaTime); // Get number ot ticks per second - more usefull for converting time to ticks
    private static int _ticksPerSecond = Mathf.RoundToInt(1 / Time.fixedDeltaTime); // Get number ot ticks per second - more usefull for converting time to ticks

    /* Initial variables */
    private static bool _isReady       = false; // Ticks are in sync
    private static bool _isInitialized = false; // Ticks are initialized

    private static int _currentNetworkTick            = 0; // Server tick
    private static int _currentNetworkTickOffset      = 0; // 1/2 Rtt converted to Ticks
    private static int _currentNetworkTickLocalOffset = 0; // Difference between Client ticks and Network ticks ( used in case client is lagging for some reason

    /***************************************/
    /* Static variables for project access */
    /***************************************/

    /* Tick int variables */
    public static bool IsReady       => _isReady;

    public static int TickPerSecond        => _ticksPerSecond;
    public static int ServerTick           => _currentNetworkTick;
    public static int ClientOffsetTick     => _currentNetworkTickOffset;
    public static int ClientPredictionTick => _currentNetworkTick + _currentNetworkTickOffset;

    /* Tick based time float variables */
    public static float ServerTime           => (float) _currentNetworkTick / _ticksPerSecond;
    public static float ClientOffsetTime     => (float) _currentNetworkTickOffset / _ticksPerSecond;
    public static float ClientPredictionTime => (float) (_currentNetworkTick + _currentNetworkTickOffset) / _ticksPerSecond;


    /* Network Tick Control functions  */

    public void SetIsReady(bool isReady)            => _isReady = isReady;
    public int  GetServerTickPerSecond()            => _ticksPerSecond;
    public int  SetServerTickPerSecond(int newTick) => _ticksPerSecond = newTick;

    public int  GetServerTick()                               => _currentNetworkTick;
    public int  SetServerTick(int newTick)                    => _currentNetworkTick = newTick;
    public int  GetClientTickOffset()                         => _currentNetworkTickOffset;
    public void SetClientTickOffset(int newNetworkTickOffset) => _currentNetworkTickOffset = newNetworkTickOffset;
    public int  GetTickLocalOffset()                          => _currentNetworkTickLocalOffset;
    public int  SetTickLocalOffset(int newTickOffset)         => _currentNetworkTickLocalOffset = newTickOffset;


    /* to avoid accessing static methos when NetworkTick is instanciated */
    public int   GetPredictionTick()       => _currentNetworkTick + _currentNetworkTickOffset;
    public float GetServerTime()           => (float) _currentNetworkTick / _ticksPerSecond;
    public float GetServerPredictionTime() => (float) (_currentNetworkTick + _currentNetworkTickOffset) / _ticksPerSecond;
  }
}
