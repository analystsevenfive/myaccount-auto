# Tests

MVP นี้ต้องทดสอบกับ Prosoft จริงบน Windows เพราะ control tree ไม่สามารถจำลองจากภาพหน้าจอได้

## Manual smoke test

1. Prosoft ไม่ได้เปิด -> ต้องแจ้งว่าไม่พบ process
2. Prosoft เปิดหน้า Login + password ว่าง -> ต้องไม่เริ่ม automation
3. password ถูก -> หน้า Login หายหรือพบ main window และขึ้น Success
4. password ผิด -> ต้องแสดงข้อความจาก popup และไม่กด OK เอง
5. user ค้างในระบบ -> ต้องแสดงข้อความ popup
6. กด Cancel ระหว่างรอ -> ต้องยกเลิกและ UI กลับมาใช้งานได้
7. Export UI Tree -> ไฟล์ต้องไม่มีค่าที่พิมพ์ใน PasswordBox

## Automated unit tests

รันชุด unit test ด้วยคำสั่ง:

```powershell
./scripts/test.ps1
```

ครอบคลุม:
- การ Deserialize `appsettings.json` และความถูกต้องของ selectors/timeouts
- การทำงานของ `LoginResult` (Success, Error, Timeout)
- ความถูกต้องของค่าเริ่มต้นใน `ProsoftOptions`
