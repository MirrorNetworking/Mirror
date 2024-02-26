using System;
using System.Runtime.InteropServices;
using System.Security;
using UnityEngine;
namespace Mirror.Transports.Encryption.Native
{
    public class AesGCMEncryptionNative
    {
        [SuppressUnmanagedCodeSecurity]
        [DllImport("rusty_mirror_encryption", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern byte is_supported();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("rusty_mirror_encryption", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern uint aes_gcm_encrypt(
            UIntPtr key, uint key_size,
            UIntPtr nonce, uint nonce_size,
            UIntPtr data, uint data_in_size, uint data_capacity);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("rusty_mirror_encryption", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern uint aes_gcm_decrypt(
            UIntPtr key, uint key_size,
            UIntPtr nonce, uint nonce_size,
            UIntPtr data, uint data_size);

        private static bool _supported;
        static AesGCMEncryptionNative()
        {
            try
            {
                _supported = is_supported() == 1;
            }
            catch (Exception e)
            {
                // TODO: silent?
                Debug.LogWarning($"Native AES GCM is not supported: {e}");
            }
        }
        public static bool IsSupported => _supported;
        public static unsafe ArraySegment<byte> Encrypt(byte[] key, byte[] nonce, ArraySegment<byte> plaintext, ArraySegment<byte> dataOut)
        {
            Array.Copy(plaintext.Array, plaintext.Offset, dataOut.Array, dataOut.Offset ,plaintext.Count);
            fixed (byte* keyPtr = key)
            {
                fixed (byte* noncePtr = nonce)
                {
                    fixed (byte* dataPtr = dataOut.Array)
                    {
                        UIntPtr data = ((UIntPtr)dataPtr) + dataOut.Offset;
                        uint resultLength = aes_gcm_encrypt(
                            (UIntPtr)keyPtr, (uint)key.Length,
                            (UIntPtr)noncePtr, (uint)nonce.Length,
                            data, (uint)plaintext.Count, (uint)dataOut.Count);
                        return new ArraySegment<byte>(dataOut.Array, dataOut.Offset, (int)resultLength);
                    }
                }
            }
        }
        
        public static unsafe ArraySegment<byte> Decrypt(byte[] key, byte[] nonce, ArraySegment<byte> ciphertext, ArraySegment<byte> dataOut)
        {
            Array.Copy(ciphertext.Array, ciphertext.Offset, dataOut.Array, dataOut.Offset, ciphertext.Count);
            fixed (byte* keyPtr = key)
            {
                fixed (byte* noncePtr = nonce)
                {
                    fixed (byte* dataPtr = dataOut.Array)
                    {
                        UIntPtr data = ((UIntPtr)dataPtr) + dataOut.Offset;
                        uint resultLength = aes_gcm_decrypt(
                            (UIntPtr)keyPtr, (uint)key.Length,
                            (UIntPtr)noncePtr, (uint)nonce.Length,
                            data, (uint)ciphertext.Count);
                        return new ArraySegment<byte>(dataOut.Array, dataOut.Offset, (int)resultLength);
                    }
                }
            }
        }
    }
}
