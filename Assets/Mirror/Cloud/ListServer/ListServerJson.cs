using System;
using System.Collections.Generic;
using System.Linq;

namespace Mirror.Cloud.ListServerService
{
    [Serializable]
    public struct ServerCollectionJson : ICanBeJson
    {
        public ServerJson[] servers;
    }

    [Serializable]
    public struct ServerJson : ICanBeJson
    {
        public string protocol;
        public int port;
        public int playerCount;
        public int maxPlayerCount;

        /// <summary>
        /// optional
        /// </summary>
        public string displayName;

        /// <summary>
        /// Uri string of the ip and port of the server.
        /// <para>The ip is calculated by the request to the API</para>
        /// <para>This is returns from the api, any incoming address fields will be ignored</para>
        /// </summary>
        public string address;

        /// <summary>
        /// Can be used to set custom uri
        /// <para>optional</para>
        /// </summary>
        public string customAddress;

        /// <summary>
        /// Array of custom data, use SetCustomData to set values
        /// <para>optional</para>
        /// </summary>
        public KeyValue[] customData;

        /// <summary>
        /// Uri from address field
        /// </summary>
        /// <returns></returns>
        public Uri GetServerUri() => new Uri(address);

        /// <summary>
        /// Uri from customAddress field
        /// </summary>
        /// <returns></returns>
        public Uri GetCustomUri() => new Uri(customAddress);

        /// <summary>
        /// Updates the customData array
        /// </summary>
        /// <param name="data"></param>
        public void SetCustomData(Dictionary<string, string> data)
        {
            if (data == null)
            {
                customData = null;
            }
            else
            {
                customData = data.ToKeyValueArray();
                CustomDataHelper.ValidateCustomData(customData);
            }
        }

        public bool Validate()
        {
            CustomDataHelper.ValidateCustomData(customData);

            if (string.IsNullOrEmpty(protocol))
            {
                Logger.LogError("ServerJson should not have empty protocol");
                return false;
            }

            if (port == 0)
            {
                Logger.LogError("ServerJson should not have port equal 0");
                return false;
            }

            if (maxPlayerCount == 0)
            {
                Logger.LogError("ServerJson should not have maxPlayerCount equal 0");
                return false;
            }

            return true;
        }
    }

    [Serializable]
    public struct PartialServerJson : ICanBeJson
    {
        /// <summary>
        /// optional
        /// </summary>
        public int playerCount;

        /// <summary>
        /// optional
        /// </summary>
        public int maxPlayerCount;

        /// <summary>
        /// optional
        /// </summary>
        public string displayName;

        /// <summary>
        /// Array of custom data, use SetCustomData to set values
        /// <para>optional</para>
        /// </summary>
        public KeyValue[] customData;


        public void SetCustomData(Dictionary<string, string> data)
        {
            if (data == null)
            {
                customData = null;
            }
            else
            {
                customData = data.ToKeyValueArray();
                CustomDataHelper.ValidateCustomData(customData);
            }
        }

        public void Validate()
        {
            CustomDataHelper.ValidateCustomData(customData);
        }
    }

    public static class CustomDataHelper
    {
        const int MaxCustomData = 16;

        public static Dictionary<string, string> ToDictionary(this KeyValue[] keyValues)
        {
            return keyValues.ToDictionary(x => x.key, x => x.value);
        }
        public static KeyValue[] ToKeyValueArray(this Dictionary<string, string> dictionary)
        {
            return dictionary.Select(kvp => new KeyValue(kvp.Key, kvp.Value)).ToArray();
        }

        public static void ValidateCustomData(KeyValue[] customData)
        {
            if (customData == null)
            {
                return;
            }

            if (customData.Length > MaxCustomData)
            {
                Logger.LogError($"There can only be {MaxCustomData} custom data but there was {customData.Length} values given");
                Array.Resize(ref customData, MaxCustomData);
            }

            foreach (KeyValue item in customData)
            {
                item.Validate();
            }
        }
    }

    [Serializable]
    public struct KeyValue
    {
        const int MaxKeySize = 32;
        const int MaxValueSize = 256;

        public string key;
        public string value;

        public KeyValue(string key, string value)
        {
            this.key = key;
            this.value = value;
        }

        public void Validate()
        {
            if (key.Length > MaxKeySize)
            {
                Logger.LogError($"Custom Data must have key with length less than {MaxKeySize}");
                key = key.Substring(0, MaxKeySize);
            }

            if (value.Length > MaxValueSize)
            {
                Logger.LogError($"Custom Data must have value with length less than {MaxValueSize}");
                value = value.Substring(0, MaxValueSize);
            }
        }
    }
}
