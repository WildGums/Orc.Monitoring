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

        var directory = _fileSystem.GetDirectoryName(filePath);
        if (directory is null)
        {
            return;
        }

        var archiveDirectory = GetArchiveDirectory(directory);

        var fileName = _fileSystem.GetFileNameWithoutExtension(filePath);
        var extension = _fileSystem.GetExtension(filePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var archivedFileName = $"{fileName}_{timestamp}{extension}";
        var archivedFilePath = _fileSystem.Combine(archiveDirectory, archivedFileName);

        _fileSystem.CopyFile(filePath, archivedFilePath, true);
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
        var destinationFolderName = $"{_fileSystem.GetFileName(folderPath)}_{timestamp}";
        var archiveFolder = _fileSystem.Combine(_fileSystem.GetDirectoryName(folderPath) ?? string.Empty, "Archived");
        var destinationFolder = _fileSystem.Combine(archiveFolder, destinationFolderName);

        _logger.LogInformation($"Creating archived folder: {destinationFolder}");
        _fileSystem.CreateDirectory(destinationFolder);

        var files = _fileSystem.GetFiles(folderPath, "*", SearchOption.AllDirectories);
        _logger.LogInformation($"Found {files.Length} files to copy");

        foreach (var file in files)
        {
            var relativePath = _fileSystem.GetRelativePath(folderPath, file);
            var destinationFilePath = _fileSystem.Combine(destinationFolder, relativePath);
            var destinationFileDirectory = _fileSystem.GetDirectoryName(destinationFilePath);

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
        var archiveDirectory = _fileSystem.Combine(directory, "Archived");
        if (!_fileSystem.DirectoryExists(archiveDirectory))
        {
            _fileSystem.CreateDirectory(archiveDirectory);
        }

        return archiveDirectory;
    }
}
