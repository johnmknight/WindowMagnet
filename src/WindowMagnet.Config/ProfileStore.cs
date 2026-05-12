using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowMagnet.Config;

/// <summary>
/// Loads/saves <see cref="Profile"/> as JSON. Defaults to
/// <c>%APPDATA%\WindowMagnet\profiles.json</c>; takes a custom path for tests.
/// </summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string Path { get; }

    public ProfileStore(string? customPath = null)
    {
        Path = customPath ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowMagnet",
            "profiles.json");
    }

    public Profile Load()
    {
        if (!File.Exists(Path)) return new Profile();
        try
        {
            using var stream = File.OpenRead(Path);
            return JsonSerializer.Deserialize<Profile>(stream, JsonOpts) ?? new Profile();
        }
        catch
        {
            // Bad config shouldn't crash the app — fall back to defaults.
            return new Profile();
        }
    }

    public void Save(Profile profile)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(Path, JsonSerializer.Serialize(profile, JsonOpts));
    }
}
