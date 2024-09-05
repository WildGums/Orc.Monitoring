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
        _overrideFilePath = Path.Combine(outputDirectory, "method_overrides.csv");
        _overrideTemplateFilePath = Path.Combine(outputDirectory, "method_overrides.template");
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

        var overrides = _csvUtils.ReadCsv(_overrideFilePath);
        if (!overrides.Any())
        {
            _overrides.Clear();
            return;
        }

        _overrides.Clear();
        foreach (var row in overrides)
        {
            var fullName = row["FullName"];
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
    }

    public void SaveOverrides(ICollection<ReportItem> reportItems)
    {
        var headers = new[] { "FullName", "IsStatic", "IsExtension" };

        using var writer = _fileSystem.CreateStreamWriter(_overrideTemplateFilePath, false, System.Text.Encoding.UTF8);
        _csvUtils.WriteCsvLine(writer, headers);

        foreach (var item in reportItems.Where(i => i.MethodName != MethodCallParameter.Types.Gap))
        {
            var fullName = item.FullName ?? string.Empty;
            var isStatic = item.Parameters.TryGetValue("IsStatic", out var staticValue) ? staticValue : string.Empty;
            var isExtension = item.Parameters.TryGetValue("IsExtension", out var extensionValue) ? extensionValue : string.Empty;

            _csvUtils.WriteCsvLine(writer, new[] { fullName, isStatic, isExtension });
        }

        _logger.LogInformation($"Saved method override template to {_overrideTemplateFilePath}");
    }

    public Dictionary<string, string> GetOverridesForMethod(string fullName)
    {
        return _overrides.TryGetValue(fullName, out var methodOverrides) ? methodOverrides : new Dictionary<string, string>();
    }
}
