Unity 2022 doesn't allow MonoBehaviours to live in the Editor/ folder anymore
Previously these were inlined in the respective test files, but that doesnt work now.

The asmdef can't be set to editor only either, that triggers the same check to fail, UNITY_EDITOR as a define constraint does work though thankfully.