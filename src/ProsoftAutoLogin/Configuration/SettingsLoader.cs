using System.IO;
using System.Text.Json;

namespace ProsoftAutoLogin.Configuration;

public static class SettingsLoader
{
    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "ไม่พบ appsettings.json ในโฟลเดอร์โปรแกรม",
                path);
        }

        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        return settings
            ?? throw new InvalidOperationException("appsettings.json ไม่มีข้อมูลที่อ่านได้");
    }
}
