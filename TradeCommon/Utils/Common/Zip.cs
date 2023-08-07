using log4net;
using System.IO.Compression;

namespace Common;
public class Zip
{
    private static readonly ILog _log = Logger.New();

    /// <summary>
    /// Archive one file or a directory to target zip file path.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="zipFilePath"></param>
    public static void Archive(string path, string zipFilePath)
    {
        if (Directory.Exists(path))
        {
            ZipFile.CreateFromDirectory(path, zipFilePath);
        }
        else
        {
            using var a = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
            a.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
        }
        _log.Info($"Zipped file/folder {path} into {zipFilePath}.");
    }
}

