namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.IO;
using System.Threading.Tasks;
using IO;
using Microsoft.Extensions.Logging;

public class ReportArchiver
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ReportArchiver> _logger;

    public ReportArchiver(IFileSystem fileSystem, IMonitoringLoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        _fileSystem = fileSystem;
        _logger = loggerFactory.CreateLogger<ReportArchiver>();
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

    public async Task CreateTimestampedFolderCopyAsync(string folderPath)
    {
        _logger.LogInformation($"Starting CreateTimestampedFolderCopyAsync for folder: {folderPath}");

        if (!_fileSystem.DirectoryExists(folderPath))
        {
            _logger.LogWarning($"Source folder does not exist: {folderPath}");
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var destinationFolderName = $"{Path.GetFileName(folderPath)}_{timestamp}";
        var archiveFolder = Path.Combine(Path.GetDirectoryName(folderPath) ?? string.Empty, "Archived");
        var destinationFolder = Path.Combine(archiveFolder, destinationFolderName);

        _logger.LogInformation($"Creating archived folder: {destinationFolder}");
        _fileSystem.CreateDirectory(destinationFolder);

        var files = _fileSystem.GetFiles(folderPath, "*", SearchOption.AllDirectories);
        _logger.LogInformation($"Found {files.Length} files to copy");

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(folderPath, file);
            var destinationFilePath = Path.Combine(destinationFolder, relativePath);
            var destinationFileDirectory = Path.GetDirectoryName(destinationFilePath);

            if (!string.IsNullOrEmpty(destinationFileDirectory))
            {
                _fileSystem.CreateDirectory(destinationFileDirectory);
            }

            _logger.LogInformation($"Copying file: {file} to {destinationFilePath}");
            _fileSystem.CopyFile(file, destinationFilePath, true);

            if (_fileSystem.FileExists(destinationFilePath))
            {
                _logger.LogInformation($"File copied successfully: {destinationFilePath}");
            }
            else
            {
                _logger.LogWarning($"Failed to copy file: {destinationFilePath}");
            }
        }

        _logger.LogInformation($"Folder copy completed: {destinationFolder}");
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
