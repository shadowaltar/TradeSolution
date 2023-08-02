using log4net;
using System.IO.Compression;

namespace Common;
public class Zip
{
    private static readonly ILog _log = Logger.New();

    public static void Archive(string filePath, string zipFilePath)
    {
        using var a = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
        a.CreateEntryFromFile(filePath, Path.GetFileName(filePath), CompressionLevel.Optimal);
        _log.Info($"Zipped file {filePath} into {zipFilePath}.");
    }
}

