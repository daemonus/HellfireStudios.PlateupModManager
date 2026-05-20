namespace HellfireStudios.PlateupModManager.Models;

public class ModProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSpeedRunProfile { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ProfileMod> Mods { get; set; } = [];
}

public class ProfileMod
{
    public string PublishedFileId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayTitle => string.IsNullOrEmpty(Title) ? PublishedFileId : Title;
}
