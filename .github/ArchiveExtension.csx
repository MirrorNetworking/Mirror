#r "nuget: SharpZipLib, 1.4.2"

using System.IO;
using ICSharpCode.SharpZipLib.Tar;

public static class ArchiveExtension
{
    /// <summary>
    /// Tar a folder recursively
    /// </summary>
    /// <param name="archive">Archive.</param>
    /// <param name="directory">Directory.</param>
    public static void AddFilesRecursive(this TarArchive archive, string directory)
    {
        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

        foreach (string filename in files)
        {
            var entry = TarEntry.CreateEntryFromFile(filename);
            if (archive.RootPath != null && Path.IsPathRooted(filename))
            {
                entry.Name = Path.GetRelativePath(archive.RootPath, filename);
            }
            entry.Name = entry.Name.Replace('\\', '/');
            archive.WriteEntry(entry, true);
        }
    }
}
