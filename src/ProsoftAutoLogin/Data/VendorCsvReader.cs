using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProsoftAutoLogin.Data;

public sealed record VendorCsvRecord(string? VendorName, string? VendorCode);

public static class VendorCsvReader
{
    public static string ResolveCsvPath(string relativeOrFullPath)
    {
        if (Path.IsPathRooted(relativeOrFullPath) && File.Exists(relativeOrFullPath))
        {
            return relativeOrFullPath;
        }

        // 1. Current directory
        var path1 = Path.GetFullPath(relativeOrFullPath);
        if (File.Exists(path1)) return path1;

        // 2. Base directory
        var path2 = Path.Combine(AppContext.BaseDirectory, relativeOrFullPath);
        if (File.Exists(path2)) return Path.GetFullPath(path2);

        // 3. Project root fallback
        var path3 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativeOrFullPath));
        if (File.Exists(path3)) return path3;

        var path4 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativeOrFullPath));
        if (File.Exists(path4)) return path4;

        return path1;
    }

    public static List<VendorCsvRecord> ReadAllVendors(
        string filePath,
        string nameColumn = "ชื่อผู้ขาย",
        string codeColumn = "รหัสผู้ขาย")
    {
        var resolved = ResolveCsvPath(filePath);
        if (!File.Exists(resolved))
        {
            throw new FileNotFoundException($"ไม่พบไฟล์ CSV ที่: {resolved}", resolved);
        }

        // Try reading with UTF-8 first using FileShare.ReadWrite (works even if open in Excel)
        var lines = ReadLinesWithShare(resolved, Encoding.UTF8);

        if (lines.Count < 2)
        {
            // Might be encoded in Windows-874 / TIS-620
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var thaiEnc = Encoding.GetEncoding(874);
                lines = ReadLinesWithShare(resolved, thaiEnc);
            }
            catch { }
        }

        if (lines.Count == 0)
        {
            return [];
        }

        // Parse header
        var headers = ParseCsvLine(lines[0]);
        int nameIndex = -1;
        int codeIndex = -1;

        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim();
            if (h.Equals(nameColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("ชื่อผู้ขาย", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("VendorName", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Vendor", StringComparison.OrdinalIgnoreCase))
            {
                nameIndex = i;
            }

            if (h.Equals(codeColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("รหัสผู้ขาย", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("VendorCode", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Code", StringComparison.OrdinalIgnoreCase))
            {
                codeIndex = i;
            }
        }

        // If only 1 column in header and neither matched, default to column 0 as name
        if (nameIndex == -1 && codeIndex == -1 && headers.Count == 1)
        {
            nameIndex = 0;
        }

        var results = new List<VendorCsvRecord>();
        for (int row = 1; row < lines.Count; row++)
        {
            var cols = ParseCsvLine(lines[row]);
            string? name = null;
            string? code = null;

            if (nameIndex >= 0 && nameIndex < cols.Count)
            {
                name = cols[nameIndex].Trim();
            }
            if (codeIndex >= 0 && codeIndex < cols.Count)
            {
                code = cols[codeIndex].Trim();
            }

            // Fallback: if name not set but column 0 exists
            if (string.IsNullOrWhiteSpace(name) && cols.Count > 0 && nameIndex == -1)
            {
                name = cols[0].Trim();
            }

            if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(code))
            {
                results.Add(new VendorCsvRecord(name, code));
            }
        }

        return results;
    }

    public static VendorCsvRecord? ReadFirstVendor(
        string filePath,
        string nameColumn = "ชื่อผู้ขาย",
        string codeColumn = "รหัสผู้ขาย")
    {
        var list = ReadAllVendors(filePath, nameColumn, codeColumn);
        return list.FirstOrDefault();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static List<string> ReadLinesWithShare(string path, Encoding encoding)
    {
        var result = new List<string>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, encoding);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                result.Add(trimmed);
            }
        }
        return result;
    }
}
