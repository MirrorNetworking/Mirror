using System.Net;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Linq;

namespace Mirror
{
    public sealed class IPAddressUtility
    {
        private IPAddressUtility() { }

        public static IPAddress[] GetBroadcastAdresses()
        {
            // try multiple methods - because some of them may fail on some devices, especially if IL2CPP comes into play
            IPAddress[] ips = null;

            NetworkDiscoveryUtility.RunSafe(() => ips = GetBroadcastAdressesFromNetworkInterfaces(), false);

            if (ips == null || ips.Length < 1)
            {
                // try another method
                NetworkDiscoveryUtility.RunSafe(() => ips = GetBroadcastAdressesFromHostEntry(), false);
            }

            if (ips == null || ips.Length < 1)
            {
                // all methods failed, or there is no network interface on this device
                // just use full-broadcast address
                ips = new IPAddress[] { IPAddress.Broadcast };
            }

            return ips;
        }

        static IPAddress[] GetBroadcastAdressesFromNetworkInterfaces()
        {
            List<IPAddress> ips = new List<IPAddress>();

            var nifs = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nif => nif.OperationalStatus == OperationalStatus.Up)
                .Where(nif => nif.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || nif.NetworkInterfaceType == NetworkInterfaceType.Ethernet);

            foreach (var nif in nifs)
            {
                foreach (UnicastIPAddressInformation ipInfo in nif.GetIPProperties().UnicastAddresses)
                {
                    var ip = ipInfo.Address;
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (ToBroadcastAddress(ref ip, ipInfo.IPv4Mask))
                            ips.Add(ip);
                    }
                }
            }

            return ips.ToArray();
        }

        static IPAddress[] GetBroadcastAdressesFromHostEntry()
        {
            var ips = new List<IPAddress>();

            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var address in hostEntry.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    // this is IPv4 address
                    // convert it to broadcast address
                    // use default subnet
                    var subnetMask = GetDefaultSubnetMask(address);

                    if (subnetMask != null)
                    {
                        var broadcastAddress = address;
                        if (ToBroadcastAddress(ref broadcastAddress, subnetMask))
                        {
                            ips.Add(broadcastAddress);
                        }
                    }
                }
            }

            if (ips.Count > 0)
            {
                // if we found at least 1 ip, then also add full-broadcast address
                // this will compensate in case we used a wrong subnet mask
                ips.Add(IPAddress.Broadcast);
            }

            return ips.ToArray();
        }

        static bool ToBroadcastAddress(ref IPAddress ip, IPAddress subnetMask)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork || subnetMask.AddressFamily != AddressFamily.InterNetwork)
                return false;

            byte[] bytes = ip.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            for (int i = 0; i < 4; i++)
            {
                // on places where subnet mask has 1s, address bits are copied,
                // and on places where subnet mask has 0s, address bits are 1
                bytes[i] = (byte)((~subnetMaskBytes[i]) | bytes[i]);
            }

            ip = new IPAddress(bytes);

            return true;
        }

        static IPAddress GetDefaultSubnetMask(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                return null;

            IPAddress subnetMask;

            byte[] bytes = ip.GetAddressBytes();
            byte firstByte = bytes[0];

            if (firstByte >= 0 && firstByte <= 127)
                subnetMask = new IPAddress(new byte[] { 255, 0, 0, 0 });
            else if (firstByte >= 128 && firstByte <= 191)
                subnetMask = new IPAddress(new byte[] { 255, 255, 0, 0 });
            else if (firstByte >= 192 && firstByte <= 223)
                subnetMask = new IPAddress(new byte[] { 255, 255, 255, 0 });
            else
            { 
                // undefined subnet
                subnetMask = null;
            }

            return subnetMask;
        }
    }
}