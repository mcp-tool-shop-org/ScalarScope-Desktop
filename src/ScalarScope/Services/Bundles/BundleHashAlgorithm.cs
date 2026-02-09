// ComparisonBundle v1.0.0 Hash Algorithm
// Implements deterministic bundleHash computation per spec.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScalarScope.Services.Bundles;

/// <summary>
/// Implements the v1.0.0 bundleHash algorithm.
/// 
/// Algorithm (pseudocode):
/// 1. Compute per-file hashes for all entries except manifest.json
/// 2. Sort fileEntries by path (lexicographic, ordinal)
/// 3. Build manifestCore without integrity block
/// 4. Canonicalize manifestCore JSON → manifestCoreHash
/// 5. bundleHash = SHA256(manifestCoreHash + concat(sorted(fileSha256)))
/// 6. Populate integrity block with bundleHash
/// </summary>
public static class BundleHashAlgorithm
{
    /// <summary>Bundle hash definition string (stored in manifest).</summary>
    public const string BundleHashDefinition = 
        "sha256(manifestCoreHash + concat(sorted(fileSha256)))";
    
    /// <summary>
    /// Compute bundleHash from manifest core and file entries.
    /// </summary>
    public static string ComputeBundleHash(
        ComparisonBundleManifest manifestCore,
        IEnumerable<FileIntegrityEntry> fileEntries)
    {
        // 1. Canonicalize manifest (without integrity block - we receive it that way)
        var manifestCoreJson = CanonicalJson(manifestCore);
        var manifestCoreHash = ComputeSha256Hex(Encoding.UTF8.GetBytes(manifestCoreJson));
        
        // 2. Sort file entries by path
        var sortedHashes = fileEntries
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Sha256);
        
        // 3. Concatenate manifestCoreHash + all file hashes
        var accumulator = manifestCoreHash + string.Concat(sortedHashes);
        
        // 4. Final hash
        return ComputeSha256Hex(Encoding.UTF8.GetBytes(accumulator));
    }
    
    /// <summary>
    /// Compute SHA-256 hash of bytes, return lowercase hex string.
    /// </summary>
    public static string ComputeSha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>
    /// Compute SHA-256 hash of UTF-8 string, return lowercase hex string.
    /// </summary>
    public static string ComputeSha256Hex(string content)
    {
        return ComputeSha256Hex(Encoding.UTF8.GetBytes(content));
    }
    
    /// <summary>
    /// Compute SHA-256 hash of a file stream.
    /// </summary>
    public static async Task<string> ComputeFileSha256Async(Stream stream)
    {
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>
    /// Serialize object to canonical JSON (minified, sorted keys, invariant culture).
    /// </summary>
    public static string CanonicalJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, GetCanonicalOptions());
    }
    
    /// <summary>
    /// Serialize object to canonical JSON bytes (UTF-8).
    /// </summary>
    public static byte[] CanonicalJsonBytes<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, GetCanonicalOptions());
    }
    
    /// <summary>
    /// Get canonical JSON serializer options.
    /// - camelCase property naming
    /// - sorted keys
    /// - no indentation (minified)
    /// - invariant culture numbers
    /// - null omitted when allowed by schema
    /// </summary>
    public static JsonSerializerOptions GetCanonicalOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = 
            { 
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new SortedDictionaryConverter()
            },
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
    }
    
    /// <summary>
    /// Get human-readable JSON serializer options (indented).
    /// </summary>
    public static JsonSerializerOptions GetReadableOptions()
    {
        var options = GetCanonicalOptions();
        options.WriteIndented = true;
        return options;
    }
    
    /// <summary>
    /// Build FileIntegrityEntry from content.
    /// </summary>
    public static FileIntegrityEntry CreateFileEntry(string path, byte[] content, string contentType)
    {
        return new FileIntegrityEntry
        {
            Path = path,
            Sha256 = ComputeSha256Hex(content),
            Bytes = content.Length,
            ContentType = contentType
        };
    }
    
    /// <summary>
    /// Build FileIntegrityEntry from UTF-8 string content.
    /// </summary>
    public static FileIntegrityEntry CreateFileEntry(string path, string content, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return CreateFileEntry(path, bytes, contentType);
    }
    
    /// <summary>
    /// Guess content type from file extension.
    /// </summary>
    public static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// JSON converter that ensures dictionary keys are sorted for canonical output.
/// </summary>
public class SortedDictionaryConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType) return false;
        var generic = typeToConvert.GetGenericTypeDefinition();
        return generic == typeof(Dictionary<,>) || 
               generic == typeof(IDictionary<,>) ||
               generic == typeof(IReadOnlyDictionary<,>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var keyType = typeToConvert.GetGenericArguments()[0];
        var valueType = typeToConvert.GetGenericArguments()[1];
        
        var converterType = typeof(SortedDictionaryConverterInner<,>).MakeGenericType(keyType, valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
    
    private class SortedDictionaryConverterInner<TKey, TValue> : JsonConverter<IDictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override IDictionary<TKey, TValue>? Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options);
        }

        public override void Write(
            Utf8JsonWriter writer, IDictionary<TKey, TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            
            // Sort keys for deterministic output
            var sortedKeys = value.Keys.OrderBy(k => k?.ToString(), StringComparer.Ordinal);
            
            foreach (var key in sortedKeys)
            {
                var keyString = key?.ToString() ?? "";
                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(keyString) ?? keyString);
                JsonSerializer.Serialize(writer, value[key], options);
            }
            
            writer.WriteEndObject();
        }
    }
}
