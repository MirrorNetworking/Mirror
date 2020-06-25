using System.Collections;
using UnityEngine;

namespace Mirror.Cloud
{
    public interface ICoroutineRunner : IUnityEqualCheck
    {
        Coroutine StartCoroutine(IEnumerator routine);
        void StopCoroutine(IEnumerator routine);
        void StopCoroutine(Coroutine routine);
    }
}
