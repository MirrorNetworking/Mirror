using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkMatchCheckerTest
    {
        GameObject player1;
        GameObject player2;
        GameObject player3;
#pragma warning disable 618
        NetworkMatchChecker player1MatchChecker;
        NetworkMatchChecker player2MatchChecker;
#pragma warning restore 618
        NetworkConnection player1Connection;
        NetworkConnection player2Connection;
        NetworkConnection player3Connection;
        GameObject transportGO;
        static int nextConnectionId;
        Dictionary<Guid, HashSet<NetworkIdentity>> matchPlayers;

        [SetUp]
        public void Setup()
        {
            transportGO = new GameObject("transportGO");
            Transport.activeTransport = transportGO.AddComponent<MemoryTransport>();

#pragma warning disable 618
            player1 = new GameObject("TestPlayer1", typeof(NetworkIdentity), typeof(NetworkMatchChecker));
            player2 = new GameObject("TestPlayer2", typeof(NetworkIdentity), typeof(NetworkMatchChecker));
#pragma warning restore 618
            player3 = new GameObject("TestPlayer3", typeof(NetworkIdentity));

#pragma warning disable 618
            player1MatchChecker = player1.GetComponent<NetworkMatchChecker>();
            player2MatchChecker = player2.GetComponent<NetworkMatchChecker>();
#pragma warning restore 618


            player1Connection = CreateNetworkConnection(player1);
            player2Connection = CreateNetworkConnection(player2);
            player3Connection = CreateNetworkConnection(player3);
            Dictionary<Guid, HashSet<NetworkIdentity>> g = GetMatchPlayersDictionary();
            matchPlayers = g;
        }

        static Dictionary<Guid, HashSet<NetworkIdentity>> GetMatchPlayersDictionary()
        {
#pragma warning disable 618
            Type type = typeof(NetworkMatchChecker);
#pragma warning restore 618
            FieldInfo fieldInfo = type.GetField("matchPlayers", BindingFlags.Static | BindingFlags.NonPublic);
            return (Dictionary<Guid, HashSet<NetworkIdentity>>)fieldInfo.GetValue(null);
        }

        static NetworkConnection CreateNetworkConnection(GameObject player)
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(++nextConnectionId, false, 0);
            connection.identity = player.GetComponent<NetworkIdentity>();
            connection.identity.connectionToClient = connection;
            connection.identity.observers = new Dictionary<int, NetworkConnection>();
            connection.isReady = true;
            return connection;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(player1);
            UnityEngine.Object.DestroyImmediate(player2);
            UnityEngine.Object.DestroyImmediate(player3);
            UnityEngine.Object.DestroyImmediate(transportGO);

            matchPlayers.Clear();
            matchPlayers = null;
        }

#pragma warning disable 618
        static void SetMatchId(NetworkMatchChecker target, Guid guid)
        {
            // set using reflection so bypass property
            FieldInfo field = typeof(NetworkMatchChecker).GetField("currentMatch", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, guid);
        }
#pragma warning restore 618

        [Test]
        public void OnCheckObserverShouldBeTrueForSameMatchId()
        {
            string guid = Guid.NewGuid().ToString();

            SetMatchId(player1MatchChecker, new Guid(guid));
            SetMatchId(player2MatchChecker, new Guid(guid));

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

            SetMatchId(player1MatchChecker, new Guid(guid1));
            SetMatchId(player2MatchChecker, new Guid(guid2));

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

            SetMatchId(player1MatchChecker, new Guid(guid));

            bool player3Visable = player1MatchChecker.OnCheckObserver(player3Connection);
            Assert.IsFalse(player3Visable);
        }

        [Test]
        public void OnCheckObserverShouldBeFalseForEmptyGuid()
        {
            string guid = Guid.Empty.ToString();

            SetMatchId(player1MatchChecker, new Guid(guid));
            SetMatchId(player2MatchChecker, new Guid(guid));

            bool player1Visable = player1MatchChecker.OnCheckObserver(player1Connection);
            Assert.IsFalse(player1Visable);

            bool player2Visable = player1MatchChecker.OnCheckObserver(player2Connection);
            Assert.IsFalse(player2Visable);
        }

        [Test]
        public void SettingMatchIdShouldRebuildObservers()
        {
            string guidMatch1 = Guid.NewGuid().ToString();

            // make players join same match
            player1MatchChecker.matchId = new Guid(guidMatch1);
            player2MatchChecker.matchId = new Guid(guidMatch1);

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
            player1MatchChecker.matchId = new Guid(guidMatch1);
            player2MatchChecker.matchId = new Guid(guidMatch1);

            // make player2 join different match
            player2MatchChecker.matchId = new Guid(guidMatch2);

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
            player1MatchChecker.matchId = new Guid(guidMatch1);
            player2MatchChecker.matchId = new Guid(guidMatch1);

            // make player 2 leave match
            player2MatchChecker.matchId = Guid.Empty;

            // check player1's observers does NOT contain player 2
            Assert.IsFalse(player1MatchChecker.netIdentity.observers.ContainsValue(player2MatchChecker.connectionToClient));
            // check player2's observers does NOT contain player 1
            Assert.IsFalse(player2MatchChecker.netIdentity.observers.ContainsValue(player1MatchChecker.connectionToClient));
        }
    }
}
