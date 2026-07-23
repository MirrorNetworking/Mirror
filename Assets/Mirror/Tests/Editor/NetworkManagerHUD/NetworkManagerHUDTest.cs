using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkManagerHUDTest : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void NetworkManagerHUDAttributesTest()
        {
            CreateGameObject(out GameObject manager, out NetworkManagerHUD managerHUD);

            NetworkManagerHUD[] allHUDs = manager.GetComponents<NetworkManagerHUD>();

            // Check if [RequireComponent(typeof(NetworkManager))] works
            Assert.That(manager.GetComponent<NetworkManager>(), Is.Not.Null);
            Assert.That(allHUDs.Length, Is.EqualTo(1));

            manager.AddComponent<NetworkManagerHUD>();
            allHUDs = manager.GetComponents<NetworkManagerHUD>();

            // Check if [DisallowMultipleComponent] works
            Assert.That(allHUDs.Length, Is.EqualTo(1));
            Assert.That(allHUDs[0], Is.SameAs(managerHUD));
        }

        [Test]
        public void NetworkManagerHUDLifecycleTest()
        {
            CreateGameObject(out GameObject managerGO, out NetworkManager networkManager);

            NetworkManagerHUD managerHUD = managerGO.AddComponent<NetworkManagerHUD>();

            Assert.That(managerHUD.manager, Is.Null, "manager should be null before Awake()");

            // Must call Unity lifecycle methods manually in edit mode tests
            managerHUD.Awake();

            Assert.That(managerHUD.manager, Is.Not.Null, "manager should be set after Awake()");
            //Assert.That(managerHUD.manager, Is.Not.Null, "manager should be set after Awake()");
        }
    }
}
