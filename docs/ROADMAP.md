# Roadmap

## Phase 0 — Control discovery

- ยืนยัน process และ top-level windows
- Export UI Tree
- ระบุ password/login/error/main-window selectors

## Phase 1 — Auto Login MVP

- รับ password ใน WPF
- Attach Prosoft
- กรอกและกด Login
- ตรวจ success/error/timeout
- ทดสอบซ้ำอย่างน้อย 3 รอบ

## Phase 2 — Production hardening

- Auto launch ด้วย path ที่ตั้งค่าได้
- เลือก user/account อย่างปลอดภัย
- Windows Credential Manager แบบ opt-in
- Structured logging ที่ไม่มี credential
- Screenshot เฉพาะเมื่อ error และต้อง mask พื้นที่ input
- Installer/publish single-file

## Phase 3 — Excel job intake

- ClosedXML reader
- Header validation
- Preview rows ก่อนทำงาน
- Job status: Pending/Processing/Done/Error/Skipped
- SQLite checkpoint

## Phase 4 — Prosoft document workflow

- เปิดหน้า Quotation
- เลือกลูกค้า
- เพิ่ม SKU/Qty/Price/Discount
- Save และอ่าน Document No
- เขียนผลกลับ Excel
- Idempotency key ป้องกันเอกสารซ้ำ

## Phase 5 — Operations

- Pause/resume
- Retry policy ราย job
- Audit log
- Packaging และ auto-update ตามนโยบายองค์กร
