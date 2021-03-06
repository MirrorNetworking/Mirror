using AssetStoreTools.Utility;
using UnityEngine;

namespace AssetStoreTools.Uploader.Data
{
    internal class PackageUploadResult
    {
        public enum UploadStatus
        {
            Default = 0,
            Success = 1,
            Fail = 2,
            Cancelled = 3,
            ResponseTimeout = 4
        }

        public UploadStatus Status;
        public ASError Error;

        private PackageUploadResult() { }

        public static PackageUploadResult PackageUploadSuccess() => new PackageUploadResult() { Status = UploadStatus.Success };

        public static PackageUploadResult PackageUploadFail(ASError e) => new PackageUploadResult() { Status = UploadStatus.Fail, Error = e };

        public static PackageUploadResult PackageUploadCancelled() => new PackageUploadResult() { Status = UploadStatus.Cancelled };

        public static PackageUploadResult PackageUploadResponseTimeout() => new PackageUploadResult() { Status = UploadStatus.ResponseTimeout };

        public static Color GetColorByStatus(UploadStatus status)
        {
            switch (status)
            {
                default:
                case UploadStatus.Default:
                    return new Color(0.13f, 0.59f, 0.95f);
                case UploadStatus.Success:
                    return new Color(0f, 0.50f, 0.14f);
                case UploadStatus.Cancelled:
                    return new Color(0.78f, 0.59f, 0f);
                case UploadStatus.Fail:
                    return new Color(0.69f, 0.04f, 0.04f);
            }
        }
    }
}
