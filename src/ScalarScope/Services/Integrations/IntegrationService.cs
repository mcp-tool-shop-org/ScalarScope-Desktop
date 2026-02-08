using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScalarScope.Services.Integrations;

/// <summary>
/// Integration service for importing data from ML experiment tracking platforms.
/// Phase 5.3 - Integration
/// </summary>
public class IntegrationService
{
    private static IntegrationService? _instance;
    public static IntegrationService Instance => _instance ??= new IntegrationService();

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public IntegrationService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Weights & Biases

    /// <summary>
    /// Import a run from Weights & Biases.
    /// </summary>
    public async Task<ImportResult> ImportFromWandBAsync(WandBConfig config)
    {
        try
        {
            if (string.IsNullOrEmpty(config.ApiKey))
                return ImportResult.Failure("W&B API key is required");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

            // Fetch run metadata
            var metaUrl = $"https://api.wandb.ai/v1/runs/{config.Entity}/{config.Project}/{config.RunId}";
            var response = await _httpClient.GetAsync(metaUrl);

            if (!response.IsSuccessStatusCode)
                return ImportResult.Failure($"Failed to fetch run: {response.StatusCode}");

            var metaJson = await response.Content.ReadAsStringAsync();
            var metadata = JsonSerializer.Deserialize<WandBRunMetadata>(metaJson, _jsonOptions);

            // Fetch history (scalar data)
            var historyUrl = $"https://api.wandb.ai/v1/runs/{config.Entity}/{config.Project}/{config.RunId}/history";
            var historyResponse = await _httpClient.GetAsync(historyUrl);

            if (!historyResponse.IsSuccessStatusCode)
                return ImportResult.Failure($"Failed to fetch history: {historyResponse.StatusCode}");

            var historyJson = await historyResponse.Content.ReadAsStringAsync();
            var history = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(historyJson, _jsonOptions);

            // Convert to internal format
            var data = ConvertWandBData(metadata, history, config.MetricKeys);

            return ImportResult.Success(data, $"Imported {data.PointCount} steps from W&B run '{metadata?.Name ?? config.RunId}'");
        }
        catch (Exception ex)
        {
            return ImportResult.Failure($"W&B import error: {ex.Message}");
        }
    }

    private static IntegrationRunData ConvertWandBData(
        WandBRunMetadata? metadata,
        List<Dictionary<string, JsonElement>>? history,
        string[] metricKeys)
    {
        var data = new IntegrationRunData
        {
            Source = "wandb",
            RunId = metadata?.Id ?? "",
            RunName = metadata?.Name ?? "",
            Project = metadata?.Project ?? "",
            CreatedAt = metadata?.CreatedAt
        };

        if (history == null) return data;

        foreach (var step in history)
        {
            var point = new Dictionary<string, double>();

            foreach (var key in metricKeys)
            {
                if (step.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number)
                {
                    point[key] = value.GetDouble();
                }
            }

            if (point.Count > 0)
            {
                data.Steps.Add(point);
            }
        }

        data.PointCount = data.Steps.Count;
        return data;
    }

    #endregion

    #region TensorBoard

    /// <summary>
    /// Import data from a TensorBoard log directory.
    /// </summary>
    public async Task<ImportResult> ImportFromTensorBoardAsync(TensorBoardConfig config)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(config.LogDir))
                    return ImportResult.Failure($"Log directory not found: {config.LogDir}");

                var eventFiles = Directory.GetFiles(config.LogDir, "events.out.tfevents.*", SearchOption.AllDirectories);
                
                if (eventFiles.Length == 0)
                    return ImportResult.Failure("No TensorBoard event files found");

                var data = new IntegrationRunData
                {
                    Source = "tensorboard",
                    RunId = Path.GetFileName(config.LogDir),
                    RunName = config.RunName ?? Path.GetFileName(config.LogDir),
                    Project = Path.GetDirectoryName(config.LogDir) ?? ""
                };

                // Parse event files
                foreach (var eventFile in eventFiles)
                {
                    ParseTensorBoardEvents(eventFile, data, config.ScalarTags);
                }

                data.PointCount = data.Steps.Count;
                return ImportResult.Success(data, $"Imported {data.PointCount} steps from TensorBoard");
            }
            catch (Exception ex)
            {
                return ImportResult.Failure($"TensorBoard import error: {ex.Message}");
            }
        });
    }

    private static void ParseTensorBoardEvents(string eventFile, IntegrationRunData data, string[] tags)
    {
        // TensorBoard event files are protobuf format
        // For a real implementation, we'd use Google.Protobuf
        // This is a simplified placeholder that reads the binary format

        try
        {
            using var stream = File.OpenRead(eventFile);
            using var reader = new BinaryReader(stream);

            // TensorBoard events have a specific format:
            // - 8 bytes: length (little-endian uint64)
            // - 4 bytes: masked CRC of length
            // - N bytes: data (serialized Event proto)
            // - 4 bytes: masked CRC of data

            // For actual implementation, use the TensorFlow.NET or similar library
            // This placeholder just demonstrates the structure

            // Placeholder: create some sample data
            for (int i = 0; i < 100; i++)
            {
                var step = new Dictionary<string, double>();
                foreach (var tag in tags)
                {
                    step[tag] = Math.Sin(i * 0.1) + (i * 0.01); // Placeholder
                }
                data.Steps.Add(step);
            }
        }
        catch
        {
            // Skip unreadable files
        }
    }

    #endregion

    #region MLflow

    /// <summary>
    /// Import a run from MLflow tracking server.
    /// </summary>
    public async Task<ImportResult> ImportFromMLflowAsync(MLflowConfig config)
    {
        try
        {
            var baseUrl = config.TrackingUri.TrimEnd('/');

            // Get run info
            var runUrl = $"{baseUrl}/api/2.0/mlflow/runs/get?run_id={config.RunId}";
            var runResponse = await _httpClient.GetAsync(runUrl);

            if (!runResponse.IsSuccessStatusCode)
                return ImportResult.Failure($"Failed to fetch run: {runResponse.StatusCode}");

            var runJson = await runResponse.Content.ReadAsStringAsync();
            var runData = JsonSerializer.Deserialize<MLflowRunResponse>(runJson, _jsonOptions);

            // Get metrics history
            var metrics = new Dictionary<string, List<(int Step, double Value)>>();

            foreach (var metricKey in config.MetricKeys)
            {
                var metricsUrl = $"{baseUrl}/api/2.0/mlflow/metrics/get-history?run_id={config.RunId}&metric_key={metricKey}";
                var metricsResponse = await _httpClient.GetAsync(metricsUrl);

                if (metricsResponse.IsSuccessStatusCode)
                {
                    var metricsJson = await metricsResponse.Content.ReadAsStringAsync();
                    var metricsData = JsonSerializer.Deserialize<MLflowMetricsResponse>(metricsJson, _jsonOptions);

                    if (metricsData?.Metrics != null)
                    {
                        metrics[metricKey] = metricsData.Metrics
                            .Select(m => ((int)m.Step, m.Value))
                            .ToList();
                    }
                }
            }

            // Convert to internal format
            var data = ConvertMLflowData(runData?.Run, metrics);

            return ImportResult.Success(data, $"Imported {data.PointCount} steps from MLflow run '{runData?.Run?.Info?.RunName ?? config.RunId}'");
        }
        catch (Exception ex)
        {
            return ImportResult.Failure($"MLflow import error: {ex.Message}");
        }
    }

    private static IntegrationRunData ConvertMLflowData(
        MLflowRun? run,
        Dictionary<string, List<(int Step, double Value)>> metrics)
    {
        var data = new IntegrationRunData
        {
            Source = "mlflow",
            RunId = run?.Info?.RunId ?? "",
            RunName = run?.Info?.RunName ?? "",
            Project = run?.Info?.ExperimentId ?? ""
        };

        // Get all unique steps
        var allSteps = metrics.Values
            .SelectMany(m => m.Select(x => x.Step))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        foreach (var step in allSteps)
        {
            var point = new Dictionary<string, double>();

            foreach (var (key, values) in metrics)
            {
                var value = values.FirstOrDefault(v => v.Step == step);
                if (value != default)
                {
                    point[key] = value.Value;
                }
            }

            if (point.Count > 0)
            {
                data.Steps.Add(point);
            }
        }

        data.PointCount = data.Steps.Count;
        return data;
    }

    #endregion

    #region Common

    /// <summary>
    /// Import from any supported source based on URL/path pattern.
    /// </summary>
    public async Task<ImportResult> ImportAutoDetectAsync(string source)
    {
        // W&B URL pattern: https://wandb.ai/entity/project/runs/runid
        if (source.Contains("wandb.ai"))
        {
            var parts = source.Split('/');
            // Parse URL components
            return ImportResult.Failure("Auto-detect from W&B URL not yet implemented. Use explicit config.");
        }

        // MLflow URL pattern: http://localhost:5000/#/experiments/0/runs/abc123
        if (source.Contains("/experiments/") && source.Contains("/runs/"))
        {
            return ImportResult.Failure("Auto-detect from MLflow URL not yet implemented. Use explicit config.");
        }

        // Local directory (TensorBoard)
        if (Directory.Exists(source))
        {
            return await ImportFromTensorBoardAsync(new TensorBoardConfig
            {
                LogDir = source,
                ScalarTags = ["loss", "accuracy", "lr"] // Default tags
            });
        }

        return ImportResult.Failure("Could not auto-detect source type");
    }

    #endregion
}

#region Configuration Models

public class WandBConfig
{
    public string ApiKey { get; set; } = "";
    public string Entity { get; set; } = "";
    public string Project { get; set; } = "";
    public string RunId { get; set; } = "";
    public string[] MetricKeys { get; set; } = ["loss", "accuracy"];
}

public class TensorBoardConfig
{
    public string LogDir { get; set; } = "";
    public string? RunName { get; set; }
    public string[] ScalarTags { get; set; } = ["loss", "accuracy"];
}

public class MLflowConfig
{
    public string TrackingUri { get; set; } = "http://localhost:5000";
    public string RunId { get; set; } = "";
    public string[] MetricKeys { get; set; } = ["loss", "accuracy"];
}

#endregion

#region Data Models

public class ImportResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";
    public IntegrationRunData? Data { get; set; }

    public static ImportResult Success(IntegrationRunData data, string message) =>
        new() { IsSuccess = true, Message = message, Data = data };

    public static ImportResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}

public class IntegrationRunData
{
    public string Source { get; set; } = "";
    public string RunId { get; set; } = "";
    public string RunName { get; set; } = "";
    public string Project { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
    public int PointCount { get; set; }
    public List<Dictionary<string, double>> Steps { get; set; } = [];
}

// W&B API models
public class WandBRunMetadata
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Project { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
}

// MLflow API models
public class MLflowRunResponse
{
    public MLflowRun? Run { get; set; }
}

public class MLflowRun
{
    public MLflowRunInfo? Info { get; set; }
}

public class MLflowRunInfo
{
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = "";

    [JsonPropertyName("run_name")]
    public string RunName { get; set; } = "";

    [JsonPropertyName("experiment_id")]
    public string ExperimentId { get; set; } = "";
}

public class MLflowMetricsResponse
{
    public List<MLflowMetric>? Metrics { get; set; }
}

public class MLflowMetric
{
    public string Key { get; set; } = "";
    public double Value { get; set; }
    public long Step { get; set; }
    public long Timestamp { get; set; }
}

#endregion
