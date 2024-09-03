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
        _fileSystem = new InMemoryFileSystem(_loggerFactory);
        _reportArchiver = new ReportArchiver(_fileSystem);
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
        var sourceFolder = Path.Combine(_testFolderPath, "SourceFolder");
        _fileSystem.CreateDirectory(sourceFolder);
        var testfile1Txt = "TestFile1.txt";
        var testFilePath = Path.Combine(sourceFolder, testfile1Txt);
        await _fileSystem.WriteAllTextAsync(testFilePath, "Test content");
        _fileSystem.CreateDirectory("DummyFolder");
        var testFilePath2 = Path.Combine(sourceFolder, "DummyFolder", "TestFile2.txt");
        await _fileSystem.WriteAllTextAsync(testFilePath2, "Test content");


        // Act
        await _reportArchiver.CreateTimestampedFolderCopyAsync(sourceFolder);

        // Assert
        var archivedFolder = Path.Combine(_testFolderPath, "Archived");
        Assert.That(_fileSystem.DirectoryExists(archivedFolder), Is.True, "Archived folder should exist");

        var archivedFolders = _fileSystem.GetDirectories(archivedFolder);
        Assert.That(archivedFolders.Length, Is.GreaterThan(0), "At least one archived folder should be created");

        var newestArchivedFolder = archivedFolders.OrderByDescending(f => f).First();
        var archivedFilePath = Path.Combine(newestArchivedFolder, testfile1Txt);
        Assert.That(_fileSystem.FileExists(archivedFilePath), Is.True, "Archived file should exist");

        var archivedContent = await _fileSystem.ReadAllTextAsync(archivedFilePath);
        Assert.That(archivedContent, Is.EqualTo("Test content"), "Archived file should have the correct content");

        _logger.LogInformation($"Source folder: {sourceFolder}");
        _logger.LogInformation($"Archived folder: {archivedFolder}");
        _logger.LogInformation($"Newest archived folder: {newestArchivedFolder}");
        _logger.LogInformation($"Files in newest archived folder: {string.Join(", ", _fileSystem.GetFiles(newestArchivedFolder))}");
    }
}
