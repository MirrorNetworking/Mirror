using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class SceneObjectSpawningTests
    {
        const string ScenePath = "Assets/Mirror/Tests/Runtime/Scenes/SceneObjectSpawningTestsScene.unity";
        readonly Regex errorMessage = new Regex(".*Don't call Instantiate for NetworkIdentities that were in the scene since the beginning \\(aka scene objects\\).*");

        NetworkIdentity sceneObject;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // load scene
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive });
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(scene);

            // wait for networkmanager to awake
            yield return null;

            NetworkManager.singleton.StartHost();
            // wait for start host
            yield return null;

            sceneObject = GameObject.Find("SceneNetworkIdentity").GetComponent<NetworkIdentity>();
            Debug.Assert(sceneObject != null, $"Could not find 'SceneNetworkIdentity' in Scene:{ScenePath}");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NetworkManager.Shutdown();

            // unload scene
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        //[UnityTest]
        //public IEnumerator CallingInstantiateOnASceneObjectGivesAHelpfulError()
        //{
        //    // make sure sceneobject has a sceneId
        //    Assert.That(sceneObject.sceneId, Is.Not.Zero);
        //    yield return null;

        //    LogAssert.Expect(LogType.Error, errorMessage);
        //    GameObject clone = GameObject.Instantiate(sceneObject.gameObject);
        //    NetworkServer.Spawn(clone);
        //}

        //[UnityTest]
        //public IEnumerator CallingInstantiateOnASceneObjectMutlipleTimesGivesAHelpfulErrorEachTime()
        //{
        //    // make sure sceneobject has a sceneId
        //    Assert.That(sceneObject.sceneId, Is.Not.Zero);
        //    yield return null;

        //    for (int i = 0; i < 5; i++)
        //    {
        //        LogAssert.Expect(LogType.Error, errorMessage);
        //        GameObject clone = GameObject.Instantiate(sceneObject.gameObject);
        //        NetworkServer.Spawn(clone);
        //    }
        //}
    }
}
