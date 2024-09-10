#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

[TestFixture]
public class InMemoryFileSystemTests
{
    private InMemoryFileSystem _fileSystem;
    private TestLogger<InMemoryFileSystemTests> _logger;

    [SetUp]
    public void SetUp()
    {
        _logger = new TestLogger<InMemoryFileSystemTests>();
        var loggerFactory = new TestLoggerFactory<InMemoryFileSystemTests>(_logger);
        loggerFactory.EnableLoggingFor<InMemoryFileSystem>();
        _fileSystem = new InMemoryFileSystem(loggerFactory);
    }

    [TearDown]
    public void TearDown()
    {
        _fileSystem.Dispose();
    }

    [Test]
    public void FileExists_ShouldReturnFalse_WhenFileDoesNotExist()
    {
        Assert.That(_fileSystem.FileExists("/nonexistent.txt"), Is.False);
    }

    [Test]
    public void WriteAllText_ShouldCreateFile_WhenPathIsValid()
    {
        _fileSystem.WriteAllText("/test.txt", "Hello, World!");

        Assert.That(_fileSystem.FileExists("/test.txt"), Is.True);
    }

    [Test]
    public void ReadAllText_ShouldReturnContentsOfFile_WhenFileExists()
    {
        _fileSystem.WriteAllText("/test.txt", "Hello, World!");

        string contents = _fileSystem.ReadAllText("/test.txt");

        Assert.That(contents, Is.EqualTo("Hello, World!"));
    }

    [Test]
    public void AppendAllText_ShouldAppendToFile_WhenFileExists()
    {
        _fileSystem.WriteAllText("/test.txt", "Hello, ");
        _fileSystem.AppendAllText("/test.txt", "World!");

        string contents = _fileSystem.ReadAllText("/test.txt");

        Assert.That(contents, Is.EqualTo("Hello, World!"));
    }

    [Test]
    public void CreateDirectory_ShouldCreateDirectory_WhenPathIsValid()
    {
        _fileSystem.CreateDirectory("/myDir");

        Assert.That(_fileSystem.DirectoryExists("/myDir"), Is.True);
    }

    [Test]
    public void DirectoryExists_ShouldReturnFalse_WhenDirectoryDoesNotExist()
    {
        Assert.That(_fileSystem.DirectoryExists("/nonexistentDir"), Is.False);
    }

    [Test]
    public void DeleteDirectory_ShouldRemoveDirectory_WhenRecursiveIsFalseAndDirectoryIsEmpty()
    {
        _fileSystem.CreateDirectory("/emptyDir");
        _fileSystem.DeleteDirectory("/emptyDir", false);

        Assert.That(_fileSystem.DirectoryExists("/emptyDir"), Is.False);
    }

    [Test]
    public void DeleteDirectory_ShouldThrowIOException_WhenDirectoryIsNotEmptyAndRecursiveIsFalse()
    {
        _fileSystem.CreateDirectory("/myDir");
        _fileSystem.WriteAllText("/myDir/file.txt", "Test");

        Assert.That(() => _fileSystem.DeleteDirectory("/myDir", false), Throws.InstanceOf<IOException>());
    }

    [Test]
    public void DeleteDirectory_ShouldDeleteDirectoryAndContents_WhenRecursiveIsTrue()
    {
        _fileSystem.CreateDirectory("/myDir");
        _fileSystem.WriteAllText("/myDir/file.txt", "Test");

        _fileSystem.DeleteDirectory("/myDir", true);

        Assert.That(_fileSystem.DirectoryExists("/myDir"), Is.False);
        Assert.That(_fileSystem.FileExists("/myDir/file.txt"), Is.False);
    }

    [Test]
    public void GetFiles_ShouldReturnAllFilesInDirectory()
    {
        _fileSystem.CreateDirectory("/myDir");
        _fileSystem.WriteAllText("/myDir/file1.txt", "File1");
        _fileSystem.WriteAllText("/myDir/file2.txt", "File2");

        var files = _fileSystem.GetFiles("/myDir");

        Assert.That(files.Length, Is.EqualTo(2));
        Assert.That(files, Does.Contain("/myDir/file1.txt"));
        Assert.That(files, Does.Contain("/myDir/file2.txt"));
    }

    [Test]
    public void SetAttributes_ShouldSetAttributesOnFile()
    {
        _fileSystem.WriteAllText("/test.txt", "Test");
        _fileSystem.SetAttributes("/test.txt", FileAttributes.ReadOnly);

        var attributes = _fileSystem.GetAttributes("/test.txt");

        Assert.That(attributes, Is.EqualTo(FileAttributes.ReadOnly));
    }

    [Test]
    public async Task ReadAllTextAsync_ShouldReturnContentsOfFile_WhenFileExists()
    {
        await _fileSystem.WriteAllTextAsync("/asyncTest.txt", "Async Hello, World!");

        string contents = await _fileSystem.ReadAllTextAsync("/asyncTest.txt");

        Assert.That(contents, Is.EqualTo("Async Hello, World!"));
    }

    [Test]
    public void CreateFileStream_ShouldCreateFile_WhenFileDoesNotExist()
    {
        using (var stream = _fileSystem.CreateFileStream("/streamTest.txt", FileMode.Create, FileAccess.Write, FileShare.None, 1024, FileOptions.None))
        {
            byte[] bytes = "Stream Test"u8.ToArray();
            stream.Write(bytes, 0, bytes.Length);
        }

        Assert.That(_fileSystem.FileExists("/streamTest.txt"), Is.True);
        Assert.That(_fileSystem.ReadAllText("/streamTest.txt"), Is.EqualTo("Stream Test"));
    }

    [Test]
    public void CreateFileStream_ShouldAppendToFile_WhenFileExistsAndFileModeIsAppend()
    {
        _fileSystem.WriteAllText("/streamTest.txt", "Start ");

        using (var stream = _fileSystem.CreateFileStream("/streamTest.txt", FileMode.Append, FileAccess.Write, FileShare.None, 1024, FileOptions.None))
        {
            byte[] bytes = "End"u8.ToArray();
            stream.Write(bytes, 0, bytes.Length);
        }

        Assert.That(_fileSystem.ReadAllText("/streamTest.txt"), Is.EqualTo("Start End"));
    }

    [Test]
    public void GetFiles_WithSearchPattern_ShouldReturnMatchingFiles()
    {
        _fileSystem.CreateDirectory("/myDir");
        _fileSystem.WriteAllText("/myDir/file1.txt", "File1");
        _fileSystem.WriteAllText("/myDir/file2.log", "File2");
        _fileSystem.WriteAllText("/myDir/file3.txt", "File3");

        var files = _fileSystem.GetFiles("/myDir", "*.txt");

        Assert.That(files.Length, Is.EqualTo(2));
        Assert.That(files, Does.Contain("/myDir/file1.txt"));
        Assert.That(files, Does.Contain("/myDir/file3.txt"));
    }

    [Test]
    public void GetFiles_WithSearchPatternAndAllDirectories_ShouldReturnMatchingFilesInAllDirectories()
    {
        _fileSystem.CreateDirectory("/myDir");
        _fileSystem.CreateDirectory("/myDir/subDir");
        _fileSystem.WriteAllText("/myDir/file1.txt", "File1");
        _fileSystem.WriteAllText("/myDir/subDir/file2.txt", "File2");

        var files = _fileSystem.GetFiles("/myDir", "*.txt", SearchOption.AllDirectories);

        Assert.That(files.Length, Is.EqualTo(2));
        Assert.That(files, Does.Contain("/myDir/file1.txt"));
        Assert.That(files, Does.Contain("/myDir/subDir/file2.txt"));
    }

    [Test]
    public void GetFiles_WithNoMatchingPattern_ShouldReturnEmptyArray()
    {
        _fileSystem.CreateDirectory("/myDir");
        _fileSystem.WriteAllText("/myDir/file1.txt", "File1");

        var files = _fileSystem.GetFiles("/myDir", "*.log");

        Assert.That(files.Length, Is.EqualTo(0));
    }

    [Test]
    public void LargeFile_ReadWrite_ShouldWork()
    {
        var filePath = "/largefile.txt";
        var largeContent = new string('a', 10_000_000); // 10 MB string

        _fileSystem.WriteAllText(filePath, largeContent);
        var readContent = _fileSystem.ReadAllText(filePath);

        Assert.That(readContent.Length, Is.EqualTo(largeContent.Length));
        Assert.That(readContent, Is.EqualTo(largeContent));
    }

    [Test]
    public void FileLocking_ShouldPreventConcurrentWrites()
    {
        var filePath = "/locked.txt";
        _fileSystem.WriteAllText(filePath, "Initial content");

        Exception caughtException = null;
        using (var _ = _fileSystem.CreateFileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            try
            {
                _fileSystem.WriteAllText(filePath, "New content");
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
        }

        Assert.That(caughtException, Is.TypeOf<IOException>());
        Assert.That(_fileSystem.ReadAllText(filePath), Is.EqualTo("Initial content"));
    }

    [Test]
    public void PathNormalization_ShouldHandleDifferentFormats()
    {
        _fileSystem.WriteAllText("/folder/file.txt", "content");

        Assert.That(_fileSystem.FileExists("/folder/file.txt"), Is.True);
        Assert.That(_fileSystem.FileExists("\\folder\\file.txt"), Is.True);
        Assert.That(_fileSystem.FileExists("/folder/../folder/./file.txt"), Is.True);
    }

    [Test]
    public void ReadNonExistentFile_ShouldThrowFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => _fileSystem.ReadAllText("/nonexistent.txt"));
    }

    [Test]
    public void WriteToReadOnlyFile_ShouldThrowUnauthorizedAccessException()
    {
        var filePath = "/readonly.txt";
        _fileSystem.WriteAllText(filePath, "Initial content");
        _fileSystem.SetAttributes(filePath, FileAttributes.ReadOnly);

        Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.WriteAllText(filePath, "New content"));
    }

    [Test]
    public void MoveFile_ShouldWork()
    {
        var sourcePath = "/source.txt";
        var destPath = "/dest.txt";
        _fileSystem.WriteAllText(sourcePath, "content");

        _fileSystem.MoveFile(sourcePath, destPath);

        Assert.That(_fileSystem.FileExists(sourcePath), Is.False);
        Assert.That(_fileSystem.FileExists(destPath), Is.True);
        Assert.That(_fileSystem.ReadAllText(destPath), Is.EqualTo("content"));
    }

    [Test]
    public void FileStream_PartialReadWrite_ShouldWork()
    {
        var filePath = "/stream.txt";
        var content = "Hello, World!";

        using (var stream = _fileSystem.CreateFileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        using (var stream = _fileSystem.CreateFileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var buffer = new byte[5];
            var bytesRead = stream.Read(buffer, 0, 5);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(Encoding.UTF8.GetString(buffer), Is.EqualTo("Hello"));
        }
    }

    [Test]
    public void EmptyFile_ShouldBeHandledCorrectly()
    {
        var filePath = "/empty.txt";
        _fileSystem.WriteAllText(filePath, string.Empty);

        Assert.That(_fileSystem.FileExists(filePath), Is.True);
        Assert.That(_fileSystem.ReadAllText(filePath), Is.Empty);
    }

    [Test]
    public void VeryLongFileName_ShouldBeHandled()
    {
        var longFileName = new string('a', 255) + ".txt";
        var filePath = "/" + longFileName;

        Assert.DoesNotThrow(() => _fileSystem.WriteAllText(filePath, "content"));
        Assert.That(_fileSystem.FileExists(filePath), Is.True);
    }

    [Test]
    public void WriteAndReadFile_ShouldReturnSameContent()
    {
        // Arrange
        var filePath = "/testfile.txt";
        var content = "Hello, this is a test content!";

        // Act
        _fileSystem.WriteAllText(filePath, content);
        var readContent = _fileSystem.ReadAllText(filePath);

        // Assert
        Assert.That(readContent, Is.EqualTo(content), "The content read from the file should match the content written to it.");

        // Additional Checks
        Assert.That(_fileSystem.FileExists(filePath), Is.True, "The file should exist after writing.");

        // Check file contents using a stream
        using (var stream = _fileSystem.CreateFileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That(stream.Length, Is.EqualTo(content.Length), "The file length should match the content length.");

            var buffer = new byte[content.Length];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            Assert.That(bytesRead, Is.EqualTo(content.Length), "The number of bytes read should match the content length.");

            var streamContent = Encoding.UTF8.GetString(buffer);
            Assert.That(streamContent, Is.EqualTo(content), "The content read from the stream should match the original content.");
        }

        _logger.LogInformation($"Successfully wrote and read file: {filePath}");
        _logger.LogInformation($"Content length: {content.Length}");
    }

    [Test]
    public void WriteAndReadFile_WithVariousContentTypes_ShouldWork()
    {
        var testCases = new[]
        {
            ("Empty string", string.Empty),
            ("Empty string multiline", "\n\n\n"),
            ("Short string", "Hello"),
            ("Long string", new string('a', 1000000)),
            ("String with special characters", "Line 1\nLine 2\rTab\tQuote\"Backslash\\"),
            ("Unicode characters", "こんにちは世界"),
        };

        foreach (var (description, content) in testCases)
        {
            var filePath = $"/test_{description.Replace(" ", "_")}.txt";

            _fileSystem.WriteAllText(filePath, content);
            var readContent = _fileSystem.ReadAllText(filePath);

            Assert.That(readContent, Is.EqualTo(content), $"Failed for case: {description}");
            _logger.LogInformation($"Passed: {description}, Length: {content.Length}");
        }
    }
}


