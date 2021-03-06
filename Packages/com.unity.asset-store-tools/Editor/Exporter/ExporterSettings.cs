namespace AssetStoreTools.Exporter
{
    public abstract class ExporterSettings
    {
        public string[] ExportPaths;
        public string OutputFilename;
    }

    public class DefaultExporterSettings : ExporterSettings
    {
        public string[] Dependencies;
    }

    public class LegacyExporterSettings : ExporterSettings
    {
        public bool IncludeDependencies;
    }
}