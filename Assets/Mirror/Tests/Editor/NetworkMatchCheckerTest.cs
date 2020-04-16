using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkMatchCheckerTest
    {
        private GameObject player1;
        private GameObject player2;
        private GameObject player3;
        private NetworkMatchChecker player1MatchChecker;
        private NetworkMatchChecker player2MatchChecker;
        private NetworkConnection player1Connection;
        private NetworkConnection player2Connection;
        private NetworkConnection player3Connection;

        [SetUp]
        public void Setup()
        {
            player1 = new GameObject("TestPlayer1", typeof(NetworkIdentity), typeof(NetworkMatchChecker));
            player2 = new GameObject("TestPlayer2", typeof(NetworkIdentity), typeof(NetworkMatchChecker));
            player3 = new GameObject("TestPlayer3", typeof(NetworkIdentity));

            player1MatchChecker = player1.GetComponent<NetworkMatchChecker>();
            player2MatchChecker = player2.GetComponent<NetworkMatchChecker>();


            player1Connection = CreateNetworkConnection(player1);
            player2Connection = CreateNetworkConnection(player2);
            player3Connection = CreateNetworkConnection(player3);
        }
        static int next;
        static NetworkConnection CreateNetworkConnection(GameObject player)
        {
            NetworkConnection connection = new NetworkConnectionToClient(++next);
            connection.identity = player.GetComponent<NetworkIdentity>();
            return connection;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(player1);
            UnityEngine.Object.DestroyImmediate(player2);
            UnityEngine.Object.DestroyImmediate(player3);
        }

        static void SetMatchId(NetworkMatchChecker target, Guid guid)
        {
            // set using reflection so bypass property
            System.Reflection.FieldInfo field = typeof(NetworkMatchChecker).GetField("currentMatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(target, guid);
        }

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
    }
}
