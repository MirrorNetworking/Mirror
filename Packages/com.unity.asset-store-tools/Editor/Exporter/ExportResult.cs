using AssetStoreTools.Utility;

namespace AssetStoreTools.Exporter
{
    internal class ExportResult
    {
        public bool Success;
        public string ExportedPath;
        public ASError Error;

        public static implicit operator bool(ExportResult value)
        {
            return value != null && value.Success;
        }
    }
}