using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class NetworkManagerTests
    {
        Scene activeScene;

        IEnumerator RunIsActiveSceneTest(string sceneToCheck, bool expected)
        {
            // wait for first frame to make sure scene is loaded
            yield return null;
            activeScene = SceneManager.GetActiveScene();

            bool isActive = Utils.IsSceneActive(sceneToCheck);
            Assert.That(isActive, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator IsActiveSceneWorksWithSceneName()
        {
            yield return RunIsActiveSceneTest(activeScene.name, true);
            yield return RunIsActiveSceneTest("NotActiveScene", false);
        }
        [UnityTest]
        public IEnumerator IsActiveSceneWorksWithScenePath()
        {
            yield return RunIsActiveSceneTest(activeScene.path, true);
            yield return RunIsActiveSceneTest("Assets/Mirror/Tests/Runtime/Scenes/NotActiveScene.unity", false);
        }
        [UnityTest]
        public IEnumerator IsActiveSceneIsFalseForScenesWithSameNameButDifferentPath()
        {
            yield return RunIsActiveSceneTest($"Another/Path/To/{activeScene.path}", false);
        }
        [UnityTest]
        public IEnumerator IsActiveSceneIsFalseForEmptyString()
        {
            yield return RunIsActiveSceneTest("", false);
        }
    }
}
