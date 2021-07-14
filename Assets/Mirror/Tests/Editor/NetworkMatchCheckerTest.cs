using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkMatchCheckerTest : MirrorEditModeTest
    {
        GameObject player1;
        GameObject player2;
        GameObject player3;
#pragma warning disable 618
        NetworkMatch player1Match;
        NetworkMatch player2Match;
        NetworkMatchChecker player1MatchChecker;
        NetworkMatchChecker player2MatchChecker;
#pragma warning restore 618
        NetworkConnection player1Connection;
        NetworkConnection player2Connection;
        NetworkConnection player3Connection;
        static int nextConnectionId;
        Dictionary<Guid, HashSet<NetworkIdentity>> matchPlayers;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

#pragma warning disable 618
            CreateNetworked(out player1, out NetworkIdentity _, out player1Match, out player1MatchChecker);
            player1.name = "TestPlayer1";

            CreateNetworked(out player2, out NetworkIdentity _, out player2Match, out player2MatchChecker);
            player2.name = "TestPlayer2";

            player3 = new GameObject("TestPlayer3", typeof(NetworkIdentity));

            player1Connection = CreateNetworkConnection(player1);
            player2Connection = CreateNetworkConnection(player2);
            player3Connection = CreateNetworkConnection(player3);
            matchPlayers = NetworkMatchChecker.matchPlayers;
#pragma warning restore 618
        }

        static NetworkConnection CreateNetworkConnection(GameObject player)
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(++nextConnectionId);
            connection.identity = player.GetComponent<NetworkIdentity>();
            connection.identity.connectionToClient = connection;
            connection.identity.observers = new Dictionary<int, NetworkConnection>();
            connection.isReady = true;
            return connection;
        }

        [TearDown]
        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(player3);

            matchPlayers.Clear();
            matchPlayers = null;

            base.TearDown();
        }

        [Test]
        public void OnCheckObserverShouldBeTrueForSameMatchId()
        {
            string guid = Guid.NewGuid().ToString();

            player1MatchChecker.currentMatch = new Guid(guid);
            player2MatchChecker.currentMatch = new Guid(guid);

            bool player1Visable = player1MatchChecker.OnCheckObserver(player1Connection);
            Assert.IsTrue(player1Visable);

            bool player2Visable = player1MatchChecker.OnCheckObserver(player2Connection);
            Assert.IsTrue(player2Visable);
        }

        [Test]
        public void OnCheckObserverShouldBeFalseForDifferentMatchId()
        {
            string guid1 = Guid.NewGuid().ToString();
            string guid2 = Guid.NewGuid().ToString();

            player1MatchChecker.currentMatch = new Guid(guid1);
            player2MatchChecker.currentMatch = new Guid(guid2);

            bool player1VisableToPlayer1 = player1MatchChecker.OnCheckObserver(player1Connection);
            Assert.IsTrue(player1VisableToPlayer1);

            bool player2VisableToPlayer1 = player1MatchChecker.OnCheckObserver(player2Connection);
            Assert.IsFalse(player2VisableToPlayer1);


            bool player1VisableToPlayer2 = player2MatchChecker.OnCheckObserver(player1Connection);
            Assert.IsFalse(player1VisableToPlayer2);

            bool player2VisableToPlayer2 = player2MatchChecker.OnCheckObserver(player2Connection);
            Assert.IsTrue(player2VisableToPlayer2);
        }

        [Test]
        public void OnCheckObserverShouldBeFalseIfObjectDoesNotHaveNetworkMatchChecker()
        {
            string guid = Guid.NewGuid().ToString();

            player1MatchChecker.currentMatch =  new Guid(guid);

            bool player3Visable = player1MatchChecker.OnCheckObserver(player3Connection);
            Assert.IsFalse(player3Visable);
        }

        [Test]
        public void OnCheckObserverShouldBeFalseForEmptyGuid()
        {
            string guid = Guid.Empty.ToString();

            player1MatchChecker.currentMatch = new Guid(guid);
            player2MatchChecker.currentMatch = new Guid(guid);

            bool player1Visable = player1MatchChecker.OnCheckObserver(player1Connection);
            Assert.IsFalse(player1Visable);

            bool player2Visable = player1MatchChecker.OnCheckObserver(player2Connection);
            Assert.IsFalse(player2Visable);
        }

        /*
        [Test]
        public void SettingMatchIdShouldRebuildObservers()
        {
            string guidMatch1 = Guid.NewGuid().ToString();

            // make players join same match
            player1Match.matchId = new Guid(guidMatch1);
            player2Match.matchId = new Guid(guidMatch1);

            // check player1's observers contains player 2
            Assert.IsTrue(player1MatchChecker.netIdentity.observers.ContainsValue(player2MatchChecker.connectionToClient));
            // check player2's observers contains player 1
            Assert.IsTrue(player2MatchChecker.netIdentity.observers.ContainsValue(player1MatchChecker.connectionToClient));
        }

        [Test]
        public void ChangingMatchIdShouldRebuildObservers()
        {
            string guidMatch1 = Guid.NewGuid().ToString();
            string guidMatch2 = Guid.NewGuid().ToString();

            // make players join same match
            player1Match.matchId = new Guid(guidMatch1);
            player2Match.matchId = new Guid(guidMatch1);

            // make player2 join different match
            player2Match.matchId = new Guid(guidMatch2);

            // check player1's observers does NOT contain player 2
            Assert.IsFalse(player1MatchChecker.netIdentity.observers.ContainsValue(player2MatchChecker.connectionToClient));
            // check player2's observers does NOT contain player 1
            Assert.IsFalse(player2MatchChecker.netIdentity.observers.ContainsValue(player1MatchChecker.connectionToClient));
        }

        [Test]
        public void ClearingMatchIdShouldRebuildObservers()
        {
            string guidMatch1 = Guid.NewGuid().ToString();

            // make players join same match
            player1Match.matchId = new Guid(guidMatch1);
            player2Match.matchId = new Guid(guidMatch1);

            // make player 2 leave match
            player2Match.matchId = Guid.Empty;

            // check player1's observers does NOT contain player 2
            Assert.IsFalse(player1MatchChecker.netIdentity.observers.ContainsValue(player2MatchChecker.connectionToClient));
            // check player2's observers does NOT contain player 1
            Assert.IsFalse(player2MatchChecker.netIdentity.observers.ContainsValue(player1MatchChecker.connectionToClient));
        }
        */
    }
}
