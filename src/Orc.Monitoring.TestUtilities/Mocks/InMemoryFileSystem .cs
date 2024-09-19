namespace Orc.Monitoring.TestUtilities.Mocks;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IO;

public class InMemoryFileSystem : IFileSystem, IDisposable
{
    private readonly ILogger<InMemoryFileSystem> _logger;
    private readonly ConcurrentDictionary<string, InMemoryFile> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FileAttributes> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<FileAccessMode>> _openFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _fileLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _directoryLocks = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    public InMemoryFileSystem(IMonitoringLoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<InMemoryFileSystem>();
        // Root directory
        _directories.TryAdd("/", FileAttributes.Directory);
    }

    private object GetFileLock(string normalizedPath)
    {
        return _fileLocks.GetOrAdd(normalizedPath, _ => new object());
    }

    private object GetDirectoryLock(string normalizedPath)
    {
        return _directoryLocks.GetOrAdd(normalizedPath, _ => new object());
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

        EnsureDirectoryExists(directoryPath);

        var fileLock = GetFileLock(normalizedPath);
        lock (fileLock)
        {
            // Check for conflicting access
            if (_openFiles.TryGetValue(normalizedPath, out var accessModes))
            {
                foreach (var accessMode in accessModes)
                {
                    if ((accessMode.Share & FileShare.Write) == 0)
                    {
                        throw new IOException("The process cannot access the file because it is being used by another process.");
                    }
                }
            }

            // Check if the file is read-only
            if (_files.TryGetValue(normalizedPath, out var existingFile) &&
                (existingFile.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                _logger.LogError("Access to the path is denied (file is read-only): {Path}", path);
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }

            // Check if the directory is read-only
            if (_directories.TryGetValue(directoryPath, out var dirAttributes) &&
                (dirAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                _logger.LogError("Access to the path is denied (directory is read-only): {Path}", path);
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }

            try
            {
                // Use UTF8Encoding with emitUTF8Identifier set to false to prevent BOM
                var ms = new MemoryStream();
                using (var writer = new StreamWriter(ms, new UTF8Encoding(false), bufferSize: -1, leaveOpen: true))
                {
                    writer.Write(contents);
                    writer.Flush();
                }
                ms.Position = 0;

                var newFile = new InMemoryFile
                {
                    Contents = ms,
                    Attributes = FileAttributes.Normal
                };

                SetFileContent(normalizedPath, newFile);

                _logger.LogInformation("Successfully wrote text to file at path: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write text to file at path: {Path}", path);
                throw;
            }
        }
    }

    public string ReadAllText(string path)
    {
        var normalizedPath = NormalizePath(path);

        _logger.LogInformation("Reading text from file at path: {Path}", path);

        if (!_files.TryGetValue(normalizedPath, out var file))
        {
            _logger.LogError("File not found: {Path}", path);
            throw new FileNotFoundException("File not found", path);
        }

        var fileLock = GetFileLock(normalizedPath);
        lock (fileLock)
        {
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
        var directoryPath = NormalizePath(Path.GetDirectoryName(normalizedPath));

        EnsureDirectoryExists(directoryPath);

        var fileLock = GetFileLock(normalizedPath);
        lock (fileLock)
        {
            if (!_files.TryGetValue(normalizedPath, out var existingFile))
            {
                WriteAllText(normalizedPath, contents);
            }
            else
            {
                // Ensure the MemoryStream is expandable
                if (!existingFile.Contents.CanWrite || existingFile.Contents.Capacity <= existingFile.Contents.Length)
                {
                    var newContents = new MemoryStream();
                    existingFile.Contents.Position = 0;
                    existingFile.Contents.CopyTo(newContents);
                    existingFile.Contents.Dispose();
                    existingFile.Contents = newContents;
                }

                existingFile.Contents.Position = existingFile.Contents.Length;
                using var writer = new StreamWriter(existingFile.Contents, new UTF8Encoding(false), 1024, true);
                writer.Write(contents);
                writer.Flush();
                _logger.LogInformation("Successfully appended text to file at path: {Path}", path);
            }
        }
    }

    public void CreateDirectory(string path)
    {
        var normalizedPath = NormalizePath(path);

        EnsureDirectoryExists(normalizedPath);
    }

    public bool DirectoryExists(string path)
    {
        return _directories.ContainsKey(NormalizePath(path));
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        var normalizedPath = NormalizePath(path);

        var directoryLock = GetDirectoryLock(normalizedPath);
        lock (directoryLock)
        {
            if (!_directories.ContainsKey(normalizedPath))
                throw new DirectoryNotFoundException("Directory not found: " + path);

            if (recursive)
            {
                var directoriesToDelete = _directories.Keys.Where(d => d.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)).ToList();
                var filesToDelete = _files.Keys.Where(f => f.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var filePath in filesToDelete)
                {
                    var fileLock = GetFileLock(filePath);
                    lock (fileLock)
                    {
                        if (_files.TryRemove(filePath, out var removedFile))
                        {
                            removedFile.Dispose();
                        }
                    }
                }

                foreach (var dirPath in directoriesToDelete)
                {
                    _directories.TryRemove(dirPath, out _);
                }
            }
            else
            {
                var hasSubDirectories = _directories.Keys.Any(d => IsDirectSubdirectory(normalizedPath, d));
                var hasFiles = _files.Keys.Any(f => IsDirectChild(normalizedPath, f));

                if (hasSubDirectories || hasFiles)
                    throw new IOException("The directory is not empty");

                _directories.TryRemove(normalizedPath, out _);
            }
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
        if (!_directories.ContainsKey(normalizedPath))
            throw new DirectoryNotFoundException("Directory not found: " + path);

        var searchPatternRegex = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        var files = _files.Keys.Where(f =>
        {
            var fileDir = NormalizePath(Path.GetDirectoryName(f));
            var isInDirectory = (searchOption == SearchOption.AllDirectories && fileDir.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)) ||
                                (searchOption == SearchOption.TopDirectoryOnly && fileDir.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            return isInDirectory && searchPatternRegex.IsMatch(Path.GetFileName(f));
        });

        return files.ToArray();
    }

    public void SetAttributes(string path, FileAttributes fileAttributes)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.ContainsKey(normalizedPath))
        {
            _files[normalizedPath].Attributes = fileAttributes;
        }
        else if (_directories.ContainsKey(normalizedPath))
        {
            _directories[normalizedPath] = fileAttributes | FileAttributes.Directory;
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
        else if (_directories.TryGetValue(normalizedPath, out var dirAttributes))
        {
            return dirAttributes;
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
        var fileStream = CreateFileStream(normalizedPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        return new StreamWriter(fileStream, encoding);
    }

    public StreamReader CreateStreamReader(string fullPath)
    {
        var normalizedPath = NormalizePath(fullPath);
        if (!_files.ContainsKey(normalizedPath))
            throw new FileNotFoundException("File not found", fullPath);

#pragma warning disable IDISP001
        var fileStream = CreateFileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
#pragma warning restore IDISP001
        return new StreamReader(fileStream, Encoding.UTF8, true, 1024, true);
    }

    public async Task<string> ReadAllTextAsync(string fullPath)
    {
        var normalizedPath = NormalizePath(fullPath);

        if (!_files.TryGetValue(normalizedPath, out var file))
            throw new FileNotFoundException("File not found", fullPath);

        var fileLock = GetFileLock(normalizedPath);
        lock (fileLock)
        {
            file.Contents.Position = 0;
            using var reader = new StreamReader(file.Contents, Encoding.UTF8, true, 1024, true);
#pragma warning disable CL0001
            return reader.ReadToEnd();
#pragma warning restore CL0001
        }
    }

    public string[] GetDirectories(string sourcePath)
    {
        var normalizedPath = NormalizePath(sourcePath);

        var directories = _directories.Keys.Where(d =>
            IsDirectSubdirectory(normalizedPath, d)).ToArray();

        return directories;
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

        EnsureDirectoryExists(directoryPath);

        var fileLock = GetFileLock(normalizedPath);
        lock (fileLock)
        {
            if (_directories.TryGetValue(directoryPath, out var dirAttributes) &&
                (dirAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                _logger.LogError("Access to the path is denied (directory is read-only): {Path}", sourcePath);
                throw new UnauthorizedAccessException("Access to the path is denied.");
            }

            // Check for conflicting access
            if (_openFiles.TryGetValue(normalizedPath, out var accessModes))
            {
                foreach (var accessMode in accessModes)
                {
                    if ((accessMode.Share & FileShare.Write) == 0 && (fileAccess & FileAccess.Write) != 0)
                    {
                        throw new IOException("The process cannot access the file because it is being used by another process.");
                    }
                    if ((accessMode.Share & FileShare.Read) == 0 && (fileAccess & FileAccess.Read) != 0)
                    {
                        throw new IOException("The process cannot access the file because it is being used by another process.");
                    }
                }
            }

            // Update _openFiles
            if (!_openFiles.ContainsKey(normalizedPath))
            {
                _openFiles[normalizedPath] = new List<FileAccessMode>();
            }
            _openFiles[normalizedPath].Add(new FileAccessMode { Access = fileAccess, Share = fileShare });


#pragma warning disable IDISP001
            if (_files.TryGetValue(normalizedPath, out var existingFile))
#pragma warning restore IDISP001
            {
                // Ensure the MemoryStream is expandable
                if (!existingFile.Contents.CanWrite || existingFile.Contents.Capacity <= existingFile.Contents.Length)
                {
                    var newContents = new MemoryStream();
                    existingFile.Contents.Position = 0;
                    existingFile.Contents.CopyTo(newContents);
                    existingFile.Contents.Dispose();
                    existingFile.Contents = newContents;
                }

                var existingFileStream = existingFile.Contents;

                if (fileMode == FileMode.Append)
                {
                    existingFileStream.Position = existingFileStream.Length;
                }
                else
                {
                    existingFileStream.Position = 0;
                }

                // Return the stream wrapped in a NonClosingStreamWrapper
                return new NonClosingStreamWrapper(existingFileStream, () => { /* No action needed on dispose */ });
            }
            else
            {
                if (fileMode == FileMode.Open || fileMode == FileMode.Truncate)
                {
                    _logger.LogError("File not found: {Path}", sourcePath);
                    throw new FileNotFoundException("File not found", sourcePath);
                }

#pragma warning disable IDISP001
                existingFile = new InMemoryFile
                {
                    Contents = new MemoryStream(),
                    Attributes = FileAttributes.Normal
                };
#pragma warning restore IDISP001
                SetFileContent(normalizedPath, existingFile);
            }

            var stream = existingFile.Contents;

            if (fileMode == FileMode.Append)
            {
                stream.Position = stream.Length;
            }
            else
            {
                stream.Position = 0;
            }

            _logger.LogInformation("File stream created successfully for path: {Path}", sourcePath);

            return new NonClosingStreamWrapper(stream, () =>
            {
                // On stream dispose, remove from _openFiles
                lock (fileLock)
                {
                    _openFiles[normalizedPath].RemoveAll(f => f.Access == fileAccess && f.Share == fileShare);
                    if (_openFiles[normalizedPath].Count == 0)
                    {
                        _openFiles.TryRemove(normalizedPath, out _);
                    }
                }
            });
        }
    }

    public void DeleteFile(string path)
    {
        var normalizedPath = NormalizePath(path);

        var fileLock = GetFileLock(normalizedPath);
        lock (fileLock)
        {
            if (_files.TryRemove(normalizedPath, out var file))
            {
                file.Dispose();
            }
            else
            {
                throw new FileNotFoundException("File not found", path);
            }
        }
    }

    public async Task<string[]> ReadAllLinesAsync(string path)
    {
        var contents = await ReadAllTextAsync(path);
        return contents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
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

        var sourceFileLock = GetFileLock(sourcePath);
        var destFileLock = GetFileLock(destPath);

        lock (sourceFileLock)
        {
            lock (destFileLock)
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

                SetFileContent(destPath, destFile);
            }
        }
    }

    public void MoveFile(string sourceFileName, string destFileName)
    {
        var sourcePath = NormalizePath(sourceFileName);
        var destPath = NormalizePath(destFileName);

        var sourceFileLock = GetFileLock(sourcePath);
        var destFileLock = GetFileLock(destPath);

        lock (sourceFileLock)
        {
            lock (destFileLock)
            {
                if (!_files.TryRemove(sourcePath, out var sourceFile))
                    throw new FileNotFoundException("File not found", sourceFileName);

                if (_files.ContainsKey(destPath))
                    throw new IOException("File already exists");

                SetFileContent(destPath, sourceFile);
            }
        }
    }

    public string? GetDirectoryName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var normalizedPath = NormalizePath(path);
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');

        if (lastSeparatorIndex <= 0)
            return null;

        var directoryName = normalizedPath.Substring(0, lastSeparatorIndex);

        return directoryName;
    }

    public string GetFileNameWithoutExtension(string path)
    {
        var fileName = GetFileName(path);

        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        var lastDotIndex = fileName.LastIndexOf('.');

        if (lastDotIndex < 0)
            return fileName; // No extension found

        return fileName.Substring(0, lastDotIndex);
    }

    public string GetExtension(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var fileName = GetFileName(path);
        var lastDotIndex = fileName.LastIndexOf('.');

        if (lastDotIndex < 0)
            return string.Empty;

        return fileName.Substring(lastDotIndex);
    }

    public string Combine(string path1, string path2)
    {
        if (string.IsNullOrEmpty(path1))
            return NormalizePath(path2);

        if (string.IsNullOrEmpty(path2))
            return NormalizePath(path1);

        var combinedPath = path1.TrimEnd('/') + '/' + path2.TrimStart('/');
        return NormalizePath(combinedPath);
    }

    public string GetFileName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        // Replace backslashes with forward slashes
        var pathToUse = path.Replace('\\', '/');

        // If path ends with a slash, it's a directory, so return an empty string
        if (pathToUse.EndsWith("/"))
            return string.Empty;

        var lastSeparatorIndex = pathToUse.LastIndexOf('/');

        if (lastSeparatorIndex < 0)
            return pathToUse; // No slashes, return the entire path

        return pathToUse.Substring(lastSeparatorIndex + 1);
    }

    public string GetRelativePath(string relativeTo, string path)
    {
        if (string.IsNullOrEmpty(relativeTo))
            throw new ArgumentNullException(nameof(relativeTo));

        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        var normalizedRelativeTo = NormalizePath(relativeTo).TrimEnd('/');
        var normalizedPath = NormalizePath(path);

        if (!normalizedPath.StartsWith(normalizedRelativeTo + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath;
        }

        var relativePath = normalizedPath.Substring(normalizedRelativeTo.Length);

        if (relativePath.StartsWith("/"))
        {
            relativePath = relativePath.Substring(1);
        }

        return relativePath;
    }

    public string[] ReadAllLines(string path)
    {
        var contents = ReadAllText(path);
        return contents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
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
            if (string.IsNullOrEmpty(part))
                continue;

            currentPath += "/" + part;
            var dirPath = NormalizePath(currentPath);
            _directories.TryAdd(dirPath, FileAttributes.Directory);
        }
    }

    private bool IsDirectSubdirectory(string parentDir, string subDir)
    {
        if (parentDir.Equals(subDir, StringComparison.OrdinalIgnoreCase))
            return false;

        var parentSegments = parentDir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var subDirSegments = subDir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        return subDirSegments.Length == parentSegments.Length + 1 &&
               subDir.StartsWith(parentDir, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDirectChild(string parentDir, string childPath)
    {
        var childDir = NormalizePath(Path.GetDirectoryName(childPath));
        return childDir.Equals(parentDir, StringComparison.OrdinalIgnoreCase);
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
            _fileLocks.Clear();
            _directoryLocks.Clear();
        }

        _disposed = true;
    }

    private void SetFileContent(string path, InMemoryFile content)
    {
        _files[path] = content;
    }

    private sealed class InMemoryFile : IDisposable
    {
        public MemoryStream Contents { get; set; }
        public FileAttributes Attributes { get; set; }

        public void Dispose()
        {
#pragma warning disable IDISP007
            Contents.Dispose();
#pragma warning restore IDISP007
        }
    }

    private class NonClosingStreamWrapper : Stream
    {
        private readonly Stream _baseStream;
        private readonly Action _onDispose;

        public NonClosingStreamWrapper(Stream baseStream, Action onDispose)
        {
            _baseStream = baseStream;
            _onDispose = onDispose;
        }

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

        public override void SetLength(long value) => _baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

#pragma warning disable IDISP010
        protected override void Dispose(bool disposing)
#pragma warning restore IDISP010
        {
            if (disposing)
            {
                _onDispose?.Invoke();
            }
            // Do not dispose the base stream
            // base.Dispose(disposing);
        }
    }

    private class FileAccessMode
    {
        public FileAccess Access { get; set; }
        public FileShare Share { get; set; }
    }
}
