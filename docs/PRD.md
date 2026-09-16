# PRD — Prosoft Auto Login MVP

## Problem

ผู้ใช้ต้องเปิด Prosoft myAccount และกรอกรหัสผ่านเพื่อเข้าสู่ระบบซ้ำ ๆ เป้าหมายแรกคือพิสูจน์ว่าเราสามารถควบคุมหน้า Login ของ Prosoft ด้วย Windows UI Automation ได้อย่างเสถียร ก่อนขยายไปอ่าน Excel และกรอกเอกสารอัตโนมัติ

## Primary user story

เมื่อหน้า Login ของ Prosoft เปิดอยู่ ผู้ใช้เปิด Prosoft Auto Login กรอกรหัสผ่านครั้งเดียว และกดปุ่มเดียวเพื่อส่งรหัสผ่านเข้า Prosoft และเข้าสู่ระบบ

## Functional requirements

1. แสดง PasswordBox และไม่แสดงตัวอักษรจริง
2. ตรวจว่ามี Prosoft process ที่กำหนดอยู่หรือไม่
3. Attach โดยไม่เปิด process ซ้ำ
4. หา top-level login window จาก title และ fallback ที่กำหนดได้
5. หา password control จาก selector แบบเรียงลำดับ
6. กรอก password ด้วย ValuePattern/TextBox และ fallback เป็น keyboard input
7. หา Login button จาก selector แบบเรียงลำดับและ invoke/click
8. Poll ผลลัพธ์จน success, error, timeout หรือ cancel
9. แสดงสถานะเป็นภาษาไทยที่เข้าใจได้
10. Export UI metadata โดยไม่ export ค่าภายใน input controls

## Non-functional requirements

- Windows 10/11
- UI ต้องไม่ค้างระหว่าง automation
- ทุก wait ต้องมี timeout และ cancel ได้
- Config ต้องแก้ selector ได้โดยไม่แก้ source code
- ไม่มี credential ใน source/config/log
- ไม่พึ่งพา Microsoft Excel ใน MVP

## Acceptance scenarios

| Scenario | Expected result |
| --- | --- |
| Prosoft ไม่ได้เปิด | แจ้งว่าไม่พบ process พร้อมรายชื่อที่กำลังค้นหา |
| ไม่กรอกรหัสผ่าน | ไม่เริ่ม automation และแจ้งให้กรอก |
| ไม่พบ password control | แจ้งให้ Export UI Tree แล้วปรับ selector |
| ไม่พบ Login button | แจ้งให้ Export UI Tree แล้วปรับ selector |
| Login สำเร็จ | แสดง Success จากหน้าหลักหรือหน้า Login ที่หายไป |
| รหัสผ่านผิด | ตรวจ dialog และแสดงข้อความที่อ่านได้ |
| ผู้ใช้อยู่ในระบบแล้ว | ตรวจ dialog และแสดงข้อความจาก popup |
| ไม่มีผลลัพธ์ตามเวลา | คืน Timeout และ UI พร้อมลองใหม่ |
| ผู้ใช้กด Cancel | ยกเลิกอย่างปลอดภัยและไม่ปิด Prosoft |

## Out of scope

- เก็บ password
- เลือก username อัตโนมัติ
- เปิด Prosoft.exe อัตโนมัติ
- อ่าน Excel
- สร้าง QT/SO
- click ด้วย coordinate
- auto dismiss popup
