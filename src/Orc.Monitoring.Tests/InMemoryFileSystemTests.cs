#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TestUtilities.Logging;
using TestUtilities.Mocks;

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
        // Act
        bool exists = _fileSystem.FileExists("/nonexistent.txt");

        // Assert
        Assert.That(exists, Is.False, "FileExists should return false for a nonexistent file.");
    }

    [Test]
    public void WriteAllText_ShouldCreateFile_WhenPathIsValid()
    {
        // Act
        _fileSystem.WriteAllText("/test.txt", "Hello, World!");

        // Assert
        Assert.That(_fileSystem.FileExists("/test.txt"), Is.True, "File should exist after WriteAllText.");
    }

    [Test]
    public void ReadAllText_ShouldReturnContentsOfFile_WhenFileExists()
    {
        // Arrange
        string path = "/test.txt";
        string expectedContent = "Hello, World!";
        _fileSystem.WriteAllText(path, expectedContent);

        // Act
        string actualContent = _fileSystem.ReadAllText(path);

        // Assert
        Assert.That(actualContent, Is.EqualTo(expectedContent), "ReadAllText should return the content that was written.");
    }

    [Test]
    public void AppendAllText_ShouldAppendToFile_WhenFileExists()
    {
        // Arrange
        string path = "/test.txt";
        _fileSystem.WriteAllText(path, "Hello, ");

        // Act
        _fileSystem.AppendAllText(path, "World!");

        // Assert
        string contents = _fileSystem.ReadAllText(path);
        Assert.That(contents, Is.EqualTo("Hello, World!"));
    }

    [Test]
    public void CreateDirectory_ShouldCreateDirectory_WhenPathIsValid()
    {
        // Act
        _fileSystem.CreateDirectory("/myDir");

        // Assert
        Assert.That(_fileSystem.FileExists("/myDir"), Is.False, "Directory should not be treated as a file.");
    }

    [Test]
    public void DirectoryExists_ShouldReturnFalse_WhenDirectoryDoesNotExist()
    {
        // Act
        bool exists = _fileSystem.DirectoryExists("/nonexistentDir");

        // Assert
        Assert.That(exists, Is.False, "DirectoryExists should return false for a nonexistent directory.");
    }

    [Test]
    public void DeleteDirectory_ShouldRemoveDirectory_WhenRecursiveIsFalseAndDirectoryIsEmpty()
    {
        // Arrange
        string path = "/emptyDir";
        _fileSystem.CreateDirectory(path);

        // Act
        _fileSystem.DeleteDirectory(path, recursive: false);

        // Assert
        Assert.That(_fileSystem.FileExists(path), Is.False, "Directory should be deleted when recursive is false and directory is empty.");
    }

    [Test]
    public void DeleteDirectory_ShouldThrowIOException_WhenDirectoryIsNotEmptyAndRecursiveIsFalse()
    {
        // Arrange
        string path = "/myDir";
        _fileSystem.CreateDirectory(path);
        _fileSystem.WriteAllText($"{path}/file.txt", "Content");

        // Act & Assert
        Assert.Throws<IOException>(() => _fileSystem.DeleteDirectory(path, recursive: false), "Deleting a non-empty directory without recursive should throw IOException.");
    }

    [Test]
    public void DeleteDirectory_ShouldDeleteDirectoryAndContents_WhenRecursiveIsTrue()
    {
        // Arrange
        string path = "/myDir";
        _fileSystem.CreateDirectory(path);
        _fileSystem.WriteAllText($"{path}/file.txt", "Content");

        // Act
        _fileSystem.DeleteDirectory(path, recursive: true);

        // Assert
        Assert.That(_fileSystem.DirectoryExists(path), Is.False, "Directory should be deleted recursively.");
        Assert.That(_fileSystem.FileExists($"{path}/file.txt"), Is.False, "Files within the directory should also be deleted.");
    }

    [Test]
    public void GetFiles_ShouldReturnAllFilesInDirectory()
    {
        // Arrange
        string dir = "/myDir";
        _fileSystem.CreateDirectory(dir);
        _fileSystem.WriteAllText($"{dir}/file1.txt", "Content1");
        _fileSystem.WriteAllText($"{dir}/file2.txt", "Content2");

        // Act
        string[] files = _fileSystem.GetFiles(dir);

        // Assert
        Assert.That(files.Length, Is.EqualTo(2), "GetFiles should return all files in the directory.");
        Assert.That(files, Contains.Item($"{dir}/file1.txt"), "File1 should be in the list of files.");
        Assert.That(files, Contains.Item($"{dir}/file2.txt"), "File2 should be in the list of files.");
    }

    [Test]
    public void SetAttributes_ShouldSetAttributesOnFile()
    {
        // Arrange
        string path = "/test.txt";
        _fileSystem.WriteAllText(path, "Content");

        // Act
        _fileSystem.SetAttributes(path, FileAttributes.ReadOnly);

        // Assert
        FileAttributes attributes = _fileSystem.GetAttributes(path);
        Assert.That(attributes, Is.EqualTo(FileAttributes.ReadOnly), "SetAttributes should set the specified attributes on the file.");
    }

    [Test]
    public async Task ReadAllTextAsync_ShouldReturnContentsOfFile_WhenFileExists()
    {
        // Arrange
        string path = "/asyncTest.txt";
        string content = "Async Content";
        await _fileSystem.WriteAllTextAsync(path, content);

        // Act
        string actualContent = await _fileSystem.ReadAllTextAsync(path);

        // Assert
        Assert.That(actualContent, Is.EqualTo(content), "ReadAllTextAsync should return the content that was written asynchronously.");
    }

    [Test]
    public void CreateFileStream_ShouldCreateFile_WhenFileDoesNotExist()
    {
        // Arrange
        string path = "/streamTest.txt";
        string content = "Stream Content";

        // Act
        using (var stream = _fileSystem.CreateFileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        // Assert
        Assert.That(_fileSystem.FileExists(path), Is.True, "File should exist after writing with a stream.");
        string actualContent = _fileSystem.ReadAllText(path);
        Assert.That(actualContent, Is.EqualTo(content), "Content written via stream should be correctly saved.");
    }

    [Test]
    public void CreateFileStream_ShouldAppendToFile_WhenFileExistsAndFileModeIsAppend()
    {
        // Arrange
        string path = "/streamTest.txt";
        _fileSystem.WriteAllText(path, "Start ");

        // Act
        using (var stream = _fileSystem.CreateFileStream(path, FileMode.Append, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = Encoding.UTF8.GetBytes("End");
            stream.Write(bytes, 0, bytes.Length);
        }

        // Assert
        string content = _fileSystem.ReadAllText(path);
        Assert.That(content, Is.EqualTo("Start End"));
    }

    [Test]
    public void GetFiles_WithSearchPattern_ShouldReturnMatchingFiles()
    {
        // Arrange
        string dir = "/myDir";
        _fileSystem.CreateDirectory(dir);
        _fileSystem.WriteAllText($"{dir}/file1.txt", "Content1");
        _fileSystem.WriteAllText($"{dir}/file2.log", "Content2");
        _fileSystem.WriteAllText($"{dir}/file3.txt", "Content3");

        // Act
        string[] files = _fileSystem.GetFiles(dir, "*.txt");

        // Assert
        Assert.That(files.Length, Is.EqualTo(2), "GetFiles should return files matching the search pattern.");
        Assert.That(files, Contains.Item($"{dir}/file1.txt"));
        Assert.That(files, Contains.Item($"{dir}/file3.txt"));
    }

    [Test]
    public void GetFiles_WithSearchPatternAndAllDirectories_ShouldReturnMatchingFilesInAllDirectories()
    {
        // Arrange
        string dir = "/myDir";
        string subDir = $"{dir}/subDir";
        _fileSystem.CreateDirectory(subDir);
        _fileSystem.WriteAllText($"{dir}/file1.txt", "Content1");
        _fileSystem.WriteAllText($"{subDir}/file2.txt", "Content2");

        // Act
        string[] files = _fileSystem.GetFiles(dir, "*.txt", SearchOption.AllDirectories);

        // Assert
        Assert.That(files.Length, Is.EqualTo(2), "GetFiles should return matching files in all directories.");
        Assert.That(files, Contains.Item($"{dir}/file1.txt"));
        Assert.That(files, Contains.Item($"{subDir}/file2.txt"));
    }

    [Test]
    public void GetFiles_WithNoMatchingPattern_ShouldReturnEmptyArray()
    {
        // Arrange
        string dir = "/myDir";
        _fileSystem.CreateDirectory(dir);
        _fileSystem.WriteAllText($"{dir}/file1.txt", "Content1");

        // Act
        string[] files = _fileSystem.GetFiles(dir, "*.log");

        // Assert
        Assert.That(files.Length, Is.EqualTo(0), "GetFiles should return an empty array when no files match the pattern.");
    }

    [Test]
    public void LargeFile_ReadWrite_ShouldWork()
    {
        // Arrange
        string path = "/largefile.txt";
        string content = new string('a', 10_000_000); // 10 MB

        // Act
        _fileSystem.WriteAllText(path, content);
        string actualContent = _fileSystem.ReadAllText(path);

        // Assert
        Assert.That(actualContent.Length, Is.EqualTo(content.Length), "Large file should be written and read correctly.");
        Assert.That(actualContent, Is.EqualTo(content), "Content of the large file should match the original.");
    }

    [Test]
    public void FileLocking_ShouldPreventConcurrentWrites()
    {
        // Arrange
        string path = "/locked.txt";
        _fileSystem.WriteAllText(path, "Initial content");

        // Act
        Exception exception = null;
        using (_fileSystem.CreateFileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            try
            {
                _fileSystem.WriteAllText(path, "New content");
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }

        // Assert
        Assert.That(exception, Is.Not.Null, "An exception should be thrown when writing to a locked file.");
        Assert.That(exception, Is.TypeOf<IOException>(), "Exception should be of type IOException.");
        string content = _fileSystem.ReadAllText(path);
        Assert.That(content, Is.EqualTo("Initial content"), "Content should remain unchanged after a failed write attempt.");
    }

    [Test]
    public void PathNormalization_ShouldHandleDifferentFormats()
    {
        // Arrange
        string path = "/folder/file.txt";
        _fileSystem.WriteAllText(path, "Content");

        // Act & Assert
        Assert.That(_fileSystem.FileExists("/folder/file.txt"), Is.True, "File should exist with normalized path.");
        Assert.That(_fileSystem.FileExists("\\folder\\file.txt"), Is.True, "File existence should be recognized with backslashes.");
        Assert.That(_fileSystem.FileExists("/folder/../folder/./file.txt"), Is.True, "File existence should be recognized with relative segments.");
    }

    [Test]
    public void ReadNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() => _fileSystem.ReadAllText("/nonexistent.txt"), "Reading a nonexistent file should throw FileNotFoundException.");
        Assert.That(exception.Message, Is.EqualTo("File not found"), "Exception message should be 'File not found'.");
    }

    [Test]
    public void WriteToReadOnlyFile_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        string path = "/readonly.txt";
        _fileSystem.WriteAllText(path, "Content");
        _fileSystem.SetAttributes(path, FileAttributes.ReadOnly);

        // Act & Assert
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.WriteAllText(path, "New content"), "Writing to a read-only file should throw UnauthorizedAccessException.");
        Assert.That(exception.Message, Is.EqualTo("Access to the path is denied."), "Exception message should be 'Access to the path is denied.'.");
    }

    [Test]
    public void MoveFile_ShouldWork()
    {
        // Arrange
        string sourcePath = "/source.txt";
        string destPath = "/dest.txt";
        _fileSystem.WriteAllText(sourcePath, "Content");

        // Act
        _fileSystem.MoveFile(sourcePath, destPath);

        // Assert
        Assert.That(_fileSystem.FileExists(sourcePath), Is.False, "Source file should not exist after moving.");
        Assert.That(_fileSystem.FileExists(destPath), Is.True, "Destination file should exist after moving.");
        string content = _fileSystem.ReadAllText(destPath);
        Assert.That(content, Is.EqualTo("Content"), "Content should be preserved after moving the file.");
    }

    [Test]
    public void FileStream_PartialReadWrite_ShouldWork()
    {
        // Arrange
        string path = "/stream.txt";
        string content = "Hello, World!";
        _fileSystem.WriteAllText(path, content);

        // Act
        using (var stream = _fileSystem.CreateFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            byte[] buffer = new byte[5];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            // Assert
            Assert.That(bytesRead, Is.EqualTo(5), "Should read 5 bytes.");
            string readContent = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.That(readContent, Is.EqualTo("Hello"), "Content read should match the expected substring.");
        }
    }

    [Test]
    public void EmptyFile_ShouldBeHandledCorrectly()
    {
        // Arrange
        string path = "/empty.txt";
        _fileSystem.WriteAllText(path, string.Empty);

        // Act
        string content = _fileSystem.ReadAllText(path);

        // Assert
        Assert.That(_fileSystem.FileExists(path), Is.True, "Empty file should exist.");
        Assert.That(content, Is.Empty, "Content of the empty file should be empty.");
    }

    [Test]
    public void VeryLongFileName_ShouldBeHandled()
    {
        // Arrange
        string longFileName = new string('a', 255) + ".txt";
        string path = "/" + longFileName;

        // Act
        _fileSystem.WriteAllText(path, "Content");

        // Assert
        Assert.That(_fileSystem.FileExists(path), Is.True, "File with a very long name should be handled.");
        string content = _fileSystem.ReadAllText(path);
        Assert.That(content, Is.EqualTo("Content"), "Content should be correctly read from the file with a long name.");
    }

    [Test]
    public void WriteAndReadFile_ShouldReturnSameContent()
    {
        // Arrange
        string path = "/testfile.txt";
        string content = "Hello, this is a test content!";

        // Act
        _fileSystem.WriteAllText(path, content);
        string actualContent = _fileSystem.ReadAllText(path);

        // Assert
        Assert.That(actualContent, Is.EqualTo(content), "Content read should match the content written.");
    }

    [Test]
    public void WriteAndReadFile_WithVariousContentTypes_ShouldWork()
    {
        // Arrange
        var testCases = new[]
        {
            ("Empty string", string.Empty),
            ("Empty string multiline", "\n\n\n"),
            ("Short string", "Hello"),
            ("Long string", new string('a', 1_000_000)),
            ("String with special characters", "Line 1\nLine 2\rTab\tQuote\"Backslash\\"),
            ("Unicode characters", "こんにちは世界"),
        };

        foreach (var (description, content) in testCases)
        {
            string path = $"/test_{description.Replace(" ", "_")}.txt";

            // Act
            _fileSystem.WriteAllText(path, content);
            string actualContent = _fileSystem.ReadAllText(path);

            // Assert
            Assert.That(actualContent, Is.EqualTo(content), $"Content read should match for case: {description}");
            _logger.LogInformation($"Tested {description}, Length: {content.Length}");
        }
    }

    [Test]
    public void ConcurrentAccess_ShouldHandleMultipleThreads()
    {
        // Arrange
        string path = "/concurrent.txt";
        _fileSystem.WriteAllText(path, "Start");
        int threadCount = 10;
        Task[] tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks[i] = Task.Run(() =>
            {
                _fileSystem.AppendAllText(path, $" Thread{threadId}");
            });
        }
        Task.WaitAll(tasks);

        // Assert
        string content = _fileSystem.ReadAllText(path);
        Assert.That(content, Does.StartWith("Start"), "Content should start with 'Start'");
        for (int i = 0; i < threadCount; i++)
        {
            Assert.That(content, Does.Contain($"Thread{i}"), $"Content should contain 'Thread{i}'");
        }
    }

    [Test]
    public void DeleteFile_ShouldRemoveFile()
    {
        // Arrange
        string path = "/deleteTest.txt";
        _fileSystem.WriteAllText(path, "To be deleted");

        // Act
        _fileSystem.DeleteFile(path);

        // Assert
        Assert.That(_fileSystem.FileExists(path), Is.False, "File should not exist after deletion.");
        Assert.Throws<FileNotFoundException>(() => _fileSystem.ReadAllText(path), "Reading a deleted file should throw FileNotFoundException.");
    }

    [Test]
    public void GetDirectoryName_ShouldReturnCorrectDirectoryName()
    {
        // Arrange
        string path = "/folder/subfolder/file.txt";

        // Act
        string directoryName = _fileSystem.GetDirectoryName(path);

        // Assert
        Assert.That(directoryName, Is.EqualTo("/folder/subfolder"), "GetDirectoryName should return the parent directory path.");
    }

    [Test]
    public void GetDirectoryName_ShouldReturnNull_WhenNoDirectory()
    {
        // Arrange
        string path = "/file.txt";

        // Act
        string directoryName = _fileSystem.GetDirectoryName(path);

        // Assert
        Assert.That(directoryName, Is.Null, "GetDirectoryName should return null when there is no parent directory.");
    }

    [Test]
    public void GetFileNameWithoutExtension_ShouldReturnFileNameWithoutExtension()
    {
        // Arrange
        string path = "/folder/subfolder/file.txt";

        // Act
        string fileNameWithoutExtension = _fileSystem.GetFileNameWithoutExtension(path);

        // Assert
        Assert.That(fileNameWithoutExtension, Is.EqualTo("file"), "GetFileNameWithoutExtension should return the file name without its extension.");
    }

    [Test]
    public void GetFileNameWithoutExtension_ShouldReturnEmpty_WhenNoFileName()
    {
        // Arrange
        string path = "/folder/subfolder/";

        // Act
        string fileNameWithoutExtension = _fileSystem.GetFileNameWithoutExtension(path);

        // Assert
        Assert.That(fileNameWithoutExtension, Is.Empty, "GetFileNameWithoutExtension should return empty when there is no file name.");
    }

    [Test]
    public void GetExtension_ShouldReturnFileExtension()
    {
        // Arrange
        string path = "/folder/subfolder/file.txt";

        // Act
        string extension = _fileSystem.GetExtension(path);

        // Assert
        Assert.That(extension, Is.EqualTo(".txt"), "GetExtension should return the file extension including the dot.");
    }

    [Test]
    public void GetExtension_ShouldReturnEmpty_WhenNoExtension()
    {
        // Arrange
        string path = "/folder/subfolder/file";

        // Act
        string extension = _fileSystem.GetExtension(path);

        // Assert
        Assert.That(extension, Is.Empty, "GetExtension should return empty when there is no file extension.");
    }

    [Test]
    public void Combine_ShouldReturnCombinedPath()
    {
        // Arrange
        string path1 = "/folder";
        string path2 = "subfolder/file.txt";

        // Act
        string combinedPath = _fileSystem.Combine(path1, path2);

        // Assert
        Assert.That(combinedPath, Is.EqualTo("/folder/subfolder/file.txt"), "Combine should correctly combine two paths.");
    }

    [Test]
    public void Combine_ShouldHandleNullAndEmptyPaths()
    {
        // Arrange
        string path1 = null;
        string path2 = "/folder/file.txt";

        // Act
        string combinedPath1 = _fileSystem.Combine(path1, path2);
        string combinedPath2 = _fileSystem.Combine(path2, path1);

        // Assert
        Assert.That(combinedPath1, Is.EqualTo("/folder/file.txt"), "Combine should return the second path when the first is null.");
        Assert.That(combinedPath2, Is.EqualTo("/folder/file.txt"), "Combine should return the first path when the second is null.");
    }

    [Test]
    public void GetFileName_ShouldReturnFileName()
    {
        // Arrange
        string path = "/folder/subfolder/file.txt";

        // Act
        string fileName = _fileSystem.GetFileName(path);

        // Assert
        Assert.That(fileName, Is.EqualTo("file.txt"), "GetFileName should return the file name with extension.");
    }

    [Test]
    public void GetFileName_ShouldReturnEmpty_WhenPathEndsWithSlash()
    {
        // Arrange
        string path = "/folder/subfolder/";

        // Act
        string fileName = _fileSystem.GetFileName(path);

        // Assert
        Assert.That(fileName, Is.Empty, "GetFileName should return empty when path ends with a slash.");
    }

    [Test]
    public void GetRelativePath_ShouldReturnPathRelativeToAnother()
    {
        // Arrange
        string relativeTo = "/folder";
        string path = "/folder/subfolder/file.txt";

        // Act
        string relativePath = _fileSystem.GetRelativePath(relativeTo, path);

        // Assert
        Assert.That(relativePath, Is.EqualTo("subfolder/file.txt"), "GetRelativePath should return the path relative to the specified base path.");
    }

    [Test]
    public void GetRelativePath_ShouldReturnFullPath_WhenNotUnderBasePath()
    {
        // Arrange
        string relativeTo = "/anotherfolder";
        string path = "/folder/subfolder/file.txt";

        // Act
        string relativePath = _fileSystem.GetRelativePath(relativeTo, path);

        // Assert
        Assert.That(relativePath, Is.EqualTo("/folder/subfolder/file.txt"), "GetRelativePath should return the full path if it is not under the base path.");
    }
}
