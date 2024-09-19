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
using TestUtilities;
using System.Collections.Generic;

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
        bool exists = _fileSystem.FileExists(TestConstants.DefaultTestFilePath);
        Assert.That(exists, Is.False, "FileExists should return false for a nonexistent file.");
    }

    [Test]
    public void WriteAllText_ShouldCreateFile_WhenPathIsValid()
    {
        _fileSystem.WriteAllText(TestConstants.DefaultTestFilePath, TestConstants.DefaultTestContent);
        Assert.That(_fileSystem.FileExists(TestConstants.DefaultTestFilePath), Is.True, "File should exist after WriteAllText.");
    }

    [Test]
    public void ReadAllText_ShouldReturnContentsOfFile_WhenFileExists()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
        string actualContent = _fileSystem.ReadAllText(path);
        Assert.That(actualContent, Is.EqualTo(TestConstants.DefaultTestContent), "ReadAllText should return the content that was written.");
    }

    [Test]
    public void AppendAllText_ShouldAppendToFile_WhenFileExists()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, "Hello, ");
        _fileSystem.AppendAllText(path, "World!");
        string contents = _fileSystem.ReadAllText(path);
        Assert.That(contents, Is.EqualTo("Hello, World!"));
    }

    [Test]
    public void CreateDirectory_ShouldCreateDirectory_WhenPathIsValid()
    {
        _fileSystem.CreateDirectory(TestConstants.DefaultTestFolderPath);
        Assert.That(_fileSystem.FileExists(TestConstants.DefaultTestFolderPath), Is.False, "Directory should not be treated as a file.");
    }

    [Test]
    public void DirectoryExists_ShouldReturnFalse_WhenDirectoryDoesNotExist()
    {
        bool exists = _fileSystem.DirectoryExists(TestConstants.DefaultTestFolderPath);
        Assert.That(exists, Is.False, "DirectoryExists should return false for a nonexistent directory.");
    }

    [Test]
    public void DeleteDirectory_ShouldRemoveDirectory_WhenRecursiveIsFalseAndDirectoryIsEmpty()
    {
        string path = TestConstants.DefaultTestFolderPath;
        _fileSystem.CreateDirectory(path);
        _fileSystem.DeleteDirectory(path, recursive: false);
        Assert.That(_fileSystem.FileExists(path), Is.False, "Directory should be deleted when recursive is false and directory is empty.");
    }

    [Test]
    public void DeleteDirectory_ShouldThrowIOException_WhenDirectoryIsNotEmptyAndRecursiveIsFalse()
    {
        string path = TestConstants.DefaultTestFolderPath;
        _fileSystem.CreateDirectory(path);
        _fileSystem.WriteAllText($"{path}/{TestConstants.DefaultTestFileName}", TestConstants.DefaultTestContent);
        Assert.Throws<IOException>(() => _fileSystem.DeleteDirectory(path, recursive: false), "Deleting a non-empty directory without recursive should throw IOException.");
    }

    [Test]
    public void DeleteDirectory_ShouldDeleteDirectoryAndContents_WhenRecursiveIsTrue()
    {
        string path = TestConstants.DefaultTestFolderPath;
        _fileSystem.CreateDirectory(path);
        _fileSystem.WriteAllText($"{path}/{TestConstants.DefaultTestFileName}", TestConstants.DefaultTestContent);
        _fileSystem.DeleteDirectory(path, recursive: true);
        Assert.That(_fileSystem.DirectoryExists(path), Is.False, "Directory should be deleted recursively.");
        Assert.That(_fileSystem.FileExists($"{path}/{TestConstants.DefaultTestFileName}"), Is.False, "Files within the directory should also be deleted.");
    }

    [Test]
    public void GetFiles_ShouldReturnAllFilesInDirectory()
    {
        string dir = TestConstants.DefaultTestFolderPath;
        _fileSystem.CreateDirectory(dir);
        _fileSystem.WriteAllText($"{dir}/file1.txt", TestConstants.DefaultTestContent);
        _fileSystem.WriteAllText($"{dir}/file2.txt", TestConstants.DefaultTestContent);
        string[] files = _fileSystem.GetFiles(dir);
        Assert.That(files.Length, Is.EqualTo(2), "GetFiles should return all files in the directory.");
        Assert.That(files, Contains.Item($"{dir}/file1.txt"), "File1 should be in the list of files.");
        Assert.That(files, Contains.Item($"{dir}/file2.txt"), "File2 should be in the list of files.");
    }

    [Test]
    public void SetAttributes_ShouldSetAttributesOnFile()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
        _fileSystem.SetAttributes(path, TestConstants.ReadOnlyFileAttributes);
        FileAttributes attributes = _fileSystem.GetAttributes(path);
        Assert.That(attributes, Is.EqualTo(TestConstants.ReadOnlyFileAttributes), "SetAttributes should set the specified attributes on the file.");
    }

    [Test]
    public async Task ReadAllTextAsync_ShouldReturnContentsOfFile_WhenFileExists()
    {
        string path = TestConstants.DefaultTestFilePath;
        await _fileSystem.WriteAllTextAsync(path, TestConstants.DefaultTestContent);
        string actualContent = await _fileSystem.ReadAllTextAsync(path);
        Assert.That(actualContent, Is.EqualTo(TestConstants.DefaultTestContent), "ReadAllTextAsync should return the content that was written asynchronously.");
    }

    [Test]
    public void CreateFileStream_ShouldCreateFile_WhenFileDoesNotExist()
    {
        string path = TestConstants.DefaultTestFilePath;
        using (var stream = _fileSystem.CreateFileStream(path, TestConstants.DefaultFileMode, TestConstants.DefaultFileAccess, TestConstants.DefaultFileShare))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(TestConstants.DefaultTestContent);
            stream.Write(bytes, 0, bytes.Length);
        }
        Assert.That(_fileSystem.FileExists(path), Is.True, "File should exist after writing with a stream.");
        string actualContent = _fileSystem.ReadAllText(path);
        Assert.That(actualContent, Is.EqualTo(TestConstants.DefaultTestContent), "Content written via stream should be correctly saved.");
    }

    [Test]
    public void CreateFileStream_ShouldAppendToFile_WhenFileExistsAndFileModeIsAppend()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, "Start ");
        using (var stream = _fileSystem.CreateFileStream(path, FileMode.Append, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = Encoding.UTF8.GetBytes("End");
            stream.Write(bytes, 0, bytes.Length);
        }
        string content = _fileSystem.ReadAllText(path);
        Assert.That(content, Is.EqualTo("Start End"));
    }

    [Test]
    public void GetFiles_WithSearchPattern_ShouldReturnMatchingFiles()
    {
        string dir = TestConstants.DefaultTestFolderPath;
        _fileSystem.CreateDirectory(dir);
        _fileSystem.WriteAllText($"{dir}/file1.txt", TestConstants.DefaultTestContent);
        _fileSystem.WriteAllText($"{dir}/file2.log", TestConstants.DefaultTestContent);
        _fileSystem.WriteAllText($"{dir}/file3.txt", TestConstants.DefaultTestContent);
        string[] files = _fileSystem.GetFiles(dir, "*.txt");
        Assert.That(files.Length, Is.EqualTo(2), "GetFiles should return files matching the search pattern.");
        Assert.That(files, Contains.Item($"{dir}/file1.txt"));
        Assert.That(files, Contains.Item($"{dir}/file3.txt"));
    }

    [Test]
    public void GetFiles_WithSearchPatternAndAllDirectories_ShouldReturnMatchingFilesInAllDirectories()
    {
        string dir = TestConstants.DefaultTestFolderPath;
        string subDir = $"{dir}/subDir";
        _fileSystem.CreateDirectory(subDir);
        _fileSystem.WriteAllText($"{dir}/file1.txt", TestConstants.DefaultTestContent);
        _fileSystem.WriteAllText($"{subDir}/file2.txt", TestConstants.DefaultTestContent);
        string[] files = _fileSystem.GetFiles(dir, "*.txt", SearchOption.AllDirectories);
        Assert.That(files.Length, Is.EqualTo(2), "GetFiles should return matching files in all directories.");
        Assert.That(files, Contains.Item($"{dir}/file1.txt"));
        Assert.That(files, Contains.Item($"{subDir}/file2.txt"));
    }

    [Test]
    public void GetFiles_WithNoMatchingPattern_ShouldReturnEmptyArray()
    {
        string dir = TestConstants.DefaultTestFolderPath;
        _fileSystem.CreateDirectory(dir);
        _fileSystem.WriteAllText($"{dir}/file1.txt", TestConstants.DefaultTestContent);
        string[] files = _fileSystem.GetFiles(dir, "*.log");
        Assert.That(files.Length, Is.EqualTo(0), "GetFiles should return an empty array when no files match the pattern.");
    }

    [Test]
    public void LargeFile_ReadWrite_ShouldWork()
    {
        string path = TestConstants.DefaultTestFilePath;
        string content = new string('a', TestConstants.LargeFileSize);
        _fileSystem.WriteAllText(path, content);
        string actualContent = _fileSystem.ReadAllText(path);
        Assert.That(actualContent.Length, Is.EqualTo(content.Length), "Large file should be written and read correctly.");
        Assert.That(actualContent, Is.EqualTo(content), "Content of the large file should match the original.");
    }

    [Test]
    public void FileLocking_ShouldPreventConcurrentWrites()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
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
        Assert.That(exception, Is.Not.Null, "An exception should be thrown when writing to a locked file.");
        Assert.That(exception, Is.TypeOf<IOException>(), "Exception should be of type IOException.");
        string content = _fileSystem.ReadAllText(path);
        Assert.That(content, Is.EqualTo(TestConstants.DefaultTestContent), "Content should remain unchanged after a failed write attempt.");
    }

    [Test]
    public void PathNormalization_ShouldHandleDifferentFormats()
    {
        string path = "/folder/file.txt";
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
        Assert.That(_fileSystem.FileExists("/folder/file.txt"), Is.True, "File should exist with normalized path.");
        Assert.That(_fileSystem.FileExists("\\folder\\file.txt"), Is.True, "File existence should be recognized with backslashes.");
        Assert.That(_fileSystem.FileExists("/folder/../folder/./file.txt"), Is.True, "File existence should be recognized with relative segments.");
    }

    [Test]
    public void ReadNonExistentFile_ShouldThrowFileNotFoundException()
    {
        var exception = Assert.Throws<FileNotFoundException>(() => _fileSystem.ReadAllText("/nonexistent.txt"), "Reading a nonexistent file should throw FileNotFoundException.");
        Assert.That(exception.Message, Is.EqualTo("File not found"), "Exception message should be 'File not found'.");
    }

    [Test]
    public void WriteToReadOnlyFile_ShouldThrowUnauthorizedAccessException()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
        _fileSystem.SetAttributes(path, FileAttributes.ReadOnly);
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.WriteAllText(path, "New content"), "Writing to a read-only file should throw UnauthorizedAccessException.");
        Assert.That(exception.Message, Is.EqualTo("Access to the path is denied."), "Exception message should be 'Access to the path is denied.'.");
    }

    [Test]
    public void MoveFile_ShouldWork()
    {
        string sourcePath = "/source.txt";
        string destPath = "/dest.txt";
        _fileSystem.WriteAllText(sourcePath, TestConstants.DefaultTestContent);
        _fileSystem.MoveFile(sourcePath, destPath);
        Assert.That(_fileSystem.FileExists(sourcePath), Is.False, "Source file should not exist after moving.");
        Assert.That(_fileSystem.FileExists(destPath), Is.True, "Destination file should exist after moving.");
        string content = _fileSystem.ReadAllText(destPath);
        Assert.That(content, Is.EqualTo(TestConstants.DefaultTestContent), "Content should be preserved after moving the file.");
    }

    [Test]
    public void FileStream_PartialReadWrite_ShouldWork()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
        using (var stream = _fileSystem.CreateFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            byte[] buffer = new byte[5];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            Assert.That(bytesRead, Is.EqualTo(5), "Should read 5 bytes.");
            string readContent = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.That(readContent, Is.EqualTo(TestConstants.DefaultTestContent.Substring(0, 5)), "Content read should match the expected substring.");
        }
    }

    [Test]
    public void EmptyFile_ShouldBeHandledCorrectly()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, string.Empty);
        string content = _fileSystem.ReadAllText(path);
        Assert.That(_fileSystem.FileExists(path), Is.True, "Empty file should exist.");
        Assert.That(content, Is.Empty, "Content of the empty file should be empty.");
    }

    [Test]
    public void VeryLongFileName_ShouldBeHandled()
    {
        string longFileName = new string('a', 255) + ".txt";
        string path = "/" + longFileName;
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
        Assert.That(_fileSystem.FileExists(path), Is.True, "File with a very long name should be handled.");
        string content = _fileSystem.ReadAllText(path);
        Assert.That(content, Is.EqualTo(TestConstants.DefaultTestContent), "Content should be correctly read from the file with a long name.");
    }

    [Test]
    public void WriteAndReadFile_ShouldReturnSameContent()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
        string actualContent = _fileSystem.ReadAllText(path);
        Assert.That(actualContent, Is.EqualTo(TestConstants.DefaultTestContent), "Content read should match the content written.");
    }

    [Test]
    public void WriteAndReadFile_WithVariousContentTypes_ShouldWork()
    {
        var testCases = new[]
        {
            ("Empty string", string.Empty),
            ("Empty string multiline", "\n\n\n"),
            ("Short string", "Hello"),
            ("Long string", new string('a', TestConstants.LargeFileSize)),
            ("String with special characters", "Line 1\nLine 2\rTab\tQuote\"Backslash\\"),
            ("Unicode characters", "こんにちは世界"),
        };

        foreach (var (description, content) in testCases)
        {
            string path = $"/test_{description.Replace(" ", "_")}.txt";
            _fileSystem.WriteAllText(path, content);
            string actualContent = _fileSystem.ReadAllText(path);
            Assert.That(actualContent, Is.EqualTo(content), $"Content read should match for case: {description}");
            _logger.LogInformation($"Tested {description}, Length: {content.Length}");
        }
    }

    [Test]
    public void ConcurrentAccess_ShouldHandleMultipleThreads()
    {
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, "Start");
        int threadCount = TestConstants.DefaultThreadCount;
        Task[] tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks[i] = Task.Run(() =>
            {
                _fileSystem.AppendAllText(path, $" Thread{threadId}");
            });
        }
        Task.WaitAll(tasks);

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
        string path = TestConstants.DefaultTestFilePath;
        _fileSystem.WriteAllText(path, TestConstants.DefaultTestContent);
        _fileSystem.DeleteFile(path);
        Assert.That(_fileSystem.FileExists(path), Is.False, "File should not exist after deletion.");
        Assert.Throws<FileNotFoundException>(() => _fileSystem.ReadAllText(path), "Reading a deleted file should throw FileNotFoundException.");
    }

    [Test]
    public void GetDirectoryName_ShouldReturnCorrectDirectoryName()
    {
        string path = "/folder/subfolder/file.txt";
        string directoryName = _fileSystem.GetDirectoryName(path);
        Assert.That(directoryName, Is.EqualTo("/folder/subfolder"), "GetDirectoryName should return the parent directory path.");
    }

    [Test]
    public void GetDirectoryName_ShouldReturnNull_WhenNoDirectory()
    {
        string path = "/file.txt";
        string directoryName = _fileSystem.GetDirectoryName(path);
        Assert.That(directoryName, Is.Null, "GetDirectoryName should return null when there is no parent directory.");
    }

    [Test]
    public void GetFileNameWithoutExtension_ShouldReturnFileNameWithoutExtension()
    {
        string path = "/folder/subfolder/file.txt";
        string fileNameWithoutExtension = _fileSystem.GetFileNameWithoutExtension(path);
        Assert.That(fileNameWithoutExtension, Is.EqualTo("file"), "GetFileNameWithoutExtension should return the file name without its extension.");
    }

    [Test]
    public void GetFileNameWithoutExtension_ShouldReturnEmpty_WhenNoFileName()
    {
        string path = "/folder/subfolder/";
        string fileNameWithoutExtension = _fileSystem.GetFileNameWithoutExtension(path);
        Assert.That(fileNameWithoutExtension, Is.Empty, "GetFileNameWithoutExtension should return empty when there is no file name.");
    }

    [Test]
    public void GetExtension_ShouldReturnFileExtension()
    {
        string path = "/folder/subfolder/file.txt";
        string extension = _fileSystem.GetExtension(path);
        Assert.That(extension, Is.EqualTo(".txt"), "GetExtension should return the file extension including the dot.");
    }

    [Test]
    public void GetExtension_ShouldReturnEmpty_WhenNoExtension()
    {
        string path = "/folder/subfolder/file";
        string extension = _fileSystem.GetExtension(path);
        Assert.That(extension, Is.Empty, "GetExtension should return empty when there is no file extension.");
    }

    [Test]
    public void Combine_ShouldReturnCombinedPath()
    {
        string path1 = "/folder";
        string path2 = "subfolder/file.txt";
        string combinedPath = _fileSystem.Combine(path1, path2);
        Assert.That(combinedPath, Is.EqualTo("/folder/subfolder/file.txt"), "Combine should correctly combine two paths.");
    }

    [Test]
    public void Combine_ShouldHandleNullAndEmptyPaths()
    {
        string path1 = null;
        string path2 = "/folder/file.txt";
        string combinedPath1 = _fileSystem.Combine(path1, path2);
        string combinedPath2 = _fileSystem.Combine(path2, path1);
        Assert.That(combinedPath1, Is.EqualTo("/folder/file.txt"), "Combine should return the second path when the first is null.");
        Assert.That(combinedPath2, Is.EqualTo("/folder/file.txt"), "Combine should return the first path when the second is null.");
    }

    [Test]
    public void GetFileName_ShouldReturnFileName()
    {
        string path = "/folder/subfolder/file.txt";
        string fileName = _fileSystem.GetFileName(path);
        Assert.That(fileName, Is.EqualTo("file.txt"), "GetFileName should return the file name with extension.");
    }

    [Test]
    public void GetFileName_ShouldReturnEmpty_WhenPathEndsWithSlash()
    {
        string path = "/folder/subfolder/";
        string fileName = _fileSystem.GetFileName(path);
        Assert.That(fileName, Is.Empty, "GetFileName should return empty when path ends with a slash.");
    }

    [Test]
    public void GetRelativePath_ShouldReturnPathRelativeToAnother()
    {
        string relativeTo = "/folder";
        string path = "/folder/subfolder/file.txt";
        string relativePath = _fileSystem.GetRelativePath(relativeTo, path);
        Assert.That(relativePath, Is.EqualTo("subfolder/file.txt"), "GetRelativePath should return the path relative to the specified base path.");
    }

    [Test]
    public void GetRelativePath_ShouldReturnFullPath_WhenNotUnderBasePath()
    {
        string relativeTo = "/anotherfolder";
        string path = "/folder/subfolder/file.txt";
        string relativePath = _fileSystem.GetRelativePath(relativeTo, path);
        Assert.That(relativePath, Is.EqualTo("/folder/subfolder/file.txt"), "GetRelativePath should return the full path if it is not under the base path.");
    }

    [Test]
    public void GetTempPath_ShouldReturnValidPath()
    {
        string tempPath = _fileSystem.GetTempPath();
        Assert.That(tempPath, Is.EqualTo("/tmp"), "GetTempPath should return '/tmp'");
        Assert.That(_fileSystem.DirectoryExists(tempPath), Is.True, "Temp directory should exist");
    }

    [Test]
    public void GetRandomFileName_ShouldReturnUniqueNames()
    {
        var fileNames = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            string fileName = _fileSystem.GetRandomFileName();
            Assert.That(fileNames.Add(fileName), Is.True, "GetRandomFileName should return unique names");
            Assert.That(fileName.Length, Is.EqualTo(12), "Random file name should be 12 characters long");
            Assert.That(fileName, Does.Match(@"^[a-zA-Z0-9]{8}\.[a-zA-Z0-9]{3}$"), "Random file name should match the expected pattern");
        }
    }

    [Test]
    public void GetTempFileName_ShouldCreateUniqueFile()
    {
        string tempFile1 = _fileSystem.GetTempFileName();
        string tempFile2 = _fileSystem.GetTempFileName();

        Assert.That(tempFile1, Is.Not.EqualTo(tempFile2), "GetTempFileName should return unique file names");
        Assert.That(_fileSystem.FileExists(tempFile1), Is.True, "Temp file 1 should exist");
        Assert.That(_fileSystem.FileExists(tempFile2), Is.True, "Temp file 2 should exist");
        Assert.That(_fileSystem.ReadAllText(tempFile1), Is.Empty, "Temp file 1 should be empty");
        Assert.That(_fileSystem.ReadAllText(tempFile2), Is.Empty, "Temp file 2 should be empty");
    }

    [Test]
    public void ReadAllLines_ShouldReturnCorrectLines()
    {
        string path = TestConstants.DefaultTestFilePath;
        string content = "Line1\nLine2\nLine3";
        _fileSystem.WriteAllText(path, content);

        string[] lines = _fileSystem.ReadAllLines(path);

        Assert.That(lines.Length, Is.EqualTo(3), "ReadAllLines should return correct number of lines");
        Assert.That(lines[0], Is.EqualTo("Line1"), "First line should be correct");
        Assert.That(lines[1], Is.EqualTo("Line2"), "Second line should be correct");
        Assert.That(lines[2], Is.EqualTo("Line3"), "Third line should be correct");
    }
}
