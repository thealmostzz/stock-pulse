# Realtime Outbox Delivery Design

## Goal

ป้องกันข่าวที่บันทึกสำเร็จแต่หายจาก realtime flow เมื่อ internal API หรือ SignalR ใช้งานไม่ได้ชั่วคราว โดยใช้ at-least-once delivery ที่ idempotent.

## Scope

- เพิ่ม transactional outbox ใน Worker persistence
- retry event ที่ยังส่งไม่สำเร็จด้วย exponential backoff
- ทำ internal realtime API idempotent ด้วย `eventId`
- แก้ integration tests ให้ใช้เฉพาะ `stockpulse_test`
- ป้องกัน source code ซ้ำและ concurrent insert ที่เป็น duplicate

ไม่ทำ API-side SignalR outbox, queue ภายนอก หรือ distributed transaction ใน Phase 0.

## Data Model

ตาราง `news_outbox_events` ประกอบด้วย:

- `event_id` UUID primary key
- `news_id` bigint unique foreign key ไปยัง `stock_news`
- `payload` JSONB
- `attempt_count` integer
- `next_attempt_at_utc` TIMESTAMPTZ
- `delivered_at_utc` TIMESTAMPTZ nullable
- `last_error` text nullable
- `created_at_utc` TIMESTAMPTZ

เพิ่ม unique index ให้ `news_sources.source_code`.

## Flow

1. Worker normalize และ deduplicate ข่าว
2. ใน transaction เดียวกัน Worker บันทึก `StockNews` และ outbox event หนึ่งรายการต่อ news ที่ insert สำเร็จ
3. Dispatcher อ่าน event ที่ยังไม่ delivered และถึงเวลาลองส่ง โดย claim แบบปลอดภัยต่อ concurrent worker
4. Dispatcher POST payload พร้อม `eventId` ไป internal API
5. API บันทึก idempotency receipt ตาม `eventId`; request ซ้ำตอบสำเร็จโดยไม่ broadcast ซ้ำ
6. Worker mark event delivered เมื่อ API ตอบสำเร็จ; หากล้มเหลว เพิ่ม attempt count และกำหนด `next_attempt_at_utc` ด้วย capped exponential backoff
7. Frontend deduplicate `news:new` ด้วย `newsId` เป็น defense-in-depth

การส่งเป็น at-least-once ระหว่าง Worker กับ API; idempotency ทำให้ผลที่ client เห็นเป็นหนึ่ง event ต่อ news.

## Failure Handling

- API/network failure: outbox คงอยู่และ retry ในรอบถัดไป
- Worker crash หลัง API ตอบสำเร็จแต่ก่อน mark delivered: retry ได้อย่างปลอดภัยเพราะ API idempotent
- duplicate news insert: database unique constraint เป็น final guard; duplicate ไม่สร้าง outbox event ใหม่
- source insert พร้อมกัน: unique constraint และ conflict handling ทำให้ได้ source เดียว

## Testing

- ข่าวที่ insert ต้องมี outbox event ใน transaction เดียวกัน
- notifier failure ต้องไม่ทำให้ event หาย และ retry ต้องส่งสำเร็จภายหลัง
- duplicate event ID ต้องไม่เรียก realtime publisher ซ้ำ
- test database guard ต้อง reject database ที่ไม่ใช่ `stockpulse_test` ก่อน DDL หรือ delete
- concurrent duplicate ingestion ต้องไม่ทำให้ทั้ง batch ล้ม

## Security and Operations

- shared internal key อยู่เฉพาะ environment variable หรือ User Secrets; ไม่มี default ที่ใช้ได้จริง
- endpoint `/internal/*` ตรวจ key แบบ constant-time และต้องถูกจำกัดเครือข่ายใน deployment
- log เฉพาะ event ID, news ID, attempt และ status; ห้าม log shared key หรือ payload เต็ม
