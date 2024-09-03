#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Orc.Monitoring.IO;
using Orc.Monitoring.Reporters.ReportOutputs;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


[TestFixture]
public class ReportArchiverTests
{
    private InMemoryFileSystem _fileSystem;
    private ReportArchiver _reportArchiver;
    private string _testFolderPath;
    private TestLogger<ReportArchiverTests> _logger;
    private TestLoggerFactory<ReportArchiverTests> _loggerFactory;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger<ReportArchiverTests>();
        _loggerFactory = new TestLoggerFactory<ReportArchiverTests>(_logger);
        _loggerFactory.EnableLoggingFor<ReportArchiver>();
        _loggerFactory.EnableLoggingFor<InMemoryFileSystem>();
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _reportArchiver = new ReportArchiver(_fileSystem, _loggerFactory);
        _testFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _fileSystem.CreateDirectory(_testFolderPath);
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
    }

    [Test]
    public async Task CreateTimestampedFolderCopy_WhenDestinationExists_HandlesGracefully()
    {
        // Arrange
        var sourceFolder = NormalizePath(Path.Combine(_testFolderPath, "SourceFolder"));
        _fileSystem.CreateDirectory(sourceFolder);
        var testFilePath = NormalizePath(Path.Combine(sourceFolder, "TestFile.txt"));
        await _fileSystem.WriteAllTextAsync(testFilePath, "Test content");

        _logger.LogInformation($"Source folder created: {sourceFolder}");
        _logger.LogInformation($"Test file created: {testFilePath}");

        // Act
        await _reportArchiver.CreateTimestampedFolderCopyAsync(sourceFolder);

        // Assert
        var archivedFolder = NormalizePath(Path.Combine(_testFolderPath, "Archived"));
        Assert.That(_fileSystem.DirectoryExists(archivedFolder), Is.True, "Archived folder should exist");

        var archivedFolders = _fileSystem.GetDirectories(archivedFolder);
        Assert.That(archivedFolders.Length, Is.GreaterThan(0), "At least one archived folder should be created");

        _logger.LogInformation($"Archived folders: {string.Join(", ", archivedFolders)}");

        var newestArchivedFolder = archivedFolders.OrderByDescending(f => f).First();
        var archivedFilePath = NormalizePath(Path.Combine(newestArchivedFolder, "TestFile.txt"));

        _logger.LogInformation($"Archived folder: {archivedFolder}");
        _logger.LogInformation($"Newest archived folder: {newestArchivedFolder}");
        _logger.LogInformation($"Archived file path: {archivedFilePath}");
        _logger.LogInformation($"File exists: {_fileSystem.FileExists(archivedFilePath)}");

        Assert.That(_fileSystem.FileExists(archivedFilePath), Is.True, "Archived file should exist");

        if (_fileSystem.FileExists(archivedFilePath))
        {
            var archivedContent = await _fileSystem.ReadAllTextAsync(archivedFilePath);
            Assert.That(archivedContent, Is.EqualTo("Test content"), "Archived file should have the correct content");
        }
        else
        {
            _logger.LogError($"Archived file does not exist: {archivedFilePath}");
            var allFiles = _fileSystem.GetFiles(_testFolderPath, "*", SearchOption.AllDirectories);
            _logger.LogInformation($"All files in test folder: {string.Join(", ", allFiles)}");
        }
    }

    private string NormalizePath(string path)
    {
        return "/" + path.TrimStart('/').Replace('\\', '/');
    }
}
