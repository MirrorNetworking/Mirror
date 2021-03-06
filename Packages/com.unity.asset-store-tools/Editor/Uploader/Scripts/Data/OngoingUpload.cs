using System;
using System.Threading;

namespace AssetStoreTools.Uploader.Data
{
    internal class OngoingUpload : IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource;

        public string VersionId { get; }
        public string PackageName { get; }
        public float Progress { get; private set; }
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public OngoingUpload(string versionId, string packageName)
        {
            VersionId = versionId;
            PackageName = packageName;
            Progress = 0f;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        public void UpdateProgress(float newProgress)
        {
            Progress = newProgress;
        }
    }
}