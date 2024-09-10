namespace Orc.Monitoring.IO;

using System.IO;
using System.Text;
using System.Threading.Tasks;

public class FileSystem : IFileSystem
{
    public static FileSystem Instance { get; } = new();
    public bool FileExists(string path) => File.Exists(path);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void AppendAllText(string path, string contents) => File.AppendAllText(path, contents);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    public string[] GetFiles(string path)
        => Directory.GetFiles(path);

    public string[] GetFiles(string path, string searchPattern)
    {
        return Directory.GetFiles(path, searchPattern);
    }

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetFiles(path, searchPattern, searchOption);
    }

    public void SetAttributes(string path, FileAttributes fileAttributes)
        => File.SetAttributes(path, fileAttributes);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public TextWriter CreateStreamWriter(string fullPath, bool append, Encoding encoding)
    {
        return new StreamWriter(fullPath, append, encoding);
    }
    
    public StreamReader CreateStreamReader(string fullPath)
    {
        return new StreamReader(fullPath);
    }
    
    public Task<string> ReadAllTextAsync(string fullPath)
    {
        return File.ReadAllTextAsync(fullPath);
    }

    public string[] GetDirectories(string sourcePath)
    {
        return Directory.GetDirectories(sourcePath);
    }

    public Stream CreateFileStream(string sourcePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
    {
        return new FileStream(sourcePath, fileMode, fileAccess, fileShare);
    }

    public Stream CreateFileStream(string sourcePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int bufferSize, FileOptions fileOptions)
    {
        return new FileStream(sourcePath, fileMode, fileAccess, fileShare, bufferSize, fileOptions);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public async Task<string[]> ReadAllLinesAsync(string path)
    {
        return await File.ReadAllLinesAsync(path);
    }

    public async Task WriteAllTextAsync(string path, string contents)
    {
        await File.WriteAllTextAsync(path, contents);
    }

    // ReadAllLines
    public string[] ReadAllLines(string path)
    {
        return File.ReadAllLines(path);
    }
    public void CopyFile(string sourceFileName, string destFileName, bool overwrite)
    {
        File.Copy(sourceFileName, destFileName, overwrite);
    }

    public void MoveFile(string sourceFileName, string destFileName)
    {
        File.Move(sourceFileName, destFileName);
    }
}
