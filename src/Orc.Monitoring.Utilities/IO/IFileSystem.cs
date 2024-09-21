namespace Orc.Monitoring.Utilities.IO;

using System.IO;
using System.Text;
using System.Threading.Tasks;

public interface IFileSystem
{
    bool FileExists(string path);
    void WriteAllText(string path, string contents);
    string ReadAllText(string path);
    void AppendAllText(string path, string contents);
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive);
    string[] GetFiles(string path);
    string[] GetFiles(string path, string searchPattern);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    void SetAttributes(string path, FileAttributes fileAttributes);
    FileAttributes GetAttributes(string path);
    TextWriter CreateStreamWriter(string fullPath, bool append, Encoding encoding);
    StreamReader CreateStreamReader(string fullPath);
    Task<string> ReadAllTextAsync(string fullPath);
    string[] GetDirectories(string sourcePath);
    Stream CreateFileStream(string sourcePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare);
    Stream CreateFileStream(string sourcePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int bufferSize, FileOptions fileOptions);
    void DeleteFile(string path);
    Task<string[]> ReadAllLinesAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
    void CopyFile(string sourceFileName, string destFileName, bool overwrite);
    string[] ReadAllLines(string path);

    void MoveFile(string sourceFileName, string destFileName);
    string? GetDirectoryName(string path);
    string GetFileNameWithoutExtension(string path);
    string GetExtension(string path);
    string Combine(string path1, string path2);
    string Combine(string path1, string path2, string path3);
    string GetFileName(string path);
    string GetRelativePath(string relativeTo, string path);
    string GetTempPath();
    string GetRandomFileName();
    string GetTempFileName();
}
