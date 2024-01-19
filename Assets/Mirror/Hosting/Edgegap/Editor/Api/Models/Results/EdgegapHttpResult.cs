using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Wraps the inner json data with outer http info.
    /// This class overload contains no json-deserialiable data result.
    /// </summary>
    public class EdgegapHttpResult
    {
        /// <summary>HTTP Status code for the request.</summary>
        public HttpStatusCode StatusCode { get; }
        
        /// <summary>This could be err, success, or null.</summary>
        public string Json { get; }
        
        /// <summary>eg: "POST"</summary>
        public HttpMethod HttpMethod;
        
        /// <summary>
        /// Typically is sent by servers together with the status code.
        /// Useful for fallback err descriptions, often based on the status code.
        /// </summary>
        public string ReasonPhrase { get; }

        /// <summary>Contains `message` with friendly info.</summary>
        public bool HasErr => Error != null;
        public EdgegapErrorResult Error { get; set; }
        
        #region Common Shortcuts
        /// <summary>OK</summary>
        public bool IsResultCode200 => StatusCode == HttpStatusCode.OK;
        
        /// <summary>NoContent</summary>
        public bool IsResultCode204 => StatusCode == HttpStatusCode.NoContent;
        
        /// <summary>Forbidden</summary>
        public bool IsResultCode403 => StatusCode == HttpStatusCode.Forbidden;
        
        /// <summary>Conflict</summary>
        public bool IsResultCode409 => StatusCode == HttpStatusCode.Conflict;

        /// <summary>BadRequest</summary>
        public bool IsResultCode400 => StatusCode == HttpStatusCode.BadRequest;
        
        /// <summary>Gone</summary>
        public bool IsResultCode410 => StatusCode == HttpStatusCode.Gone;
        #endregion // Common Shortcuts
        
        
        /// <summary>
        /// Constructor that initializes the class based on an HttpResponseMessage.
        /// </summary>
        public EdgegapHttpResult(HttpResponseMessage httpResponse)
        {
            this.ReasonPhrase = httpResponse.ReasonPhrase;
            this.StatusCode = httpResponse.StatusCode;

            try
            {
                // TODO: This can be read async with `await`, but can't do this in a Constructor.
                //       Instead, make a factory builder Task =>
                this.Json = httpResponse.Content.ReadAsStringAsync().Result;

                this.Error = JsonConvert.DeserializeObject<EdgegapErrorResult>(Json);
                if (Error != null && string.IsNullOrEmpty(Error.ErrorMessage))
                    Error = null;
            }
            catch (Exception e)
            {
                Debug.LogError("Error (reading httpResponse.Content): Client expected json, " +
                    $"but server returned !json: {e} - ");
            }
        }
    }

    /// <summary>
    /// Wraps the inner json data with outer http info.
    /// This class overload contains json-deserialiable data result.
    /// </summary>
    public class EdgegapHttpResult<TResult> : EdgegapHttpResult
    {
        /// <summary>The actual result model from Json. Could be null!</summary>
        public TResult Data { get; set; }
        
        
        public EdgegapHttpResult(HttpResponseMessage httpResponse, bool isLogLevelDebug = false) 
            : base(httpResponse)
        {
            this.HttpMethod = httpResponse.RequestMessage.Method;
            
            // Assuming JSON content and using Newtonsoft.Json for deserialization
            bool isDeserializable = httpResponse.Content != null &&
                httpResponse.Content.Headers.ContentType.MediaType == "application/json";

            if (isDeserializable)
            {
                try
                {
                    this.Data = JsonConvert.DeserializeObject<TResult>(Json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error (deserializing EdgegapHttpResult.Data): {e} - json: {Json}");
                    throw;
                }
            }

            if (isLogLevelDebug)
                UnityEngine.Debug.Log($"{typeof(TResult).Name} result: {JObject.Parse(Json)}"); // Prettified
        }
    }
}
