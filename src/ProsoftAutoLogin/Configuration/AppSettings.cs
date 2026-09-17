using ProsoftAutoLogin.Models;

namespace ProsoftAutoLogin.Configuration;

public sealed class AppSettings
{
    public ProsoftOptions Prosoft { get; set; } = new();
}

public sealed class NavigationOptions
{
    public string ModuleName { get; set; } = "Purchase Order";
    public string SubModuleName { get; set; } = "PO Data Entry";
    public string TargetAction { get; set; } = "ซื้อเชื่อ";
    public int NavigationTimeoutSeconds { get; set; } = 15;
    public List<string> ModuleAliases { get; set; } = ["Purchase Order", "จัดซื้อ", "ระบบจัดซื้อ"];
    public List<string> SubModuleAliases { get; set; } = ["PO Data Entry", "บันทึกข้อมูลประจำวัน", "บันทึกประจำวัน"];
    public List<string> TargetActionAliases { get; set; } = ["ซื้อเชื่อ", "Credit Purchase"];
}

public sealed class VendorInputOptions
{
    public string CsvPath { get; set; } = "input/input.csv";
    public string VendorNameColumn { get; set; } = "ชื่อผู้ขาย";
    public string VendorCodeColumn { get; set; } = "รหัสผู้ขาย";
    public string DocumentNumberColumn { get; set; } = "เลขที่เอกสาร";
    public string TaxInvoiceNumberColumn { get; set; } = "เลขที่ใบกำกับ";
    public string DeliveryOrderNumberColumn { get; set; } = "เลขที่ใบส่งของ";
    public string ItemCodeColumn { get; set; } = "รหัสสินค้า";
    public string QuantityColumn { get; set; } = "จำนวน";
    public string UnitPriceColumn { get; set; } = "ราคาต่อหน่วย";
    public string WarehouseColumn { get; set; } = "คลัง";
    public string LocationColumn { get; set; } = "ที่เก็บ";
    public string DiscountColumn { get; set; } = "ส่วนลด";
    public string SearchByOption { get; set; } = "ชื่อผู้ขาย";
    public List<string> FindDialogTitleContains { get; set; } = ["Find", "ผู้ขาย"];
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed class GlOptions
{
    public string DefaultDepartment { get; set; } = "INTER";
    public bool AutoSaveAfterGl { get; set; } = true;
    public List<string> FindDepartmentDialogTitleContains { get; set; } = ["Find", "แผนก"];
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed class ProsoftOptions
{
    public string? ExecutablePath { get; set; } =
        @"C:\Program Files (x86)\Prosoft\myAccount\Bin\myaccount.exe";

    public List<string> ProcessNames { get; set; } = ["myAccount", "Prosoft"];

    public List<string> LoginWindowTitleContains { get; set; } =
        ["myAccount", "Prosoft", "Login"];

    public List<string> SuccessWindowTitleContains { get; set; } = ["myAccount"];

    public int AttachTimeoutSeconds { get; set; } = 15;

    public int ResultTimeoutSeconds { get; set; } = 15;

    public int PollIntervalMilliseconds { get; set; } = 300;

    public NavigationOptions Navigation { get; set; } = new();

    public VendorInputOptions VendorInput { get; set; } = new();

    public GlOptions Gl { get; set; } = new();

    public List<ControlSelector> UserNameSelectors { get; set; } =
    [
        new() { AutomationId = "cboUserName", ControlType = "ComboBox" },
        new() { NameContains = "User Name", ControlType = "ComboBox" },
        new() { NameContains = "User", ControlType = "ComboBox" },
        new() { ControlType = "ComboBox", Index = 0 },
        new() { AutomationId = "txtUserName", ControlType = "Edit" },
        new() { NameContains = "User", ControlType = "Edit" }
    ];

    public List<ControlSelector> ProfileSelectors { get; set; } =
    [
        new() { AutomationId = "cboProfile", ControlType = "ComboBox" },
        new() { NameContains = "Profile", ControlType = "ComboBox" },
        new() { ControlType = "ComboBox", Index = 1 }
    ];

    public List<ControlSelector> PasswordSelectors { get; set; } =
    [
        new() { IsPassword = true, ControlType = "Edit" },
        new() { AutomationId = "txtPassword", ControlType = "Edit" },
        new() { NameContains = "Password", ControlType = "Edit" },
        new() { ExcludeComboBoxChildren = true, ControlType = "Edit" },
        new() { IsPassword = true }
    ];

    public List<ControlSelector> LoginButtonSelectors { get; set; } =
    [
        new() { NameContains = "OK", ControlType = "Button" },
        new() { NameContains = "ตกลง", ControlType = "Button" },
        new() { AutomationId = "cb_ok", ControlType = "Button" },
        new() { AutomationId = "btnLogin", ControlType = "Button" },
        new() { NameContains = "Login", ControlType = "Button" }
    ];

    public List<string> DialogButtonNames { get; set; } = ["OK", "ตกลง"];
}
