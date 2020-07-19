using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Mirror.Cloud
{
    /// <summary>
    /// Methods to create and send UnityWebRequest
    /// </summary>
    public class RequestCreator : IRequestCreator
    {
        const string GET = "GET";
        const string POST = "POST";
        const string PATCH = "PATCH";
        const string DELETE = "DELETE";

        public readonly string baseAddress;
        public readonly string apiKey;
        readonly ICoroutineRunner runner;

        public RequestCreator(string baseAddress, string apiKey, ICoroutineRunner coroutineRunner)
        {
            if (string.IsNullOrEmpty(baseAddress))
            {
                throw new ArgumentNullException(nameof(baseAddress));
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            this.baseAddress = baseAddress;
            this.apiKey = apiKey;

            runner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
        }


        Uri CreateUri(string page)
        {
            return new Uri(string.Format("{0}/{1}?key={2}", baseAddress, page, apiKey));
        }

        UnityWebRequest CreateWebRequest(string page, string method, string json = null)
        {
            bool hasJson = !string.IsNullOrEmpty(json);
            Logger.LogRequest(page, method, hasJson, json);

            var request = new UnityWebRequest(CreateUri(page));
            request.method = method;
            if (hasJson)
            {
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            byte[] bodyRaw = hasJson
                ? Encoding.UTF8.GetBytes(json)
                : null;

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            return request;
        }



        /// <summary>
        /// Create Get Request to page
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public UnityWebRequest Get(string page)
        {
            return CreateWebRequest(page, GET);
        }

        /// <summary>
        /// Creates Post Request to page with Json body
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="page"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        public UnityWebRequest Post<T>(string page, T json) where T : struct, ICanBeJson
        {
            string jsonString = JsonUtility.ToJson(json);
            return CreateWebRequest(page, POST, jsonString);
        }

        /// <summary>
        /// Creates Patch Request to page with Json body
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="page"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        public UnityWebRequest Patch<T>(string page, T json) where T : struct, ICanBeJson
        {
            string jsonString = JsonUtility.ToJson(json);
            return CreateWebRequest(page, PATCH, jsonString);
        }

        /// <summary>
        /// Create Delete Request to page
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public UnityWebRequest Delete(string page)
        {
            return CreateWebRequest(page, DELETE);
        }


        public void SendRequest(UnityWebRequest request, RequestSuccess onSuccess = null, RequestFail onFail = null)
        {
            runner.StartCoroutine(SendRequestEnumerator(request, onSuccess, onFail));
        }

        public IEnumerator SendRequestEnumerator(UnityWebRequest request, RequestSuccess onSuccess = null, RequestFail onFail = null)
        {
            using (UnityWebRequest webRequest = request)
            {
                yield return webRequest.SendWebRequest();
                Logger.LogResponse(webRequest);

                string text = webRequest.downloadHandler.text;
                Logger.Verbose(text);
                if (webRequest.IsOk())
                {
                    onSuccess?.Invoke(text);
                }
                else
                {
                    onFail?.Invoke(text);
                }
            }
        }
    }
}
