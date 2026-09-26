using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProsoftAutoLogin.Data;

public sealed record DetailItemRecord(
    string? ItemCode,
    string? Quantity = null,
    string? UnitPrice = null,
    string? Warehouse = null,
    string? Location = null,
    string? Discount = null);

public sealed record VendorCsvRecord(
    string? VendorName,
    string? VendorCode,
    string? DocumentNumber = null,
    string? TaxInvoiceNumber = null,
    string? DeliveryOrderNumber = null,
    List<DetailItemRecord>? Items = null,
    string? Status = null,
    string? TaxInvoiceDate = null,
    string? DeliveryOrderDate = null,
    string? ResultStatus = null)
{
    public string? DeliveryOrderNumber { get; set; } = DeliveryOrderNumber;
    public string? Status { get; set; } = Status;
    public string? TaxInvoiceDate { get; set; } = TaxInvoiceDate;
    public string? DeliveryOrderDate { get; set; } = DeliveryOrderDate;
    public string? ResultStatus { get; set; } = ResultStatus;
}

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
        string codeColumn = "รหัสผู้ขาย",
        string docNoColumn = "เลขที่เอกสาร",
        string taxInvoiceColumn = "เลขที่ใบกำกับ",
        string deliveryOrderColumn = "เลขที่ใบส่งของ",
        string itemCodeColumn = "รหัสสินค้า",
        string quantityColumn = "จำนวน",
        string unitPriceColumn = "ราคาต่อหน่วย",
        string warehouseColumn = "คลัง",
        string locationColumn = "ที่เก็บ",
        string discountColumn = "ส่วนลด",
        string statusColumn = "status approve",
        string taxInvoiceDateColumn = "วันที่ใบกำกับ",
        string deliveryOrderDateColumn = "วันที่ใบส่งของ")
    {
        var resolved = ResolveCsvPath(filePath);
        if (!File.Exists(resolved))
        {
            throw new FileNotFoundException($"ไม่พบไฟล์ CSV ที่: {resolved}", resolved);
        }

        var (lines, _) = ReadLinesWithDetectedEncoding(resolved);

        if (lines.Count == 0)
        {
            return [];
        }

        // Parse header
        var headers = ParseCsvLine(lines[0]);
        int nameIndex = -1;
        int codeIndex = -1;
        int docNoIndex = -1;
        int taxInvoiceIndex = -1;
        int deliveryOrderIndex = -1;
        int taxInvoiceDateIndex = -1;
        int deliveryOrderDateIndex = -1;
        int itemCodeIndex = -1;
        int quantityIndex = -1;
        int unitPriceIndex = -1;
        int warehouseIndex = -1;
        int locationIndex = -1;
        int discountIndex = -1;
        int statusIndex = headers.FindIndex(h => h.Trim().Equals(statusColumn, StringComparison.Ordinal));
        int resultStatusIndex = headers.FindIndex(h => h.Trim().Equals("Status", StringComparison.Ordinal));

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

            if (h.Equals(docNoColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("เลขที่เอกสาร", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DocNo", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DocumentNo", StringComparison.OrdinalIgnoreCase))
            {
                docNoIndex = i;
            }

            if (h.Equals(taxInvoiceColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("เลขที่ใบกำกับ", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("TaxInvoiceNo", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("InvoiceNo", StringComparison.OrdinalIgnoreCase))
            {
                taxInvoiceIndex = i;
            }

            if (h.Equals(deliveryOrderColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("เลขที่ใบส่งของ", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DeliveryOrderNo", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DONo", StringComparison.OrdinalIgnoreCase))
            {
                deliveryOrderIndex = i;
            }

            if (h.Equals(taxInvoiceDateColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("วันที่ใบกำกับ", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("TaxInvoiceDate", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("InvoiceDate", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("วันที่กำกับ", StringComparison.OrdinalIgnoreCase))
            {
                taxInvoiceDateIndex = i;
            }

            if (h.Equals(deliveryOrderDateColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("วันที่ใบส่งของ", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DeliveryOrderDate", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DONoDate", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DODate", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DeliveryDate", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("วันที่ส่งของ", StringComparison.OrdinalIgnoreCase))
            {
                deliveryOrderDateIndex = i;
            }

            if (h.Equals(itemCodeColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("รหัสสินค้า", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("ItemCode", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("ProductCode", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("SKU", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("THAI Code", StringComparison.OrdinalIgnoreCase))
            {
                itemCodeIndex = i;
            }

            if (h.Equals(quantityColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("จำนวน", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Quantity", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Qty", StringComparison.OrdinalIgnoreCase))
            {
                quantityIndex = i;
            }

            if (h.Equals(unitPriceColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("ราคาต่อหน่วย", StringComparison.OrdinalIgnoreCase) ||
                h.Contains("ราคา/หน่วย", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("ราคา", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("UnitPrice", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Price", StringComparison.OrdinalIgnoreCase) ||
                h.Contains("Unit Price", StringComparison.OrdinalIgnoreCase))
            {
                unitPriceIndex = i;
            }

            if (h.Equals(warehouseColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("คลัง", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Warehouse", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("WH", StringComparison.OrdinalIgnoreCase))
            {
                warehouseIndex = i;
            }

            if (h.Equals(locationColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("ที่เก็บ", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Location", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Loc", StringComparison.OrdinalIgnoreCase))
            {
                locationIndex = i;
            }

            if (h.Equals(discountColumn, StringComparison.OrdinalIgnoreCase) ||
                h.Contains("ส่วนลด", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Discount", StringComparison.OrdinalIgnoreCase))
            {
                discountIndex = i;
            }

            if (statusIndex == -1 && i != resultStatusIndex &&
                (h.Equals(statusColumn, StringComparison.OrdinalIgnoreCase) ||
                 h.Equals("status approve", StringComparison.OrdinalIgnoreCase) ||
                 h.Equals("status", StringComparison.Ordinal) ||
                 h.Contains("สถานะ", StringComparison.OrdinalIgnoreCase)))
            {
                statusIndex = i;
            }
        }

        // If only 1 column in header and neither matched, default to column 0 as name
        if (nameIndex == -1 && codeIndex == -1 && headers.Count == 1)
        {
            nameIndex = 0;
        }

        var docGroups = new Dictionary<string, VendorCsvRecord>();
        var docOrder = new List<string>();

        for (int row = 1; row < lines.Count; row++)
        {
            var cols = ParseCsvLine(lines[row]);
            string? name = null;
            string? code = null;
            string? docNo = null;
            string? taxInvoice = null;
            string? deliveryOrder = null;
            string? taxInvoiceDate = null;
            string? deliveryOrderDate = null;
            string? itemCode = null;
            string? quantity = null;
            string? unitPrice = null;
            string? warehouse = null;
            string? location = null;
            string? discount = null;
            string? status = null;
            string? resultStatus = null;

            if (nameIndex >= 0 && nameIndex < cols.Count) name = cols[nameIndex].Trim();
            if (codeIndex >= 0 && codeIndex < cols.Count) code = cols[codeIndex].Trim();
            if (docNoIndex >= 0 && docNoIndex < cols.Count) docNo = cols[docNoIndex].Trim();
            if (taxInvoiceIndex >= 0 && taxInvoiceIndex < cols.Count) taxInvoice = cols[taxInvoiceIndex].Trim();
            if (deliveryOrderIndex >= 0 && deliveryOrderIndex < cols.Count) deliveryOrder = cols[deliveryOrderIndex].Trim();
            if (taxInvoiceDateIndex >= 0 && taxInvoiceDateIndex < cols.Count) taxInvoiceDate = cols[taxInvoiceDateIndex].Trim();
            if (deliveryOrderDateIndex >= 0 && deliveryOrderDateIndex < cols.Count) deliveryOrderDate = cols[deliveryOrderDateIndex].Trim();
            if (itemCodeIndex >= 0 && itemCodeIndex < cols.Count) itemCode = cols[itemCodeIndex].Trim();
            if (quantityIndex >= 0 && quantityIndex < cols.Count) quantity = cols[quantityIndex].Trim();
            if (unitPriceIndex >= 0 && unitPriceIndex < cols.Count) unitPrice = cols[unitPriceIndex].Trim();
            if (warehouseIndex >= 0 && warehouseIndex < cols.Count) warehouse = cols[warehouseIndex].Trim();
            if (locationIndex >= 0 && locationIndex < cols.Count) location = cols[locationIndex].Trim();
            if (discountIndex >= 0 && discountIndex < cols.Count) discount = cols[discountIndex].Trim();
            if (statusIndex >= 0 && statusIndex < cols.Count) status = cols[statusIndex].Trim();
            if (resultStatusIndex >= 0 && resultStatusIndex < cols.Count) resultStatus = cols[resultStatusIndex].Trim();

            // Fallback: if name not set but column 0 exists
            if (string.IsNullOrWhiteSpace(name) && cols.Count > 0 && nameIndex == -1)
            {
                name = cols[0].Trim();
            }

            DetailItemRecord? item = null;
            if (!string.IsNullOrWhiteSpace(itemCode) ||
                !string.IsNullOrWhiteSpace(quantity) ||
                !string.IsNullOrWhiteSpace(unitPrice) ||
                !string.IsNullOrWhiteSpace(warehouse))
            {
                item = new DetailItemRecord(itemCode, quantity, unitPrice, warehouse, location, discount);
            }

            bool hasAnyData = !string.IsNullOrWhiteSpace(name) ||
                              !string.IsNullOrWhiteSpace(code) ||
                              !string.IsNullOrWhiteSpace(docNo) ||
                              !string.IsNullOrWhiteSpace(taxInvoice) ||
                              !string.IsNullOrWhiteSpace(deliveryOrder) ||
                              !string.IsNullOrWhiteSpace(taxInvoiceDate) ||
                              !string.IsNullOrWhiteSpace(deliveryOrderDate) ||
                              item != null;
            if (!hasAnyData) continue;

            // Grouping key: group rows sharing the same Document Number or Tax Invoice into 1 document
            string docKey = !string.IsNullOrWhiteSpace(docNo)
                ? $"DOC:{docNo.Trim()}"
                : !string.IsNullOrWhiteSpace(taxInvoice)
                    ? $"INV:{taxInvoice.Trim()}"
                    : !string.IsNullOrWhiteSpace(deliveryOrder)
                        ? $"DO:{deliveryOrder.Trim()}"
                        : $"ROW:{row}";

            if (docGroups.TryGetValue(docKey, out var existingDoc))
            {
                if (item != null && existingDoc.Items != null)
                {
                    existingDoc.Items.Add(item);
                }
                if (string.IsNullOrWhiteSpace(existingDoc.Status) && !string.IsNullOrWhiteSpace(status))
                {
                    existingDoc.Status = status;
                }
                if (string.IsNullOrWhiteSpace(existingDoc.ResultStatus) && !string.IsNullOrWhiteSpace(resultStatus))
                {
                    existingDoc.ResultStatus = resultStatus;
                }
                if (string.IsNullOrWhiteSpace(existingDoc.TaxInvoiceDate) && !string.IsNullOrWhiteSpace(taxInvoiceDate))
                {
                    existingDoc.TaxInvoiceDate = taxInvoiceDate;
                }
                if (string.IsNullOrWhiteSpace(existingDoc.DeliveryOrderDate) && !string.IsNullOrWhiteSpace(deliveryOrderDate))
                {
                    existingDoc.DeliveryOrderDate = deliveryOrderDate;
                }
            }
            else
            {
                var docItems = item != null ? new List<DetailItemRecord> { item } : new List<DetailItemRecord>();
                var newDoc = new VendorCsvRecord(
                    name, code, docNo, taxInvoice, deliveryOrder, docItems,
                    status, taxInvoiceDate, deliveryOrderDate, resultStatus);
                docGroups[docKey] = newDoc;
                docOrder.Add(docKey);
            }
        }

        return docOrder.Select(k => docGroups[k]).ToList();
    }

    public static void UpdateDocumentResult(
        string filePath,
        VendorCsvRecord document,
        TimeSpan elapsed,
        string resultStatus = "Completed")
    {
        var resolved = ResolveCsvPath(filePath);
        var (lines, sourceEncoding) = ReadLinesWithDetectedEncoding(resolved);
        if (lines.Count == 0) return;

        var headers = ParseCsvLine(lines[0]);
        int docNoIndex = FindHeaderIndex(headers, "เลขที่เอกสาร", "DocNo", "DocumentNo");
        int taxInvoiceIndex = FindHeaderIndex(headers, "เลขที่ใบกำกับ", "TaxInvoiceNo", "InvoiceNo");
        int deliveryOrderIndex = FindHeaderIndex(headers, "เลขที่ใบส่งของ", "DeliveryOrderNo", "DONo");
        int timeUseIndex = FindHeaderIndex(headers, "Time use");
        int resultStatusIndex = headers.FindIndex(h => h.Trim().Equals("Status", StringComparison.Ordinal));

        if (timeUseIndex < 0 || resultStatusIndex < 0)
        {
            throw new InvalidDataException("ไม่พบคอลัมน์ 'Time use' หรือ 'Status' ในไฟล์ CSV");
        }

        string documentKey = BuildDocumentKey(
            document.DocumentNumber,
            document.TaxInvoiceNumber,
            document.DeliveryOrderNumber);
        string elapsedText = elapsed.ToString(@"hh\:mm\:ss");

        for (int row = 1; row < lines.Count; row++)
        {
            var columns = ParseCsvLine(lines[row]);
            string rowKey = BuildDocumentKey(
                GetColumn(columns, docNoIndex),
                GetColumn(columns, taxInvoiceIndex),
                GetColumn(columns, deliveryOrderIndex));

            if (rowKey == documentKey)
            {
                EnsureColumnCount(columns, headers.Count);
                columns[timeUseIndex] = elapsedText;
                columns[resultStatusIndex] = resultStatus;
                lines[row] = string.Join(",", columns.Select(EscapeCsvValue));
            }
        }

        using var stream = new FileStream(resolved, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, sourceEncoding);
        foreach (var line in lines)
        {
            writer.WriteLine(line);
        }
    }

    public static List<VendorCsvRecord> ReadApprovedDocuments(
        string filePath,
        string requiredStatus = "Approved",
        string statusColumn = "status approve",
        string nameColumn = "ชื่อผู้ขาย",
        string codeColumn = "รหัสผู้ขาย",
        string docNoColumn = "เลขที่เอกสาร",
        string taxInvoiceColumn = "เลขที่ใบกำกับ",
        string deliveryOrderColumn = "เลขที่ใบส่งของ",
        string itemCodeColumn = "รหัสสินค้า",
        string quantityColumn = "จำนวน",
        string unitPriceColumn = "ราคาต่อหน่วย",
        string warehouseColumn = "คลัง",
        string locationColumn = "ที่เก็บ",
        string discountColumn = "ส่วนลด",
        string taxInvoiceDateColumn = "วันที่ใบกำกับ",
        string deliveryOrderDateColumn = "วันที่ใบส่งของ")
    {
        var all = ReadAllVendors(
            filePath,
            nameColumn,
            codeColumn,
            docNoColumn,
            taxInvoiceColumn,
            deliveryOrderColumn,
            itemCodeColumn,
            quantityColumn,
            unitPriceColumn,
            warehouseColumn,
            locationColumn,
            discountColumn,
            statusColumn,
            taxInvoiceDateColumn,
            deliveryOrderDateColumn);

        if (string.IsNullOrWhiteSpace(requiredStatus))
        {
            return all;
        }

        return all.Where(d =>
            string.Equals(d.Status?.Trim(), requiredStatus.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(d.ResultStatus?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    private static int FindHeaderIndex(List<string> headers, params string[] names)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            if (names.Any(name => headers[i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }

        return -1;
    }

    private static string BuildDocumentKey(string? documentNumber, string? taxInvoiceNumber, string? deliveryOrderNumber)
    {
        if (!string.IsNullOrWhiteSpace(documentNumber)) return $"DOC:{documentNumber.Trim()}";
        if (!string.IsNullOrWhiteSpace(taxInvoiceNumber)) return $"INV:{taxInvoiceNumber.Trim()}";
        if (!string.IsNullOrWhiteSpace(deliveryOrderNumber)) return $"DO:{deliveryOrderNumber.Trim()}";
        return string.Empty;
    }

    private static string? GetColumn(List<string> columns, int index)
    {
        return index >= 0 && index < columns.Count ? columns[index].Trim() : null;
    }

    private static void EnsureColumnCount(List<string> columns, int count)
    {
        while (columns.Count < count)
        {
            columns.Add(string.Empty);
        }
    }

    private static string EscapeCsvValue(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    public static VendorCsvRecord? ReadFirstVendor(
        string filePath,
        string nameColumn = "ชื่อผู้ขาย",
        string codeColumn = "รหัสผู้ขาย",
        string docNoColumn = "เลขที่เอกสาร",
        string taxInvoiceColumn = "เลขที่ใบกำกับ",
        string deliveryOrderColumn = "เลขที่ใบส่งของ",
        string itemCodeColumn = "รหัสสินค้า",
        string quantityColumn = "จำนวน",
        string unitPriceColumn = "ราคาต่อหน่วย",
        string warehouseColumn = "คลัง",
        string locationColumn = "ที่เก็บ",
        string discountColumn = "ส่วนลด",
        string statusColumn = "status approve",
        string taxInvoiceDateColumn = "วันที่ใบกำกับ",
        string deliveryOrderDateColumn = "วันที่ใบส่งของ")
    {
        var list = ReadAllVendors(
            filePath,
            nameColumn,
            codeColumn,
            docNoColumn,
            taxInvoiceColumn,
            deliveryOrderColumn,
            itemCodeColumn,
            quantityColumn,
            unitPriceColumn,
            warehouseColumn,
            locationColumn,
            discountColumn,
            statusColumn,
            taxInvoiceDateColumn,
            deliveryOrderDateColumn);
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

    private static (List<string> Lines, Encoding Encoding) ReadLinesWithDetectedEncoding(string path)
    {
        var utf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        try
        {
            return (ReadLinesWithShare(path, utf8), utf8);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var windows874 = Encoding.GetEncoding(874);
            return (ReadLinesWithShare(path, windows874), windows874);
        }
    }
}
