using System.Collections;
using UnityEngine.Networking;

namespace Mirror.Cloud
{
    public delegate void RequestSuccess(string responseBody);

    public delegate void RequestFail(string responseBody);

    /// <summary>
    /// Objects that can be sent to the Api must have this interface
    /// </summary>
    public interface ICanBeJson {}

    /// <summary>
    /// Methods to create and send UnityWebRequest
    /// </summary>
    public interface IRequestCreator
    {
        UnityWebRequest Delete(string page);
        UnityWebRequest Get(string page);
        UnityWebRequest Patch<T>(string page, T json) where T : struct, ICanBeJson;
        UnityWebRequest Post<T>(string page, T json) where T : struct, ICanBeJson;

        /// <summary>
        /// Sends Request to api and invokes callback when finished
        /// <para>Starts Coroutine of SendRequestEnumerator</para>
        /// </summary>
        /// <param name="request"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onFail"></param>
        void SendRequest(UnityWebRequest request, RequestSuccess onSuccess = null, RequestFail onFail = null);
        /// <summary>
        /// Sends Request to api and invokes callback when finished
        /// </summary>
        /// <param name="request"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onFail"></param>
        /// <returns></returns>
        IEnumerator SendRequestEnumerator(UnityWebRequest request, RequestSuccess onSuccess = null, RequestFail onFail = null);
    }
}
