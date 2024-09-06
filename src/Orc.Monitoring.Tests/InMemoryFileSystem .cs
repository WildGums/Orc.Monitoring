#pragma warning disable IDISP007
#pragma warning disable CL0001
#pragma warning disable IDISP001
namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO;
using Microsoft.Extensions.Logging;

public class InMemoryFileSystem : IFileSystem, IDisposable
{
    private readonly ILogger<InMemoryFileSystem> _logger;
    private readonly ConcurrentDictionary<string, InMemoryFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileAttributes> _directoryAttributes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _fileLocks = new();
    private readonly object _fileLock = new();

    private bool _disposed;

    public InMemoryFileSystem(IMonitoringLoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<InMemoryFileSystem>();
        _directories.Add("/");
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(NormalizePath(path));
    }

    public void WriteAllText(string path, string contents)
    {
        var normalizedPath = NormalizePath(path);
        var directoryPath = NormalizePath(Path.GetDirectoryName(normalizedPath));

        _logger.LogInformation("Writing text to file at path: {Path}", path);

        lock (_fileLock)
        {
            if (_fileLocks.ContainsKey(normalizedPath))
            {
                _logger.LogError("File is being used by another process: {Path}", path);
                throw new IOException("The process cannot access the file because it is being used by another process.");
            }

            if (_directoryAttributes.TryGetValue(directoryPath, out var dirAttributes) &&
                (dirAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                _logger.LogError("Access to the path is denied (directory is read-only): {Path}", path);
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }

            if (_files.TryGetValue(normalizedPath, out var existingFile) &&
                (existingFile.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                _logger.LogError("Access to the path is denied (file is read-only): {Path}", path);
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }

            EnsureDirectoryExists(directoryPath);

            _fileLocks[normalizedPath] = new object();

            try
            {
                byte[] contentBytes = Encoding.UTF8.GetBytes(contents);

                _files[normalizedPath] = new InMemoryFile
                {
                    Contents = new MemoryStream(contentBytes),
                    Attributes = FileAttributes.Normal
                };

                _logger.LogInformation("Successfully wrote text to file at path: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write text to file at path: {Path}", path);
                throw;
            }
            finally
            {
                _fileLocks.TryRemove(normalizedPath, out _);
            }
        }
    }

    public string ReadAllText(string path)
    {
        var normalizedPath = NormalizePath(path);

        _logger.LogInformation("Reading text from file at path: {Path}", path);

        lock (_fileLock)
        {
            if (!_files.TryGetValue(normalizedPath, out var file))
            {
                _logger.LogError("File not found: {Path}", path);
                throw new FileNotFoundException("File not found", path);
            }

            file.Contents.Position = 0;
            using var reader = new StreamReader(file.Contents, Encoding.UTF8, true, 1024, true);
            var content = reader.ReadToEnd();
            file.Contents.Position = 0;
            _logger.LogInformation("Successfully read text from file at path: {Path}", path);
            return content;
        }
    }

    public void AppendAllText(string path, string contents)
    {
        var normalizedPath = NormalizePath(path);
        var directoryPath = Path.GetDirectoryName(normalizedPath);

        lock (_fileLock)
        {
            EnsureDirectoryExists(directoryPath);

            if (!_files.TryGetValue(normalizedPath, out var existingFile))
            {
                WriteAllText(normalizedPath, contents);
            }
            else
            {
                var newContents = new MemoryStream();
                existingFile.Contents.Position = 0;
                existingFile.Contents.CopyTo(newContents);
                newContents.Position = newContents.Length;

                using (var writer = new StreamWriter(newContents, Encoding.UTF8, 1024, true))
                {
                    writer.Write(contents);
                    writer.Flush();
                }

                existingFile.Contents.Dispose();
                existingFile.Contents = newContents;
            }
        }
    }

    public void CreateDirectory(string path)
    {
        var normalizedPath = NormalizePath(path);
        EnsureDirectoryExists(Path.GetDirectoryName(normalizedPath));
        _directories.Add(normalizedPath);
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(NormalizePath(path));
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        var normalizedPath = NormalizePath(path);
        if (recursive)
        {
            var directoriesToDelete = _directories.Where(d => d.StartsWith(normalizedPath)).ToList();
            var filesToDelete = _files.Keys.Where(f => f.StartsWith(normalizedPath)).ToList();

            foreach (var dir in directoriesToDelete)
                _directories.Remove(dir);

            foreach (var file in filesToDelete)
            {
                _files.TryRemove(file, out var removedFile);
                removedFile?.Dispose();
            }
        }
        else
        {
            if (_directories.Any(d => d.StartsWith(normalizedPath + "/")) || _files.Keys.Any(f => f.StartsWith(normalizedPath + "/")))
                throw new IOException("The directory is not empty");

            _directories.Remove(normalizedPath);
        }
    }

    public string[] GetFiles(string path)
    {
        return GetFiles(path, "*.*");
    }

    public string[] GetFiles(string path, string searchPattern)
    {
        return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
    }

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        var normalizedPath = NormalizePath(path);
        if (!_directories.Contains(normalizedPath))
            throw new DirectoryNotFoundException("Directory not found: " + path);

        var searchPatternRegex = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        var files = _files.Keys.Where(f =>
        {
            var fileDir = NormalizePath(Path.GetDirectoryName(f));
            return (searchOption == SearchOption.AllDirectories && fileDir.StartsWith(normalizedPath)) ||
                   (searchOption == SearchOption.TopDirectoryOnly && fileDir == normalizedPath);
        });

        files = files.Where(f => searchPatternRegex.IsMatch(Path.GetFileName(f)));

        return files.ToArray();
    }

    public void SetAttributes(string path, FileAttributes fileAttributes)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var file))
        {
            file.Attributes = fileAttributes;
        }
        else if (_directories.Contains(normalizedPath))
        {
            _directoryAttributes[normalizedPath] = fileAttributes;
        }
        else
        {
            throw new FileNotFoundException("Path not found", path);
        }
    }

    public FileAttributes GetAttributes(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var file))
        {
            return file.Attributes;
        }
        else if (_directories.Contains(normalizedPath))
        {
            return _directoryAttributes.GetValueOrDefault(normalizedPath, FileAttributes.Directory);
        }
        else
        {
            throw new FileNotFoundException("Path not found", path);
        }
    }

    public TextWriter CreateStreamWriter(string fullPath, bool append, Encoding encoding)
    {
        var normalizedPath = NormalizePath(fullPath);
        EnsureDirectoryExists(Path.GetDirectoryName(normalizedPath));
        var fileStream = CreateFileStream(normalizedPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1024, FileOptions.None);
        return new StreamWriter(fileStream, encoding);
    }

    public StreamReader CreateStreamReader(string fullPath)
    {
        var normalizedPath = NormalizePath(fullPath);
        if (!_files.ContainsKey(normalizedPath))
            throw new FileNotFoundException("File not found", fullPath);

        var fileStream = CreateFileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024, FileOptions.None);
        return new StreamReader(fileStream, Encoding.UTF8, true, 1024, true);
    }

    public async Task<string> ReadAllTextAsync(string fullPath)
    {
        var normalizedPath = NormalizePath(fullPath);
        if (!_files.TryGetValue(normalizedPath, out var file))
            throw new FileNotFoundException("File not found", fullPath);

        lock (_fileLock)
        {
            file.Contents.Position = 0;
            using var reader = new StreamReader(file.Contents, Encoding.UTF8, true, 1024, true);
            return reader.ReadToEnd();
        }
    }

    public string[] GetDirectories(string sourcePath)
    {
        var normalizedPath = NormalizePath(sourcePath);
        var directories = new List<string>();

        foreach (var directory in _directories)
        {
            if (directory.StartsWith(normalizedPath) && directory != normalizedPath)
            {
                var relativePath = directory.Substring(normalizedPath.Length).TrimStart('/');
                if (!relativePath.Contains('/'))
                {
                    directories.Add(directory);
                }
            }
        }

        return directories.ToArray();
    }

    public Stream CreateFileStream(string sourcePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
    {
        return CreateFileStream(sourcePath, fileMode, fileAccess, fileShare, 1024, FileOptions.None);
    }

    public Stream CreateFileStream(string sourcePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int bufferSize, FileOptions fileOptions)
    {
        var normalizedPath = NormalizePath(sourcePath);
        var directoryPath = NormalizePath(Path.GetDirectoryName(normalizedPath));

        _logger.LogInformation("Creating file stream for path: {Path}, FileMode: {FileMode}, FileAccess: {FileAccess}, FileShare: {FileShare}", sourcePath, fileMode, fileAccess, fileShare);

        lock (_fileLock)
        {
            if (_fileLocks.ContainsKey(normalizedPath))
            {
                if (fileShare == FileShare.None || (fileAccess & FileAccess.Write) == FileAccess.Write)
                {
                    _logger.LogError("File is being used by another process: {Path}", sourcePath);
                    throw new IOException("The process cannot access the file because it is being used by another process.");
                }
            }

            if (_directoryAttributes.TryGetValue(directoryPath, out var dirAttributes) &&
                (dirAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                _logger.LogError("Access to the path is denied (directory is read-only): {Path}", sourcePath);
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }

            if (_files.TryGetValue(normalizedPath, out var existingFile) &&
                (existingFile.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly &&
                (fileAccess & FileAccess.Write) == FileAccess.Write)
            {
                _logger.LogError("Access to the path is denied (file is read-only): {Path}", sourcePath);
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }

            EnsureDirectoryExists(directoryPath);

            if (!_files.TryGetValue(normalizedPath, out var file))
            {
                if (fileMode == FileMode.Open)
                {
                    _logger.LogError("File not found: {Path}", sourcePath);
                    throw new FileNotFoundException("File not found", sourcePath);
                }

                file = new InMemoryFile
                {
                    Contents = new MemoryStream(),
                    Attributes = FileAttributes.Normal
                };
                _files[normalizedPath] = file;
            }

            MemoryStream stream;
            switch (fileMode)
            {
                case FileMode.Create:
                case FileMode.CreateNew:
                case FileMode.Truncate:
                    stream = new MemoryStream();
                    break;
                case FileMode.Append:
                    stream = new MemoryStream();
                    file.Contents.Position = 0;
                    file.Contents.CopyTo(stream);
                    stream.Position = stream.Length;
                    break;
                default:
                    stream = new MemoryStream();
                    file.Contents.Position = 0;
                    file.Contents.CopyTo(stream);
                    stream.Position = 0;
                    break;
            }

            file.Contents = stream;

            if (fileAccess != FileAccess.Read)
            {
                _fileLocks[normalizedPath] = new object();
            }

            _logger.LogInformation("File stream created successfully for path: {Path}", sourcePath);

            return new NonClosingStreamWrapper(stream, () =>
            {
                lock (_fileLock)
                {
                    _logger.LogInformation("Disposing file stream for path: {Path}", sourcePath);
                    _fileLocks.TryRemove(normalizedPath, out _);
                }
            });
        }
    }

    public void DeleteFile(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryRemove(normalizedPath, out var file))
        {
            file.Dispose();
        }
        else
        {
            throw new FileNotFoundException("File not found", path);
        }
    }

    public async Task<string[]> ReadAllLinesAsync(string path)
    {
        var contents = await ReadAllTextAsync(path);
        return contents.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
    }

    public Task WriteAllTextAsync(string path, string contents)
    {
        WriteAllText(path, contents);
        return Task.CompletedTask;
    }

    public void CopyFile(string sourceFileName, string destFileName, bool overwrite)
    {
        var sourcePath = NormalizePath(sourceFileName);
        var destPath = NormalizePath(destFileName);

        lock (_fileLock)
        {
            if (!_files.TryGetValue(sourcePath, out var sourceFile))
                throw new FileNotFoundException("File not found", sourceFileName);

            if (_files.ContainsKey(destPath) && !overwrite)
                throw new IOException("File already exists");

            var destFile = new InMemoryFile
            {
                Contents = new MemoryStream(),
                Attributes = sourceFile.Attributes
            };

            sourceFile.Contents.Position = 0;
            sourceFile.Contents.CopyTo(destFile.Contents);
            destFile.Contents.Position = 0;

            _files[destPath] = destFile;
        }
    }

    public void MoveFile(string sourceFileName, string destFileName)
    {
        var sourcePath = NormalizePath(sourceFileName);
        var destPath = NormalizePath(destFileName);

        lock (_fileLock)
        {
            if (!_files.TryRemove(sourcePath, out var sourceFile))
                throw new FileNotFoundException("File not found", sourceFileName);

            if (!_files.TryAdd(destPath, sourceFile))
                throw new IOException("File already exists");
        }
    }

    public string[] ReadAllLines(string path)
    {
        var contents = ReadAllText(path);
        return contents.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
    }

    private string NormalizePath(string path)
    {
        path = path.Replace('\\', '/').TrimEnd('/');
        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }
        var parts = path.Split('/');
        var normalizedParts = new List<string>();

        foreach (var part in parts)
        {
            if (part == "." || string.IsNullOrEmpty(part))
                continue;
            if (part == ".." && normalizedParts.Count > 0)
                normalizedParts.RemoveAt(normalizedParts.Count - 1);
            else if (part != "..")
                normalizedParts.Add(part);
        }
        return "/" + string.Join("/", normalizedParts);
    }

    private void EnsureDirectoryExists(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        var normalizedPath = NormalizePath(path);
        var parts = normalizedPath.Split('/');
        var currentPath = string.Empty;

        foreach (var part in parts)
        {
            currentPath += part + "/";
            var dirPath = NormalizePath(currentPath);
            _directories.Add(dirPath);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            foreach (var file in _files.Values)
            {
                file.Dispose();
            }
            _files.Clear();
            _directories.Clear();
            _directoryAttributes.Clear();
            _fileLocks.Clear();
        }

        _disposed = true;
    }

    private sealed class InMemoryFile : IDisposable
    {
        public MemoryStream Contents { get; set; }
        public FileAttributes Attributes { get; set; }

        public void Dispose()
        {
            Contents.Dispose();
        }
    }

    private class NonClosingStreamWrapper(Stream baseStream, Action onDispose) : Stream
    {
        public override bool CanRead => baseStream.CanRead;
        public override bool CanSeek => baseStream.CanSeek;
        public override bool CanWrite => baseStream.CanWrite;
        public override long Length => baseStream.Length;

        public override long Position
        {
            get => baseStream.Position;
            set => baseStream.Position = value;
        }

        public override void Flush() => baseStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => baseStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);
        public override void SetLength(long value) => baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => baseStream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                onDispose.Invoke();
            }
            base.Dispose(disposing);
        }
    }
}
