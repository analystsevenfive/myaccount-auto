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
    List<DetailItemRecord>? Items = null);

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
        string discountColumn = "ส่วนลด")
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
        int docNoIndex = -1;
        int taxInvoiceIndex = -1;
        int deliveryOrderIndex = -1;
        int itemCodeIndex = -1;
        int quantityIndex = -1;
        int unitPriceIndex = -1;
        int warehouseIndex = -1;
        int locationIndex = -1;
        int discountIndex = -1;

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
        }

        // If only 1 column in header and neither matched, default to column 0 as name
        if (nameIndex == -1 && codeIndex == -1 && headers.Count == 1)
        {
            nameIndex = 0;
        }

        var results = new List<VendorCsvRecord>();
        var allItems = new List<DetailItemRecord>();
        string? firstVendorName = null;
        string? firstVendorCode = null;
        string? firstDocNo = null;
        string? firstTaxInvoice = null;
        string? firstDeliveryOrder = null;

        for (int row = 1; row < lines.Count; row++)
        {
            var cols = ParseCsvLine(lines[row]);
            string? name = null;
            string? code = null;
            string? docNo = null;
            string? taxInvoice = null;
            string? deliveryOrder = null;
            string? itemCode = null;
            string? quantity = null;
            string? unitPrice = null;
            string? warehouse = null;
            string? location = null;
            string? discount = null;

            if (nameIndex >= 0 && nameIndex < cols.Count) name = cols[nameIndex].Trim();
            if (codeIndex >= 0 && codeIndex < cols.Count) code = cols[codeIndex].Trim();
            if (docNoIndex >= 0 && docNoIndex < cols.Count) docNo = cols[docNoIndex].Trim();
            if (taxInvoiceIndex >= 0 && taxInvoiceIndex < cols.Count) taxInvoice = cols[taxInvoiceIndex].Trim();
            if (deliveryOrderIndex >= 0 && deliveryOrderIndex < cols.Count) deliveryOrder = cols[deliveryOrderIndex].Trim();
            if (itemCodeIndex >= 0 && itemCodeIndex < cols.Count) itemCode = cols[itemCodeIndex].Trim();
            if (quantityIndex >= 0 && quantityIndex < cols.Count) quantity = cols[quantityIndex].Trim();
            if (unitPriceIndex >= 0 && unitPriceIndex < cols.Count) unitPrice = cols[unitPriceIndex].Trim();
            if (warehouseIndex >= 0 && warehouseIndex < cols.Count) warehouse = cols[warehouseIndex].Trim();
            if (locationIndex >= 0 && locationIndex < cols.Count) location = cols[locationIndex].Trim();
            if (discountIndex >= 0 && discountIndex < cols.Count) discount = cols[discountIndex].Trim();

            // Fallback: if name not set but column 0 exists
            if (string.IsNullOrWhiteSpace(name) && cols.Count > 0 && nameIndex == -1)
            {
                name = cols[0].Trim();
            }

            // Save first non-empty header fields
            if (firstVendorName == null && !string.IsNullOrWhiteSpace(name)) firstVendorName = name;
            if (firstVendorCode == null && !string.IsNullOrWhiteSpace(code)) firstVendorCode = code;
            if (firstDocNo == null && !string.IsNullOrWhiteSpace(docNo)) firstDocNo = docNo;
            if (firstTaxInvoice == null && !string.IsNullOrWhiteSpace(taxInvoice)) firstTaxInvoice = taxInvoice;
            if (firstDeliveryOrder == null && !string.IsNullOrWhiteSpace(deliveryOrder)) firstDeliveryOrder = deliveryOrder;

            DetailItemRecord? item = null;
            if (!string.IsNullOrWhiteSpace(itemCode) ||
                !string.IsNullOrWhiteSpace(quantity) ||
                !string.IsNullOrWhiteSpace(unitPrice) ||
                !string.IsNullOrWhiteSpace(warehouse))
            {
                item = new DetailItemRecord(itemCode, quantity, unitPrice, warehouse, location, discount);
                allItems.Add(item);
            }

            var rowItems = item != null ? new List<DetailItemRecord> { item } : new List<DetailItemRecord>();

            if (!string.IsNullOrWhiteSpace(name) ||
                !string.IsNullOrWhiteSpace(code) ||
                !string.IsNullOrWhiteSpace(docNo) ||
                !string.IsNullOrWhiteSpace(taxInvoice) ||
                !string.IsNullOrWhiteSpace(deliveryOrder) ||
                item != null)
            {
                results.Add(new VendorCsvRecord(name, code, docNo, taxInvoice, deliveryOrder, rowItems));
            }
        }

        // Attach all collected items to the first primary record if multiple items exist
        if (results.Count > 0 && allItems.Count > 0)
        {
            var primary = results[0];
            results[0] = primary with
            {
                VendorName = primary.VendorName ?? firstVendorName,
                VendorCode = primary.VendorCode ?? firstVendorCode,
                DocumentNumber = primary.DocumentNumber ?? firstDocNo,
                TaxInvoiceNumber = primary.TaxInvoiceNumber ?? firstTaxInvoice,
                DeliveryOrderNumber = primary.DeliveryOrderNumber ?? firstDeliveryOrder,
                Items = allItems
            };
        }

        return results;
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
        string discountColumn = "ส่วนลด")
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
            discountColumn);
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
