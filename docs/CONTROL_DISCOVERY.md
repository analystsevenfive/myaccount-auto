# Prosoft Control Discovery

เอกสารนี้เป็น worksheet สำหรับบันทึกค่าจริงจากเครื่อง Windows ที่ติดตั้ง Prosoft

## วิธีเก็บข้อมูล

1. เปิด Prosoft ค้างไว้ที่หน้า Login
2. เปิด Prosoft Auto Login
3. กด **Export UI Tree**
4. เปิดไฟล์ล่าสุดใน `logs/ui-tree-*.txt`
5. ค้นคำว่า `Password`, `Login`, `OK`, `Cancel` และชื่อหน้าต่าง
6. กรอกค่าที่พบลงตารางด้านล่าง

UI Tree Exporter ไม่อ่าน `Value` ของ control จึงไม่เก็บ password

## Confirmed values

| Target | Name | AutomationId | ClassName | ControlType | Notes |
| --- | --- | --- | --- | --- | --- |
| Process | `myaccount` | — | — | — | ยืนยัน path: `C:\Program Files (x86)\Prosoft\myAccount\Bin\myaccount.exe` |
| Application Stack | PowerBuilder 8.0 | — | — | — | พัฒนาด้วย Sybase PowerBuilder 8 (`pbvm80.dll`, `pbdwe80.dll`, `logon.pbd`) |
| Login window | Title contains `myAccount` / `Prosoft` | — | Win32 / PowerBuilder Window | Window | ภาพมีคำว่า "Prosoft myAccount ระบบบัญชีสำเร็จรูปสำหรับธุรกิจ SMEs" |
| User Name | NameContains `User Name` / `User` | cboUserName | ComboBox / FNCOMBO | ComboBox | อยู่บนสุด (Top < Password), ComboBox index 0, เลือกค่าด้วย `CB_SELECTSTRING` หรือพิมพ์ |
| Password | NameContains `Password` | txtPassword | Edit / FNEDIT | Edit | อยู่ตรงกลาง (User Name < Top < Profile), ตรวจ `ES_PASSWORD`/`EM_GETPASSWORDCHAR`, ตั้ง `excludeComboBoxChildren: true` เพื่อไม่ให้ชนกับ Edit ใน ComboBox |
| Profile | NameContains `Profile` | cboProfile | ComboBox / FNCOMBO | ComboBox | อยู่ด้านล่างช่อง Password (Top > Password), ComboBox index 1, เลือกค่าด้วย `CB_SELECTSTRING` เช่น "Nts" |
| Login button | `Login` / `เข้าสู่ระบบ` | btnLogin | Button / FNBUTTON | Button | อยู่แถวล่างสุด ปุ่มแรก Button index 0 |
| Cancel button | `Cancel` | — | Button / FNBUTTON | Button | อยู่แถวล่างสุดข้างปุ่ม Login |
| Error dialog | `Login` | — | #32770 / Dialog | Window | Dialog title คือ `Login` |
| OK button | `OK` / `ตกลง` | — | Button | Button | ใช้เป็นสัญญาณ Error dialog ตรวจจับ popup สำเร็จ |
| Main window | Title contains `myAccount` / `Prosoft` | — | Window | Window | ใช้ยืนยัน success ควบคู่กับการตรวจจับว่าหน้าต่าง Login ปิดตัวลง |
| Nav TreeView | `Purchase Order` -> `PO Data Entry` | — | SysTreeView32 / TreeItem | TreeItem | เมนูหมวดด้านซ้าย เมนูหลักคือ `Purchase Order` หมวดย่อยคือ `PO Data Entry` |
| Nav Workflow | `Purchase Order` Workflow Diagram | — | Pane / Canvas / WebBrowser | Pane | แสดง Flowchart เมื่อเลือก `PO Data Entry` |
| ซื้อเชื่อ (Target) | `ซื้อเชื่อ` | — | Button / Text / Card | Button/Text | การ์ด/ปุ่มตรงกลางของ Workflow สำหรับเปิดหน้าจอซื้อเชื่อ (Credit Purchase) |

## Screenshot observations

- ช่อง User Name เป็น dropdown และมีค่าผู้ใช้ที่เลือกอยู่แล้ว
- ช่อง Password อยู่ใต้ User Name
- มีปุ่ม Login และ Cancel
- มีตัวเลือก Server ด้านล่าง
- popup อาจแจ้งว่าผู้ใช้อยู่ในระบบแล้วและไม่สามารถ Login ซ้ำได้
- หน้าจอหลักหลัง Login:
  - ฝั่งซ้ายมี TreeView: `Purchase Order` แตกย่อยเป็น `PO Data Entry`, `PO Reports`, `PO Analysis Reports`, `PO Forms`
  - เมื่อเลือก `PO Data Entry` ฝั่งขวาจะแสดง Workflow Diagram ของ `Purchase Order`
  - กล่อง/การ์ดขั้นตอนงานใน Workflow ประกอบด้วย: `ใบสั่งซื้อ`, `ซื้อสด`, `ซื้อเชื่อ` (เป้าหมาย), `จ่ายเงินมัดจำ`, `ส่งคืน, ลดหนี้`, `เพิ่มหนี้`, `ปันส่วนต้นทุนสินค้า`, `PO Export/Import`

## Recommended first selector update

หลังยืนยันค่าจริง ให้แก้ `appsettings.json` เช่น:

```json
{
  "automationId": "ค่าจริงจาก UI Tree",
  "controlType": "Edit",
  "index": 0
}
```

ไม่ควรลบ fallback เดิมจนกว่าจะทดสอบผ่านหลายครั้ง

## Verified GL Tab and Save Controls

- **GL Tab**: Index 4 (5th tab) ใน TabControl `pbtab32_80` หรือคลิกพิกัด `childRect.Left + 300`, `childRect.Bottom - 52`
- **ปุ่ม Serch รูปแบบการ Post**: ปุ่มลูกศรสีเขียว `[ > ]` สแกนสีเขียวสด (`G > 150`) อ้างอิงจากตำแหน่งจริง
- **Checkbox แก้ไข GL**: อยู่ทางซ้ายของปุ่ม [ > ] พิกัด `greenArrow.X - 418` ตรวจสอบสถานะ Checked ด้วยการนับพิกเซลสีดำด้านในกล่อง 13x13
- **คอลัมน์ แผนก (Department)**:
  - ความกว้างคอลัมน์: จาก `greenArrow.X - 278` ถึง `greenArrow.X - 183`
  - ปุ่ม Dropdown `[ v ]`: อยู่ที่ `greenArrow.X - 186`, `greenArrow.Y + 42 + (row - 1) * 17`
  - ช่องแก้ไขข้อความ (Cell Text Box): อยู่ที่ `greenArrow.X - 235`, `greenArrow.Y + 42 + (row - 1) * 17`
  - **ข้อสังเกตสำคัญใน PowerBuilder DataWindow**:
    - DataWindow ต้องถูกคลิกเพื่อโฟกัสก่อน แถวแรกจึงจะรับคีย์บอร์ดและแสดงปุ่ม Dropdown
    - แถวที่ 1 จะมีค่าเริ่มต้นคือ `PURCHASE` ต้องใช้ **Double-Click** เพื่อไฮไลต์ทั้งคำ แล้วกด **Backspace** เพื่อล้างค่าให้กลายเป็นช่องว่างก่อน
    - สลับภาษาเป็น English (US) แล้วกดเลือกจาก Dropdown ด้วยคีย์ `I` + `Enter`
    - ยืนยันซ้ำด้วยการ Double-Click และวางผ่าน Clipboard (`Ctrl+V`) ด้วย `"INTER"` แล้วกด `Enter`
- **ปุ่ม Save และ Popup ยืนยัน**:
  - คลิกปุ่ม Save ที่ Toolbar ด้านล่าง `childRect.Left + 85`, `childRect.Bottom - 20` หรือ UIA Button `Save`
  - **ห้ามใช้ `Ctrl+S`** เพราะใน PowerBuilder DataWindow คีย์นี้คือคำสั่ง "Specify Sort Columns"
  - ตรวจจับ Popup ยืนยันบันทึก `#32770` (เช่น ข้อความเตือนเลขที่เอกสารข้าม) และกดปุ่มยืนยัน (`OK` / `Yes` / Button 1) อัตโนมัติ

