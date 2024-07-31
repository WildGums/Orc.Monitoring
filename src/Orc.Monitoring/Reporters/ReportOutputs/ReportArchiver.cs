namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.IO;


public static class ReportArchiver
{
    public static void CreateTimestampedFileCopy(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (directory is null)
        {
            return;
        }

        var archiveDirectory = GetArchiveDirectory(directory);

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var archivedFileName = $"{fileName}_{timestamp}{extension}";
        var archivedFilePath = Path.Combine(archiveDirectory, archivedFileName);

        File.Copy(filePath, archivedFilePath, true);
    }

    public static void CreateTimestampedFolderCopy(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(folderPath);
        if (directory is null)
        {
            return;
        }

        var archiveDirectory = GetArchiveDirectory(directory);


        var folderName = Path.GetFileName(folderPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var archivedFolderName = $"{folderName}_{timestamp}";
        var archivedFolderPath = Path.Combine(archiveDirectory, archivedFolderName);

        CopyFolder(folderPath, archivedFolderPath);
    }

    private static string GetArchiveDirectory(string directory)
    {
        var archiveDirectory = Path.Combine(directory, "Archived");
        if (!Directory.Exists(archiveDirectory))
        {
            Directory.CreateDirectory(archiveDirectory);
        }

        return archiveDirectory;
    }

    private static void CopyFolder(string folderPath, string archivedFolderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        if (!Directory.Exists(archivedFolderPath))
        {
            Directory.CreateDirectory(archivedFolderPath);
        }

        foreach (var file in Directory.GetFiles(folderPath))
        {
            var fileName = Path.GetFileName(file);
            var archivedFilePath = Path.Combine(archivedFolderPath, fileName);
            File.Copy(file, archivedFilePath, true);
        }

        foreach (var subFolder in Directory.GetDirectories(folderPath))
        {
            var folderName = Path.GetFileName(subFolder);
            var archivedSubFolderPath = Path.Combine(archivedFolderPath, folderName);
            CopyFolder(subFolder, archivedSubFolderPath);
        }
    }
}
