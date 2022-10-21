using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        public override string address =>
            Transport.active.ServerGetClientAddress(connectionId);

        /// <summary>NetworkIdentities that this connection can see</summary>
        // TODO move to server's NetworkConnectionToClient?
        public new readonly HashSet<NetworkIdentity> observing = new HashSet<NetworkIdentity>();

        [Obsolete(".clientOwnedObjects was renamed to .owned :)")] // 2022-10-13
        public new HashSet<NetworkIdentity> clientOwnedObjects => owned;

        // unbatcher
        public Unbatcher unbatcher = new Unbatcher();

        // in host mode, we apply snapshot interpolation to for each connection.
        // this way other players are still smooth on hosted games.
        // in other words, we still need ema etc. on server here.
        public ExponentialMovingAverage serverDriftEma;
        public ExponentialMovingAverage serverDeliveryTimeEma; // average delivery time (standard deviation gives average jitter)
        public double serverTimeline;
        public double serverTimescale;
        public double serverBufferTimeMultiplier = 2;
        public double serverBufferTime => NetworkServer.sendInterval * serverBufferTimeMultiplier;

        // <clienttime, snaps>
        public SortedList<double, TimeSnapshot> serverTimeSnapshots = new SortedList<double, TimeSnapshot>();

        public NetworkConnectionToClient(int networkConnectionId)
            : base(networkConnectionId)
        {
            // initialize EMA with 'emaDuration' seconds worth of history.
            // 1 second holds 'sendRate' worth of values.
            // multiplied by emaDuration gives n-seconds.
            serverDriftEma        = new ExponentialMovingAverage(NetworkServer.sendRate * NetworkClient.driftEmaDuration);
            serverDeliveryTimeEma = new ExponentialMovingAverage(NetworkServer.sendRate * NetworkClient.deliveryTimeEmaDuration);
        }

        // Send stage three: hand off to transport
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable) =>
            Transport.active.ServerSend(connectionId, segment, channelId);

        /// <summary>Disconnects this connection.</summary>
        public override void Disconnect()
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            Transport.active.ServerDisconnect(connectionId);

            // IMPORTANT: NetworkConnection.Disconnect() is NOT called for
            // voluntary disconnects from the other end.
            // -> so all 'on disconnect' cleanup code needs to be in
            //    OnTransportDisconnect, where it's called for both voluntary
            //    and involuntary disconnects!
        }

        internal void AddToObserving(NetworkIdentity netIdentity)
        {
            observing.Add(netIdentity);

            // spawn identity for this conn
            NetworkServer.ShowForConnection(netIdentity, this);
        }

        internal void RemoveFromObserving(NetworkIdentity netIdentity, bool isDestroyed)
        {
            observing.Remove(netIdentity);

            if (!isDestroyed)
            {
                // hide identity for this conn
                NetworkServer.HideForConnection(netIdentity, this);
            }
        }

        internal void RemoveFromObservingsObservers()
        {
            foreach (NetworkIdentity netIdentity in observing)
            {
                netIdentity.RemoveObserver(this);
            }
            observing.Clear();
        }

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            owned.Add(obj);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            owned.Remove(obj);
        }

        internal void DestroyOwnedObjects()
        {
            // create a copy because the list might be modified when destroying
            HashSet<NetworkIdentity> tmp = new HashSet<NetworkIdentity>(owned);
            foreach (NetworkIdentity netIdentity in tmp)
            {
                if (netIdentity != null)
                {
                    NetworkServer.Destroy(netIdentity.gameObject);
                }
            }

            // clear the hashset because we destroyed them all
            owned.Clear();
        }
    }
}
