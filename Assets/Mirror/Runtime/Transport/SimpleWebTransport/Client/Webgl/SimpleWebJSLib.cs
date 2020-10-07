using System;
using System.Runtime.InteropServices;

namespace Mirror.SimpleWeb
{
    internal static class SimpleWebJSLib
    {
#if UNITY_WEBGL
        [DllImport("__Internal")]
        internal static extern bool IsConnected();

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
        [DllImport("__Internal")]
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
        internal static extern void Connect(string address, Action openCallback, Action closeCallBack, Action<IntPtr, int> messageCallback, Action errorCallback);

        [DllImport("__Internal")]
        internal static extern void Disconnect();

        [DllImport("__Internal")]
        internal static extern bool Send(byte[] array, int offset, int length);
#else
        internal static bool IsConnected() => throw new NotSupportedException();

        internal static void Connect(string address, Action openCallback, Action closeCallBack, Action<IntPtr, int> messageCallback, Action errorCallback) => throw new NotSupportedException();

        internal static void Disconnect() => throw new NotSupportedException();

        internal static bool Send(byte[] array, int offset, int length) => throw new NotSupportedException();
#endif
    }
}
