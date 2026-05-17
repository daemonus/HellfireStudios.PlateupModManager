using System.Text.Json.Serialization;

namespace HellfireStudios.PlateupModManager.Models;

public class WorkshopMod
{
    [JsonPropertyName("publishedfileid")]
    public string PublishedFileId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("file_description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("preview_url")]
    public string PreviewUrl { get; set; } = string.Empty;

    [JsonPropertyName("subscriptions")]
    public int Subscriptions { get; set; }

    [JsonPropertyName("favorited")]
    public int Favorited { get; set; }

    [JsonPropertyName("time_updated")]
    public long TimeUpdated { get; set; }

    [JsonPropertyName("tags")]
    public List<WorkshopTag>? Tags { get; set; }

    [JsonIgnore]
    public bool IsInstalled { get; set; }

    [JsonIgnore]
    public bool IsEnabled { get; set; }
}

public class WorkshopTag
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;
}
