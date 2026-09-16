# Prosoft Auto Login (MVP)

โปรเจกต์ต้นแบบสำหรับเปิด/เชื่อมต่อ Prosoft myAccount ที่กำลังทำงานอยู่ กรอกรหัสผ่าน และกด Login ผ่าน Windows UI Automation

> ขอบเขต MVP: ผู้ใช้เปิดหน้า Login ของ Prosoft ไว้ก่อน จากนั้นเปิดโปรแกรมนี้ กรอกรหัสผ่าน และกด **Login Prosoft**

## Tech stack

- C# / .NET 10
- WPF (Windows Desktop)
- FlaUI + UIA3
- JSON configuration

## เริ่มใช้งานบน Windows

1. ติดตั้ง [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. เปิด PowerShell ที่โฟลเดอร์นี้
3. รัน `./scripts/build.ps1`
4. เปิด Prosoft myAccount และค้างไว้ที่หน้า Login
5. รัน `./scripts/run.ps1`
6. กรอกรหัสผ่านในหน้าต่าง Prosoft Auto Login แล้วกด **Login Prosoft**

หากโปรแกรมหา Password หรือปุ่ม Login ไม่พบ ให้กด **Export UI Tree** แล้วเปิดไฟล์ที่สร้างใน `logs/` จากนั้นใช้ข้อมูล `AutomationId`, `Name`, `ClassName` และ `ControlType` ปรับ `appsettings.json`

## ความปลอดภัย

- MVP นี้ไม่บันทึกรหัสผ่านลงไฟล์
- รหัสผ่านอยู่ในหน่วยความจำเฉพาะระหว่างการทำงาน และช่อง Password จะถูกล้างเมื่อจบ
- UI Tree Exporter ไม่อ่านหรือบันทึกค่าภายใน TextBox/PasswordBox
- ห้ามใส่รหัสผ่านจริงไว้ใน `appsettings.json`, source code, log หรือ commit

## โครงสร้าง

```text
ProsoftAutoLogin/
├── AGENTS.md
├── README.md
├── ProsoftAutoLogin.sln
├── docs/
│   ├── ARCHITECTURE.md
│   ├── AI_AGENT_START_PROMPT.md
│   ├── CONTROL_DISCOVERY.md
│   ├── PRD.md
│   └── ROADMAP.md
├── reference/
│   └── prosoft-login-reference.png
├── scripts/
│   ├── build.ps1
│   ├── collect-process-info.ps1
│   ├── export-ui-tree.ps1
│   ├── run.ps1
│   └── test.ps1
├── src/ProsoftAutoLogin/
│   ├── Automation/
│   ├── Configuration/
│   ├── Models/
│   ├── App.xaml
│   ├── MainWindow.xaml
│   └── appsettings.json
└── tests/
    ├── README.md
    └── ProsoftAutoLogin.Tests/
```

## สิ่งที่ต้องยืนยันบนเครื่องจริง

ภาพอ้างอิงช่วยยืนยันหน้าตาโปรแกรม แต่ไม่สามารถบอก UI Automation properties ได้ โค้ดจึงใช้ selector หลายระดับและมีเครื่องมือ Export UI Tree ให้ตรวจค่าจริงบนเครื่อง

จุดที่ต้องยืนยันก่อนถือว่า MVP เสร็จ:

1. Process name ของ Prosoft
2. Selector ของช่อง Password
3. Selector ของปุ่ม Login
4. ชื่อหรือ selector ของหน้าหลักหลัง Login
5. รูปแบบ popup กรณีผู้ใช้อยู่ในระบบแล้วหรือรหัสผ่านผิด

อ่านงานถัดไปใน `AGENTS.md` และ `docs/ROADMAP.md`
