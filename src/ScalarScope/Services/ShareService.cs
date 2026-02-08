using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ScalarScope.Services;

/// <summary>
/// Service for sharing, exporting, and importing view configurations.
/// Phase 5.1 - Collaboration & Ecosystem
/// </summary>
public class ShareService
{
    private static ShareService? _instance;
    public static ShareService Instance => _instance ??= new ShareService();

    private readonly string _galleryPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ShareService()
    {
        _galleryPath = Path.Combine(FileSystem.AppDataDirectory, "gallery");
        Directory.CreateDirectory(_galleryPath);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    #region Share Links

    /// <summary>
    /// Generate a shareable link (encoded string) for the current view state.
    /// </summary>
    public string GenerateShareLink(ShareableViewState state)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        var compressed = Compress(json);
        var encoded = Convert.ToBase64String(compressed)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"scalarscope://view/{encoded}";
    }

    /// <summary>
    /// Parse a share link back into a view state.
    /// </summary>
    public ShareableViewState? ParseShareLink(string link)
    {
        try
        {
            if (!link.StartsWith("scalarscope://view/"))
                return null;

            var encoded = link["scalarscope://view/".Length..];
            
            // Restore Base64 padding
            encoded = encoded.Replace('-', '+').Replace('_', '/');
            switch (encoded.Length % 4)
            {
                case 2: encoded += "=="; break;
                case 3: encoded += "="; break;
            }

            var compressed = Convert.FromBase64String(encoded);
            var json = Decompress(compressed);
            return JsonSerializer.Deserialize<ShareableViewState>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Export view state to a shareable .scs file.
    /// </summary>
    public async Task ExportShareFileAsync(ShareableViewState state, string filePath)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Import view state from a .scs file.
    /// </summary>
    public async Task<ShareableViewState?> ImportShareFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<ShareableViewState>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Embedding

    /// <summary>
    /// Generate HTML embed code for a view.
    /// </summary>
    public string GenerateEmbedCode(ShareableViewState state, int width = 800, int height = 600)
    {
        var link = GenerateShareLink(state);
        return $"""
            <!-- ScalarScope Embedded View -->
            <div style="width:{width}px;height:{height}px;border:1px solid #2a2a4e;background:#0f0f1a;border-radius:8px;display:flex;align-items:center;justify-content:center;">
                <div style="text-align:center;color:#888;">
                    <p style="font-size:14px;margin:0;">ScalarScope View</p>
                    <p style="font-size:12px;color:#00d9ff;">{state.Title ?? "Untitled"}</p>
                    <p style="font-size:10px;color:#666;margin-top:10px;">
                        <a href="{link}" style="color:#00d9ff;">Open in ScalarScope</a>
                    </p>
                </div>
            </div>
            """;
    }

    #endregion

    #region Gallery

    /// <summary>
    /// Save a snapshot to the local gallery.
    /// </summary>
    public async Task<GalleryEntry> SaveToGalleryAsync(ShareableViewState state, byte[]? thumbnail = null)
    {
        var id = GenerateId();
        var entry = new GalleryEntry
        {
            Id = id,
            Title = state.Title ?? "Untitled Snapshot",
            Description = state.Description,
            CreatedAt = DateTime.UtcNow,
            State = state,
            Tags = state.Tags ?? []
        };

        // Save state
        var statePath = Path.Combine(_galleryPath, $"{id}.json");
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(entry, _jsonOptions));

        // Save thumbnail if provided
        if (thumbnail != null)
        {
            var thumbPath = Path.Combine(_galleryPath, $"{id}.png");
            await File.WriteAllBytesAsync(thumbPath, thumbnail);
        }

        return entry;
    }

    /// <summary>
    /// Get all gallery entries.
    /// </summary>
    public async Task<IReadOnlyList<GalleryEntry>> GetGalleryAsync()
    {
        var entries = new List<GalleryEntry>();
        
        foreach (var file in Directory.GetFiles(_galleryPath, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var entry = JsonSerializer.Deserialize<GalleryEntry>(json, _jsonOptions);
                if (entry != null)
                    entries.Add(entry);
            }
            catch
            {
                // Skip corrupt entries
            }
        }

        return entries.OrderByDescending(e => e.CreatedAt).ToList();
    }

    /// <summary>
    /// Delete a gallery entry.
    /// </summary>
    public void DeleteFromGallery(string id)
    {
        var statePath = Path.Combine(_galleryPath, $"{id}.json");
        var thumbPath = Path.Combine(_galleryPath, $"{id}.png");

        if (File.Exists(statePath)) File.Delete(statePath);
        if (File.Exists(thumbPath)) File.Delete(thumbPath);
    }

    /// <summary>
    /// Get thumbnail for a gallery entry.
    /// </summary>
    public byte[]? GetThumbnail(string id)
    {
        var thumbPath = Path.Combine(_galleryPath, $"{id}.png");
        return File.Exists(thumbPath) ? File.ReadAllBytes(thumbPath) : null;
    }

    #endregion

    #region Comments

    /// <summary>
    /// Add a comment to a point annotation.
    /// </summary>
    public void AddComment(ShareableViewState state, PointComment comment)
    {
        state.Comments ??= [];
        comment.Id = GenerateId();
        comment.CreatedAt = DateTime.UtcNow;
        state.Comments.Add(comment);
    }

    /// <summary>
    /// Remove a comment.
    /// </summary>
    public void RemoveComment(ShareableViewState state, string commentId)
    {
        state.Comments?.RemoveAll(c => c.Id == commentId);
    }

    #endregion

    #region Helpers

    private static byte[] Compress(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        return outputStream.ToArray();
    }

    private static string Decompress(byte[] compressed)
    {
        using var inputStream = new MemoryStream(compressed);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }

    private static string GenerateId()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    #endregion
}

#region Data Models

/// <summary>
/// Represents a shareable view state.
/// </summary>
public class ShareableViewState
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }

    // Data source
    public string? DataSource { get; set; }  // File path or URL
    public string? DataHash { get; set; }    // Hash for validation

    // View settings
    public ViewportState? Viewport { get; set; }
    public double? PlaybackTime { get; set; }
    public float? PlaybackSpeed { get; set; }

    // Display options
    public bool ShowVelocity { get; set; } = true;
    public bool ShowCurvature { get; set; } = true;
    public bool ShowAnnotations { get; set; }
    public bool ShowAnalysis { get; set; }

    // Analysis settings
    public bool ShowEigenvalues { get; set; }
    public bool ShowLyapunov { get; set; }
    public bool ShowBifurcations { get; set; }

    // Comments/annotations
    public List<PointComment>? Comments { get; set; }

    // Bookmarks
    public List<BookmarkState>? Bookmarks { get; set; }
}

public class ViewportState
{
    public float PanX { get; set; }
    public float PanY { get; set; }
    public float Zoom { get; set; } = 1f;
}

public class PointComment
{
    public string? Id { get; set; }
    public int TimestepIndex { get; set; }
    public string? Text { get; set; }
    public string? Author { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BookmarkState
{
    public string? Name { get; set; }
    public ViewportState? Viewport { get; set; }
    public double? Time { get; set; }
}

public class GalleryEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; } = [];
    public ShareableViewState? State { get; set; }
}

#endregion
