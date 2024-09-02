namespace Orc.Monitoring.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orc.Monitoring.IO;

public class InMemoryFileSystem : IFileSystem, IDisposable
{
    private readonly Dictionary<string, InMemoryFile> _files = new Dictionary<string, InMemoryFile>();
    private readonly HashSet<string> _directories = new HashSet<string>();
    private readonly Dictionary<string, FileAttributes> _directoryAttributes = new Dictionary<string, FileAttributes>();

    private bool _disposed;

    public InMemoryFileSystem()
    {
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

        // Check if the directory is read-only
        if (_directoryAttributes.TryGetValue(directoryPath, out var dirAttributes) &&
            (dirAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
        {
            throw new UnauthorizedAccessException("Access to the path is denied.");
        }

        // Check if the file is read-only
        if (_files.TryGetValue(normalizedPath, out var existingFile) &&
            (existingFile.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
        {
            throw new UnauthorizedAccessException("Access to the path is denied.");
        }

        EnsureDirectoryExists(directoryPath);

        using (var stream = new MemoryStream())
        using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
        {
            writer.Write(contents);
            writer.Flush();
            stream.Position = 0;

            _files[normalizedPath] = new InMemoryFile
            {
                Contents = new MemoryStream(stream.ToArray()),
                Attributes = FileAttributes.Normal
            };
        }
    }

    public string ReadAllText(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (!_files.ContainsKey(normalizedPath))
            throw new FileNotFoundException("File not found", path);

        var file = _files[normalizedPath];
        file.Contents.Position = 0;
#pragma warning disable IDISP004
        using (var reader = new StreamReader(new NonClosingStreamWrapper(file.Contents), Encoding.UTF8, true, 1024, true))
#pragma warning restore IDISP004
        {
            return reader.ReadToEnd();
        }
    }

    public void AppendAllText(string path, string contents)
    {
        var normalizedPath = NormalizePath(path);
        var directoryPath = Path.GetDirectoryName(normalizedPath);

        EnsureDirectoryExists(directoryPath);

        if (!_files.ContainsKey(normalizedPath))
        {
            WriteAllText(normalizedPath, contents);
        }
        else
        {
            var existingFile = _files[normalizedPath];
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
                _files[file].Dispose();
                _files.Remove(file);
            }
        }
        else
        {
            if (_directories.Any(d => d.StartsWith(normalizedPath + "/")) || _files.Any(f => f.Key.StartsWith(normalizedPath + "/")))
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

    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
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
        if (_files.ContainsKey(normalizedPath))
        {
            _files[normalizedPath].Attributes = fileAttributes;
        }
        else if (_directories.Contains(normalizedPath))
        {
            // For directories, we'll just store the attributes in a new dictionary
            if (!_directoryAttributes.ContainsKey(normalizedPath))
            {
                _directoryAttributes[normalizedPath] = FileAttributes.Directory;
            }
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
        if (_files.ContainsKey(normalizedPath))
        {
            return _files[normalizedPath].Attributes;
        }
        else if (_directories.Contains(normalizedPath))
        {
            return FileAttributes.Directory;
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

#pragma warning disable IDISP001
        var fileStream = CreateFileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024, FileOptions.None);
#pragma warning restore IDISP001
        return new StreamReader(fileStream, Encoding.UTF8, true, 1024, true);
    }

    public async Task<string> ReadAllTextAsync(string fullPath)
    {
        var normalizedPath = NormalizePath(fullPath);
        if (!_files.ContainsKey(normalizedPath))
            throw new FileNotFoundException("File not found", fullPath);

#pragma warning disable IDISP004
        using (var reader = new StreamReader(new NonClosingStreamWrapper(_files[normalizedPath].Contents), Encoding.UTF8, true, 1024, true))
#pragma warning restore IDISP004
        {
            _files[normalizedPath].Contents.Position = 0;
            return await reader.ReadToEndAsync();
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

        // Check if the directory is read-only
        if (_directoryAttributes.TryGetValue(directoryPath, out var dirAttributes) &&
            (dirAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
        {
            throw new UnauthorizedAccessException("Access to the path is denied.");
        }

        // Check if the file is read-only
        if (_files.TryGetValue(normalizedPath, out var existingFile) &&
            (existingFile.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly &&
            (fileAccess & FileAccess.Write) == FileAccess.Write)
        {
            throw new UnauthorizedAccessException("Access to the path is denied.");
        }

        EnsureDirectoryExists(directoryPath);

#pragma warning disable IDISP001
        if (!_files.TryGetValue(normalizedPath, out var file))
#pragma warning restore IDISP001
        {
            if (fileMode == FileMode.Open)
            {
                throw new FileNotFoundException("File not found", sourcePath);
            }

#pragma warning disable IDISP001
            file = new InMemoryFile
            {
                Contents = new MemoryStream(),
                Attributes = FileAttributes.Normal
            };
#pragma warning restore IDISP001
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
        return new NonClosingStreamWrapper(stream);
    }

    public void DeleteFile(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.ContainsKey(normalizedPath))
        {
            _files.Remove(normalizedPath);
        }
        else
        {
            throw new FileNotFoundException("File not found", path);
        }
    }

    private string NormalizePath(string path)
    {
        return path.Replace("\\", "/").TrimEnd('/');
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
            if (!_directories.Contains(dirPath))
            {
                _directories.Add(dirPath);
            }
        }
    }

    private sealed class InMemoryFile : IDisposable
    {
        public MemoryStream Contents { get; set; }
        public FileAttributes Attributes { get; set; }

        public void Dispose()
        {
#pragma warning disable IDISP007
            Contents?.Dispose();
#pragma warning restore IDISP007
        }
    }

    private class NonClosingStreamWrapper : Stream
    {
        private readonly Stream _baseStream;

        public NonClosingStreamWrapper(Stream baseStream)
        {
            _baseStream = baseStream;
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

        protected override void Dispose(bool disposing)
        {
            // Do not dispose the base stream
            base.Dispose(disposing);
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
        }

        _disposed = true;
    }

    public async Task<string[]> ReadAllLinesAsync(string path)
    {
        var contents = await ReadAllTextAsync(path);
        return contents.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
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

        if (!_files.ContainsKey(sourcePath))
            throw new FileNotFoundException("File not found", sourceFileName);

        if (_files.ContainsKey(destPath) && !overwrite)
            throw new IOException("File already exists");

        var sourceFile = _files[sourcePath];
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

    public string[] ReadAllLines(string path)
    {
        var contents = ReadAllText(path);
        return contents.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
    }
}
