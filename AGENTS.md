# AGENTS.md

## Project goal

ทำ Windows desktop utility ที่ควบคุม Prosoft myAccount ผ่าน UI Automation โดย MVP แรกต้องรับ password จากผู้ใช้ กรอกลงหน้า Login กด Login และรายงานผลอย่างถูกต้อง

## Current scope

- Attach กับ Prosoft process ที่เปิดอยู่
- ค้นหาหน้า Login
- ค้นหาช่อง Password ด้วย selector ที่ตั้งค่าได้
- กรอก password โดยไม่บันทึกลง disk/log
- ค้นหาและกดปุ่ม Login
- ตรวจ popup error, หน้าหลัก หรือ timeout
- Export UI metadata เพื่อปรับ selector บนเครื่องจริง

ยังไม่รวม Excel import, Windows Credential Manager, auto launch, quotation workflow หรือ mouse-coordinate automation

## First task for the next agent

1. อ่าน `README.md`, `docs/PRD.md`, `docs/ARCHITECTURE.md` และ `docs/CONTROL_DISCOVERY.md`
2. Build บน Windows ด้วย `scripts/build.ps1`
3. เปิดหน้า Login ของ Prosoft แล้วรัน `scripts/collect-process-info.ps1`
4. เปิดแอปและกด **Export UI Tree**
5. ปรับ `src/ProsoftAutoLogin/appsettings.json` ให้ตรงกับ control properties จริง
6. ทดสอบกรณี Login สำเร็จ, รหัสผ่านผิด, ผู้ใช้ login ค้างอยู่ และ timeout
7. บันทึกผลที่ยืนยันแล้วใน `docs/CONTROL_DISCOVERY.md`

## Architecture rules

- UI (`MainWindow`) ต้องไม่รู้รายละเอียด FlaUI selector
- Automation workflow ต้องอ่าน selector จาก config ไม่ hardcode ตำแหน่งหน้าจอ
- ห้ามใช้ coordinate click จนกว่า UIA และ keyboard fallback จะพิสูจน์ว่าใช้ไม่ได้
- ห้ามใช้ `Thread.Sleep`; ใช้ async polling + timeout + cancellation
- ห้าม log, persist, echo หรือ serialize password
- การเพิ่ม workflow ใหม่ต้องผ่าน interface ของ automation layer
- Error หนึ่งครั้งต้องคืนผลแบบ typed result ไม่ปล่อย exception ดิบไปแสดงแก่ผู้ใช้โดยไม่จำเป็น

## Security rules

- ห้ามใส่ credential จริงใน repository
- ห้าม export `Value` ของ Edit/Password control
- ห้ามใช้ clipboard สำหรับ password
- หากเพิ่ม Remember Password ให้ใช้ Windows Credential Manager หรือ DPAPI เท่านั้น
- Log ต้องไม่มี username/password เว้นแต่ username จำเป็นและผ่านการอนุมัติ

## Commands

```powershell
./scripts/build.ps1
./scripts/run.ps1
./scripts/collect-process-info.ps1
```

## Definition of done for MVP

- Build ผ่านบน Windows 10/11 ด้วย .NET 10 SDK
- ไม่พบ password ใน log หรือ generated files
- เข้า Prosoft ได้ 3 ครั้งติดต่อกันโดยไม่ใช้ mouse coordinate
- แสดง error ที่เข้าใจได้เมื่อไม่พบ process/control
- ตรวจ popup "ผู้ใช้อยู่ในระบบแล้ว" หรือ error login ได้
- Cancel ระหว่างรอได้ และ UI ไม่ค้าง

## Notes about the supplied screenshot

- Product: Prosoft myAccount
- Username appears as a dropdown
- Password appears as an edit field
- Buttons include Login and Cancel
- Error popup title appears to be `Login` and uses an `OK` button
- Screenshot alone does not reveal `AutomationId`; verify on the target PC
