// ---------------------------------------------------------------------
// Copyright 2018 David Haig
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// ---------------------------------------------------------------------

using System;

namespace Ninja.WebSockets.Internal
{
    internal static class WebSocketFrameCommon
    {
        public const int MaskKeyLength = 4;

        /// <summary>
        /// Mutate payload with the mask key
        /// This is a reversible process
        /// If you apply this to masked data it will be unmasked and visa versa
        /// </summary>
        /// <param name="maskKey">The 4 byte mask key</param>
        /// <param name="payload">The payload to mutate</param>
        public static void ToggleMask(ArraySegment<byte> maskKey, ArraySegment<byte> payload)
        {
            if (maskKey.Count != MaskKeyLength)
            {
                throw new Exception($"MaskKey key must be {MaskKeyLength} bytes");
            }

            byte[] buffer = payload.Array;
            byte[] maskKeyArray = maskKey.Array;
            int payloadOffset = payload.Offset;
            int payloadCount = payload.Count;
            int maskKeyOffset = maskKey.Offset;

            // apply the mask key (this is a reversible process so no need to copy the payload)
            // NOTE: this is a hot function
            // TODO: make this faster
            for (int i = payloadOffset; i < payloadCount; i++)
            {
                int payloadIndex = i - payloadOffset; // index should start at zero
                int maskKeyIndex = maskKeyOffset + (payloadIndex % MaskKeyLength);
                buffer[i] = (Byte)(buffer[i] ^ maskKeyArray[maskKeyIndex]);
            }
        }
    }
}
