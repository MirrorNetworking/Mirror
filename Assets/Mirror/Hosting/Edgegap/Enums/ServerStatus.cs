using IO.Swagger.Model;
using System;
using System.Linq;
using UnityEngine;

namespace Edgegap
{
    public enum ServerStatus
    {
        NA, // Not an actual Edgegap server status. Indicates that there are no active server.
        Initializing,
        Seeking,
        Deploying,
        Ready,
        Seeked,
        Terminated,
        Scanning,
        Terminating,
        Error,
    }

    public static class ServerStatusExtensions
    {
        private static string GetServerStatusLabel(this Status serverStatusResponse) => char.ToUpper(serverStatusResponse.CurrentStatus[7]) + serverStatusResponse.CurrentStatus.Substring(8).ToLower();

        public static ServerStatus GetServerStatus(this Status serverStatusResponse)
        {
            ServerStatus serverStatus;

            try
            {
                serverStatus = (ServerStatus)Enum.Parse(typeof(ServerStatus), serverStatusResponse.GetServerStatusLabel());
            }
            catch (Exception)
            {
                Debug.LogError($"Got unexpected server status: {serverStatusResponse.CurrentStatus}. Considering the deployment to be terminated.");
                serverStatus = ServerStatus.Terminated;
            }

            return serverStatus;
        }

        public static string GetStatusBgClass(this ServerStatus serverStatus)
        {
            string statusBgClass;

            switch (serverStatus)
            {
                case ServerStatus.NA:
                case ServerStatus.Terminated:
                    statusBgClass = "bg--secondary"; break;
                case ServerStatus.Ready:
                    statusBgClass = "bg--success"; break;
                case ServerStatus.Error:
                    statusBgClass = "bg--danger"; break;
                default:
                    statusBgClass = "bg--warning"; break;
            }

            return statusBgClass;
        }

        public static string GetLabelText(this ServerStatus serverStatus)
        {
            string statusLabel;

            if (serverStatus == ServerStatus.NA)
            {
                statusLabel = "N/A";
            }
            else
            {
                statusLabel = Enum.GetName(typeof(ServerStatus), serverStatus);
            }

            return statusLabel;
        }

        public static bool IsOneOf(this ServerStatus serverStatus, params ServerStatus[] serverStatusOptions)
        {
            return serverStatusOptions.Contains(serverStatus);
        }
    }
}