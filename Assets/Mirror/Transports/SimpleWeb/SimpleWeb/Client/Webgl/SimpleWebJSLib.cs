using System;
using System.Runtime.InteropServices;

namespace Mirror.SimpleWeb
{
    internal static class SimpleWebJSLib
    {
#if UNITY_WEBGL
        [DllImport("__Internal")]
        internal static extern bool IsConnected(int index);

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
        [DllImport("__Internal")]
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
        internal static extern int Connect(string address,
            Action<int> openCallback,
            Action<int> closeCallBack,
            Action<int, IntPtr, int> messageCallback,
            Action<int> errorCallback,
            IntPtr incomingDataBuffer,
            int incomingDataBufferLength
            );

        [DllImport("__Internal")]
        internal static extern void Disconnect(int index);

        [DllImport("__Internal")]
        internal static extern bool Send(int index, IntPtr ptr, int length);
#else
        internal static bool IsConnected(int index) => throw new NotSupportedException();

        internal static int Connect(string address,
            Action<int> openCallback,
            Action<int> closeCallBack,
            Action<int, IntPtr, int> messageCallback,
            Action<int> errorCallback,
            IntPtr incomingDataBuffer,
            int incomingDataBufferLength
            )
            => throw new NotSupportedException();

        internal static void Disconnect(int index) => throw new NotSupportedException();

        internal static bool Send(int index, IntPtr ptr, int length) => throw new NotSupportedException();
#endif

#if UNITY_2021_3_OR_NEWER
        /// <summary>Pins the span and passes its start pointer directly — no offset arithmetic needed in JS.</summary>
        internal static unsafe bool Send(int index, ReadOnlySpan<byte> span)
        {
            fixed (byte* ptr = span)
                return Send(index, new IntPtr(ptr), span.Length);
        }

        /// <summary>Compat overload for callers that still have array + offset.</summary>
        internal static unsafe bool Send(int index, byte[] array, int offset, int length)
            => Send(index, new ReadOnlySpan<byte>(array, offset, length));
#else
        /// <summary>Pins the array via fixed and applies offset — no Span required.</summary>
        internal static unsafe bool Send(int index, byte[] array, int offset, int length)
        {
            fixed (byte* ptr = &array[offset])
                return Send(index, new IntPtr(ptr), length);
        }
#endif
    }
}
