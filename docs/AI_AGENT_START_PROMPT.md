# Prompt สำหรับส่งต่อให้ AI Agent

คัดลอกข้อความด้านล่างไปใช้ได้เลย:

```text
ช่วยพัฒนาโปรเจกต์ ProsoftAutoLogin ต่อจากโฟลเดอร์นี้

เริ่มจากอ่าน AGENTS.md, README.md และเอกสารทั้งหมดใน docs/ ก่อนแก้โค้ด

เป้าหมายรอบนี้คือทำ Auto Login MVP ให้ใช้งานได้จริงบน Windows:
1. Build โปรเจกต์
2. เปิด Prosoft myAccount ค้างไว้ที่หน้า Login
3. รัน scripts/collect-process-info.ps1
4. ใช้ปุ่ม Export UI Tree เพื่อเก็บ metadata ของ control
5. ปรับ processNames, loginWindowTitleContains, passwordSelectors,
   loginButtonSelectors และ successWindowTitleContains ใน appsettings.json
6. ทดสอบ Login สำเร็จ, password ผิด, user login ค้าง, timeout และ cancel
7. อัปเดต docs/CONTROL_DISCOVERY.md ด้วยค่าที่ตรวจสอบจริง

ข้อบังคับ:
- ห้ามบันทึกหรือ log password
- ห้าม hardcode credential
- ห้ามเริ่มจาก mouse coordinate
- ใช้ UI Automation selector จากข้อมูลจริง
- อย่าเปลี่ยน architecture โดยไม่มีเหตุผล
- รายงานไฟล์ที่แก้ ผล build ผล test และสิ่งที่ยังต้องยืนยัน
```

## ข้อมูลที่ควรแนบให้ Agent หลังทดสอบบนเครื่องจริง

- `logs/ui-tree-*.txt`
- `logs/prosoft-process-*.txt`
- ภาพหน้า Login/popup ที่ไม่มีรหัสผ่านปรากฏ
- ชื่อหน้าต่างหลัง Login สำเร็จ
