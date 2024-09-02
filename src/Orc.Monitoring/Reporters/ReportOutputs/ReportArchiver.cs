namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.IO;
using System.Threading.Tasks;
using IO;

public class ReportArchiver
{
    private readonly IFileSystem _fileSystem;

    public ReportArchiver(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        _fileSystem = fileSystem;
    }

    public void CreateTimestampedFileCopy(string filePath)
    {
        if (!_fileSystem.FileExists(filePath))
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

        _fileSystem.CopyFile(filePath, archivedFilePath, true);
    }

    public void CreateTimestampedFolderCopy(string folderPath)
    {
        if (!_fileSystem.DirectoryExists(folderPath))
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

    public Task CreateTimestampedFolderCopyAsync(string folderPath)
    {
        return Task.Run(() => CreateTimestampedFolderCopy(folderPath));
    }

    private string GetArchiveDirectory(string directory)
    {
        var archiveDirectory = Path.Combine(directory, "Archived");
        if (!_fileSystem.DirectoryExists(archiveDirectory))
        {
            _fileSystem.CreateDirectory(archiveDirectory);
        }

        return archiveDirectory;
    }

    private void CopyFolder(string folderPath, string archivedFolderPath)
    {
        if (!_fileSystem.DirectoryExists(folderPath))
        {
            return;
        }

        if (!_fileSystem.DirectoryExists(archivedFolderPath))
        {
            _fileSystem.CreateDirectory(archivedFolderPath);
        }

        foreach (var file in _fileSystem.GetFiles(folderPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(folderPath, file);
            var archivedFilePath = Path.Combine(archivedFolderPath, relativePath);
            var archivedFileDirectory = Path.GetDirectoryName(archivedFilePath);

            if (archivedFileDirectory is not null && !_fileSystem.DirectoryExists(archivedFileDirectory))
            {
                _fileSystem.CreateDirectory(archivedFileDirectory);
            }

            _fileSystem.CopyFile(file, archivedFilePath, true);
        }
    }

}
