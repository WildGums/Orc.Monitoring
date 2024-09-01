#pragma warning disable CL0002
namespace Orc.Monitoring.Tests;

using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

[TestFixture]
public class InMemoryFileSystemTests
{
    private InMemoryFileSystem _fileSystem;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = new InMemoryFileSystem();
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
        _fileSystem.WriteAllText("/asyncTest.txt", "Async Hello, World!");

        string contents = await _fileSystem.ReadAllTextAsync("/asyncTest.txt");

        Assert.That(contents, Is.EqualTo("Async Hello, World!"));
    }

    [Test]
    public void CreateFileStream_ShouldCreateFile_WhenFileDoesNotExist()
    {
        using (var stream = _fileSystem.CreateFileStream("/streamTest.txt", FileMode.Create, FileAccess.Write, FileShare.None, 1024, FileOptions.None))
        {
            byte[] bytes = Encoding.UTF8.GetBytes("Stream Test");
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
            byte[] bytes = Encoding.UTF8.GetBytes("End");
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
}


