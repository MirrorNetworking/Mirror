namespace AssetStoreTools.Uploader.Data
{
    internal class PackageData
    {
        public string Id { get; }
        public string Name { get; }
        public string VersionId { get; }
        public string Status { get; }
        public string Category { get; }
        public bool IsCompleteProject { get; }
        public string LastUploadedPath { get; }
        public string LastUploadedGuid { get; }

        public string LastDate { get; }
        public string LastSize { get; }

        public PackageData(string id, string name, string versionId, string status, string category, bool isCompleteProject, string lastUploadedPath, string lastUploadedGuid, string lastDate, string lastSize)
        {
            Id = id;
            Name = name;
            VersionId = versionId;
            Status = status;
            Category = category;
            IsCompleteProject = isCompleteProject;
            LastUploadedPath = lastUploadedPath;
            LastUploadedGuid = lastUploadedGuid;
            LastDate = lastDate;
            LastSize = lastSize;
        }

        public override string ToString()
        {
            return $"{Id} {Name} {VersionId} {Status} {Category} {LastUploadedPath} {LastUploadedGuid} {IsCompleteProject} {LastDate} {LastSize}";
        }
    }
}