namespace Orc.Monitoring.Reporters.ReportOutputs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO;
using Microsoft.Extensions.Logging;

public class MethodOverrideManager
{
    private readonly IFileSystem _fileSystem;
    private readonly CsvUtils _csvUtils;
    private readonly string _overrideFilePath;
    private readonly string _overrideTemplateFilePath;
    private readonly Dictionary<string, Dictionary<string, string>> _overrides;
    private readonly ILogger<MethodOverrideManager> _logger;

    public MethodOverrideManager(string outputDirectory, IMonitoringLoggerFactory loggerFactory, IFileSystem fileSystem, CsvUtils csvUtils)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(csvUtils);

        _fileSystem = fileSystem;
        _csvUtils = csvUtils;
        _overrideFilePath = _fileSystem.Combine(outputDirectory, "method_overrides.csv");
        _overrideTemplateFilePath = _fileSystem.Combine(outputDirectory, "method_overrides.template");
        _overrides = new Dictionary<string, Dictionary<string, string>>();
        _logger = loggerFactory.CreateLogger<MethodOverrideManager>();
    }

    public void ReadOverrides()
    {
        if (!_fileSystem.FileExists(_overrideFilePath))
        {
            _logger.LogInformation($"Override file not found: {_overrideFilePath}");
            return;
        }

        var content = _fileSystem.ReadAllText(_overrideFilePath);
        _logger.LogInformation($"Override file content:\n{content}");

        var overrides = _csvUtils.ReadCsv(_overrideFilePath);
        if (!overrides.Any())
        {
            _overrides.Clear();
            return;
        }

        _overrides.Clear();
        foreach (var row in overrides)
        {
            if (!row.TryGetValue("FullName", out var fullName))
            {
                _logger.LogWarning("Row in override file is missing FullName column");
                continue;
            }

            var methodOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in row.Keys.Where(h => h != "FullName"))
            {
                if (!string.IsNullOrEmpty(row[header]))
                {
                    methodOverrides[header] = row[header];
                }
            }
            if (methodOverrides.Count > 0)
            {
                _overrides[fullName] = methodOverrides;
            }
        }

        _logger.LogInformation($"Loaded {_overrides.Count} method overrides from {_overrideFilePath}");
        foreach (var kvp in _overrides)
        {
            _logger.LogInformation($"Override for {kvp.Key}: {string.Join(", ", kvp.Value.Select(x => $"{x.Key}={x.Value}"))}");
        }
    }

    public void SaveOverrides(ICollection<ReportItem> reportItems)
    {
        var headers = new HashSet<string>();

        foreach (var reportItem in reportItems)
        {
            foreach (var parameter in reportItem.Parameters)
            {
                headers.Add(parameter.Key);
            }
        }

        var sortedHeader = headers.OrderBy(h => h).ToList();
        sortedHeader.Insert(0, "FullName");

        using var writer = _fileSystem.CreateStreamWriter(_overrideTemplateFilePath, false, System.Text.Encoding.UTF8);
        _csvUtils.WriteCsvLine(writer, sortedHeader.ToArray());

        var savedFullNames = new HashSet<string>(); 

        foreach (var item in reportItems.Where(i => i.MethodName != MethodCallParameter.Types.Gap))
        {
            var fullName = item.FullName;
            if (string.IsNullOrEmpty(fullName) || !savedFullNames.Add(fullName))
            {
                continue;
            }

            var values = new string[sortedHeader.Count];
            values[0] = fullName;

            for (var i = 1; i < sortedHeader.Count; i++)
            {
                var header = sortedHeader[i];
                var value = item.Parameters.TryGetValue(header, out var parameterValue) ? parameterValue : string.Empty;
                values[i] = value;
            }

            _csvUtils.WriteCsvLine(writer, values);
        }

        _logger.LogInformation($"Saved method override template to {_overrideTemplateFilePath}");
    }

    public Dictionary<string, string> GetOverridesForMethod(string fullName)
    {
        if (_overrides.TryGetValue(fullName, out var methodOverrides) ||
            _overrides.TryGetValue(fullName.ToLowerInvariant(), out methodOverrides))
        {
            return methodOverrides;
        }

        return new Dictionary<string, string>();
    }
}
