using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LiteNetLib
{
	internal readonly struct NativeAddr : IEquatable<NativeAddr>
	{
		//common parts
		private readonly long _part1; //family, port, etc

		private readonly long _part2;

		//ipv6 parts
		private readonly long _part3;
		private readonly int _part4;

		private readonly int _hash;

		public NativeAddr(byte[] address, int len)
		{
			_part1 = BitConverter.ToInt64(address, 0);
			_part2 = BitConverter.ToInt64(address, 8);
			if (len > 16)
			{
				_part3 = BitConverter.ToInt64(address, 16);
				_part4 = BitConverter.ToInt32(address, 24);
			}
			else
			{
				_part3 = 0;
				_part4 = 0;
			}
			_hash = (int) (_part1 >> 32) ^ (int) _part1 ^
			        (int) (_part2 >> 32) ^ (int) _part2 ^
			        (int) (_part3 >> 32) ^ (int) _part3 ^
			        _part4;
		}

		public override int GetHashCode()
		{
			return _hash;
		}

		public bool Equals(NativeAddr other)
		{
			return _part1 == other._part1 &&
			       _part2 == other._part2 &&
			       _part3 == other._part3 &&
			       _part4 == other._part4;
		}

		public override bool Equals(object obj)
		{
			return obj is NativeAddr other && Equals(other);
		}

		public static bool operator ==(NativeAddr left, NativeAddr right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(NativeAddr left, NativeAddr right)
		{
			return !left.Equals(right);
		}
	}

	internal class NativeEndPoint : IPEndPoint
	{
		public readonly byte[] NativeAddress;

		public NativeEndPoint(byte[] address) : base(IPAddress.Any, 0)
		{
			NativeAddress = new byte[address.Length];
			Buffer.BlockCopy(address, 0, NativeAddress, 0, address.Length);

			short family = (short) ((address[1] << 8) | address[0]);
			Port = (ushort) ((address[2] << 8) | address[3]);

			if ((NativeSocket.UnixMode && family == NativeSocket.AF_INET6) || (!NativeSocket.UnixMode && (AddressFamily) family == AddressFamily.InterNetworkV6))
			{
				uint scope = unchecked((uint) (
					(address[27] << 24) +
					(address[26] << 16) +
					(address[25] << 8) +
					(address[24])));
#if NETCOREAPP || NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
                Address = new IPAddress(new ReadOnlySpan<byte>(address, 8, 16), scope);
#else
				byte[] addrBuffer = new byte[16];
				Buffer.BlockCopy(address, 8, addrBuffer, 0, 16);
				Address = new IPAddress(addrBuffer, scope);
#endif
			}
			else //IPv4
			{
				long ipv4Addr = unchecked((uint) ((address[4] & 0x000000FF) |
				                                  (address[5] << 8 & 0x0000FF00) |
				                                  (address[6] << 16 & 0x00FF0000) |
				                                  (address[7] << 24)));
				Address = new IPAddress(ipv4Addr);
			}
		}
	}

	internal static class NativeSocket
	{
		static
#if LITENETLIB_UNSAFE
        unsafe
#endif
			class WinSock
		{
			private const string LibName = "ws2_32.dll";

			[DllImport(LibName, SetLastError = true)]
			public static extern int recvfrom(
				IntPtr socketHandle,
				[In, Out] byte[] pinnedBuffer,
				[In] int len,
				[In] SocketFlags socketFlags,
				[Out] byte[] socketAddress,
				[In, Out] ref int socketAddressSize);

			[DllImport(LibName, SetLastError = true)]
			internal static extern int sendto(
				IntPtr socketHandle,
#if LITENETLIB_UNSAFE
                byte* pinnedBuffer,
#else
				[In] byte[] pinnedBuffer,
#endif
				[In] int len,
				[In] SocketFlags socketFlags,
				[In] byte[] socketAddress,
				[In] int socketAddressSize);
		}

		static
#if LITENETLIB_UNSAFE
        unsafe
#endif
			class UnixSock
		{
			private const string LibName = "libc";

			[DllImport(LibName, SetLastError = true)]
			public static extern int recvfrom(
				IntPtr socketHandle,
				[In, Out] byte[] pinnedBuffer,
				[In] int len,
				[In] SocketFlags socketFlags,
				[Out] byte[] socketAddress,
				[In, Out] ref int socketAddressSize);

			[DllImport(LibName, SetLastError = true)]
			internal static extern int sendto(
				IntPtr socketHandle,
#if LITENETLIB_UNSAFE
                byte* pinnedBuffer,
#else
				[In] byte[] pinnedBuffer,
#endif
				[In] int len,
				[In] SocketFlags socketFlags,
				[In] byte[] socketAddress,
				[In] int socketAddressSize);
		}

		public static readonly bool IsSupported = false;
		public static readonly bool UnixMode = false;

		public const int IPv4AddrSize = 16;
		public const int IPv6AddrSize = 28;
		public const int AF_INET = 2;
		public const int AF_INET6 = 10;

		private static readonly Dictionary<int, SocketError> NativeErrorToSocketError = new Dictionary<int, SocketError>
		{
			{
				13, SocketError.AccessDenied
			}, //EACCES
			{
				98, SocketError.AddressAlreadyInUse
			}, //EADDRINUSE
			{
				99, SocketError.AddressNotAvailable
			}, //EADDRNOTAVAIL
			{
				97, SocketError.AddressFamilyNotSupported
			}, //EAFNOSUPPORT
			{
				11, SocketError.WouldBlock
			}, //EAGAIN
			{
				114, SocketError.AlreadyInProgress
			}, //EALREADY
			{
				9, SocketError.OperationAborted
			}, //EBADF
			{
				125, SocketError.OperationAborted
			}, //ECANCELED
			{
				103, SocketError.ConnectionAborted
			}, //ECONNABORTED
			{
				111, SocketError.ConnectionRefused
			}, //ECONNREFUSED
			{
				104, SocketError.ConnectionReset
			}, //ECONNRESET
			{
				89, SocketError.DestinationAddressRequired
			}, //EDESTADDRREQ
			{
				14, SocketError.Fault
			}, //EFAULT
			{
				112, SocketError.HostDown
			}, //EHOSTDOWN
			{
				6, SocketError.HostNotFound
			}, //ENXIO
			{
				113, SocketError.HostUnreachable
			}, //EHOSTUNREACH
			{
				115, SocketError.InProgress
			}, //EINPROGRESS
			{
				4, SocketError.Interrupted
			}, //EINTR
			{
				22, SocketError.InvalidArgument
			}, //EINVAL
			{
				106, SocketError.IsConnected
			}, //EISCONN
			{
				24, SocketError.TooManyOpenSockets
			}, //EMFILE
			{
				90, SocketError.MessageSize
			}, //EMSGSIZE
			{
				100, SocketError.NetworkDown
			}, //ENETDOWN
			{
				102, SocketError.NetworkReset
			}, //ENETRESET
			{
				101, SocketError.NetworkUnreachable
			}, //ENETUNREACH
			{
				23, SocketError.TooManyOpenSockets
			}, //ENFILE
			{
				105, SocketError.NoBufferSpaceAvailable
			}, //ENOBUFS
			{
				61, SocketError.NoData
			}, //ENODATA
			{
				2, SocketError.AddressNotAvailable
			}, //ENOENT
			{
				92, SocketError.ProtocolOption
			}, //ENOPROTOOPT
			{
				107, SocketError.NotConnected
			}, //ENOTCONN
			{
				88, SocketError.NotSocket
			}, //ENOTSOCK
			{
				3440, SocketError.OperationNotSupported
			}, //ENOTSUP
			{
				1, SocketError.AccessDenied
			}, //EPERM
			{
				32, SocketError.Shutdown
			}, //EPIPE
			{
				96, SocketError.ProtocolFamilyNotSupported
			}, //EPFNOSUPPORT
			{
				93, SocketError.ProtocolNotSupported
			}, //EPROTONOSUPPORT
			{
				91, SocketError.ProtocolType
			}, //EPROTOTYPE
			{
				94, SocketError.SocketNotSupported
			}, //ESOCKTNOSUPPORT
			{
				108, SocketError.Disconnecting
			}, //ESHUTDOWN
			{
				110, SocketError.TimedOut
			}, //ETIMEDOUT
			{
				0, SocketError.Success
			}
		};

		static NativeSocket()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				IsSupported = true;
				UnixMode = true;
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				IsSupported = true;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int RecvFrom(
			IntPtr socketHandle,
			byte[] pinnedBuffer,
			int len,
			byte[] socketAddress,
			ref int socketAddressSize)
		{
			return UnixMode
				? UnixSock.recvfrom(socketHandle, pinnedBuffer, len, 0, socketAddress, ref socketAddressSize)
				: WinSock.recvfrom(socketHandle, pinnedBuffer, len, 0, socketAddress, ref socketAddressSize);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public
#if LITENETLIB_UNSAFE
            unsafe
#endif
			static int SendTo(
				IntPtr socketHandle,
#if LITENETLIB_UNSAFE
            byte* pinnedBuffer,
#else
				byte[] pinnedBuffer,
#endif
				int len,
				byte[] socketAddress,
				int socketAddressSize)
		{
			return UnixMode
				? UnixSock.sendto(socketHandle, pinnedBuffer, len, 0, socketAddress, socketAddressSize)
				: WinSock.sendto(socketHandle, pinnedBuffer, len, 0, socketAddress, socketAddressSize);
		}

		public static SocketError GetSocketError()
		{
			int error = Marshal.GetLastWin32Error();
			if (UnixMode)
				return NativeErrorToSocketError.TryGetValue(error, out var err)
					? err
					: SocketError.SocketError;
			return (SocketError) error;
		}

		public static SocketException GetSocketException()
		{
			int error = Marshal.GetLastWin32Error();
			if (UnixMode)
				return NativeErrorToSocketError.TryGetValue(error, out var err)
					? new SocketException((int) err)
					: new SocketException((int) SocketError.SocketError);
			return new SocketException(error);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short GetNativeAddressFamily(IPEndPoint remoteEndPoint)
		{
			return UnixMode
				? (short) (remoteEndPoint.AddressFamily == AddressFamily.InterNetwork ? AF_INET : AF_INET6)
				: (short) remoteEndPoint.AddressFamily;
		}
	}
}
