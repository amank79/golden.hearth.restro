# Product Requirements Document: Restaurant Billing & Management Software

| | |
|---|---|
| Status | Draft v0.2 (scope change 2026-09-28: Phase 1 = billing + menu on the family laptop) |
| Date | 2026-09-28 |
| Owner | Developer (me) |
| Customer | Mama's restaurant (single outlet, new) |
| Sources | `docs/chatgpt-restaurant-software-planning.md`, feature deck (`Restaurant_Software_Features.pptx`), demo (`demo/restaurant-demo.html`), `Cost_Summary.pdf` |

---

## 1. Summary

> **Current scope (changed 2026-09-28):** for the opening, the family only needs **billing and menu management, installed on their own laptop, with bill printing**. Everything else in this document (kitchen tickets, tables, waiter phones, reports, QR ordering, online payment, WhatsApp bills) moves to later phases. See §4.

A billing and restaurant management system built for one new restaurant. Staff take orders at the counter or on phones, orders go to the kitchen instantly, and bills are GST-correct and printed or sent on WhatsApp. Customers can scan a QR code on the table to order and pay online, and anyone can view the menu from home.

Billing and kitchen orders must keep working when the internet is down. The system runs on a small server inside the restaurant, and a small cloud server handles everything a customer's phone touches.

## 2. Goals and non-goals

### Goals
1. Every bill is fast (under 30 seconds for a normal table) and GST-correct.
2. No lost or wrong kitchen orders: every item reaches the right kitchen section.
3. Customers can order and pay from the table without waiting for a waiter.
4. The owner knows each day's sales by payment method, the best-selling dishes and GST due, without manual counting.
5. Billing never stops because of the internet, and no data is lost if a computer breaks.
6. The owner pays no yearly licence fee.

### Non-goals (V1)
- Stock/inventory, recipes and food cost, purchases/suppliers (features 7–9): Later.
- Swiggy/Zomato integration (21), table booking (14), loyalty and offers engine (12, 15), delivery management (13): Later.
- Multiple branches (25).
- Native Play Store / App Store apps. Everything is a web app (installable PWA).
- Online takeaway/delivery ordering from the home menu (the home menu is view-only in V1).

## 3. Users and roles

| Role | Where | What they do |
|---|---|---|
| Owner | Counter PC, own phone | Everything; menu, prices, staff, settings, reports, cancel/refund approval |
| Manager | Counter PC | Billing, discounts within a limit, cancellations with a reason, day close |
| Cashier | Counter PC | Take orders, print and settle bills, take payments |
| Waiter | Phone | Take table orders, send them to the kitchen, request the bill |
| Kitchen | Kitchen printer / screen | See orders by section, mark them ready |
| Customer (table) | Own phone, via table QR | View the menu, order, pay online or at the counter, call a waiter |
| Visitor (home) | Own phone, via link/QR | View the menu only |

Staff log in with a 4–6 digit PIN. Each role sees only its own screens.

## 4. Releases

| Phase | Target | Contents |
|---|---|---|
| **Phase 1: Billing + menu (now)** | Ready on or before Navratri (about 11 Oct 2026) | Installed on the family's Windows laptop; works fully without internet. **Menu management:** MENU-1, 2, 3, 5, 6, 7, 8. **Billing:** dine-in (table number) and takeaway, several bills open at the same time, add items with Half/Full and quantity (ORD-2 simplified, ORD-3), GST bill (BILL-1, 2, 3, 4, 6, 8, 9), bill history with search and reprint, today's total. **Bill printing** on a thermal printer. **Settings:** SET-1, SET-2. **Safety:** owner PIN for menu changes, cancellations and discounts; automatic daily backup (SYS-3, to a pen drive or cloud folder). |
| **Phase 2: Restaurant operations** | After opening, when the family asks | Kitchen tickets (KOT), table map, move/merge tables, waiter phones, staff logins and activity log, full reports, day close, expenses, kitchen screen, online menu for home. |
| **Phase 3: Online** | Later | QR table ordering, online payment, WhatsApp bills, owner's phone view. Needs the payment gateway and WhatsApp approvals. |

In §5, requirements marked R0 or R1 that are not listed under Phase 1 belong to Phase 2; requirements marked R2 belong to Phase 3.

**Phase 1 day plan (from 29 Sep):**

| Dates | Work |
|---|---|
| 29–30 Sep | Project setup, database, menu management screens. **Family sends the menu, GST details, FSSAI no. and printer model; laptop details confirmed.** |
| 1–3 Oct | Billing screen: open bills, add items, GST calculation, discounts, bill numbering. |
| 4–5 Oct | Bill printing, reprint, cancellation with reason, bill history, settings. |
| 6 Oct | Backup, installer, full test with the real menu. **No new features after this.** |
| 7 Oct | Install on the family laptop, connect the printer, load the menu. |
| 8 Oct | Training and practice billing with the family. |
| 9–10 Oct | Fixes and buffer. |
| 11 Oct | Open. I stay available for the first days. |

Payment milestones: ₹25,000 at start, ₹25,000 when Phase 1 is live, ₹25,000 when Phase 3 is live. Support ₹2,000/month starts 3 months after Phase 1 goes live. (To be reviewed with the family for the smaller Phase 1 scope.)

## 5. Functional requirements

Priority: **M** = must have for that release, **S** = should have, **C** = could have.

### 5.1 Menu (feature 3) — R1

| ID | Requirement | P |
|---|---|---|
| MENU-1 | Categories with display order (Starters, Main Course, Breads, Rice, Drinks, Desserts, etc.). | M |
| MENU-2 | Item: name, category, price, veg / non-veg / egg, kitchen section, short description, optional photo. | M |
| MENU-3 | Variants with their own price (Half / Full, Small / Large). | M |
| MENU-4 | Add-ons / extras with price (extra cheese, extra butter). | S |
| MENU-5 | Mark an item "not available" instantly. This applies everywhere, including the QR menu, within 5 seconds. | M |
| MENU-6 | Item search by name and by short code (for example "PBM" for Paneer Butter Masala). | M |
| MENU-7 | Price changes apply only to new orders; old bills keep their prices. | M |
| MENU-8 | Tax setting per item (default 5%). | M |

### 5.2 Tables and orders (features 1, 2) — R1

| ID | Requirement | P |
|---|---|---|
| ORD-1 | Table map/grid with statuses: free, occupied, bill printed, paid online. Shows running amount and time seated. | M |
| ORD-2 | Order types: dine-in (table), takeaway, delivery (phone order, customer name + phone + address). | M |
| ORD-3 | Add items by tapping; change quantity; per-item note ("less spicy"). | M |
| ORD-4 | Move an order to another table; merge two tables. | M |
| ORD-5 | Remove a sent item only with a reason; manager PIN if the item was already sent to the kitchen. | M |
| ORD-6 | Number of guests on the order (optional). | C |

### 5.3 Kitchen orders / KOT (feature 4) — R1

| ID | Requirement | P |
|---|---|---|
| KOT-1 | "Send to kitchen" creates a KOT with only the new items, split by kitchen section (for example Main, Tandoor, Drinks). | M |
| KOT-2 | Each section's KOT prints on its own printer: KOT no., table, waiter, time, items, notes. | M |
| KOT-3 | Kitchen screen (optional per section): live list of tickets, oldest first, with "Ready" button and a sound on a new ticket. | S |
| KOT-4 | Cancelled items print a clear "CANCEL" KOT. | M |
| KOT-5 | Reprint a KOT. | M |
| KOT-6 | QR orders are marked "QR" and "PAID" / "PAY AT COUNTER" on the ticket. | M (R2) |

### 5.4 Billing and payments (feature 1) — R1

| ID | Requirement | P |
|---|---|---|
| BILL-1 | Bill shows: restaurant name, address, phone, GSTIN, FSSAI licence no., bill no., date/time, table/order type, cashier, items with qty/rate/amount, subtotal, discount, taxable value, CGST and SGST (rate and amount), round-off, grand total, SAC 996331 (to confirm with the CA), payment method. | M |
| BILL-2 | Bill numbers are consecutive and unique within a financial year (for example `2026-27/000123`), with no gaps. Numbering restarts every 1 April. | M |
| BILL-3 | Bills are never deleted. A cancelled bill stays with status "cancelled", a reason and who cancelled it. | M |
| BILL-4 | Discount by percent or amount, with a reason. Above a set limit, manager PIN is needed. | M |
| BILL-5 | Service charge only if the owner turns it on, and it must be optional for the customer (not added automatically), as per consumer rules. Packing charge for takeaway. | S |
| BILL-6 | Payment methods: cash (with change calculation), UPI, card, online (QR), and split across methods. | M |
| BILL-7 | Split the bill by items or into equal parts. | S |
| BILL-8 | Reprint any bill (marked "DUPLICATE"). | M |
| BILL-9 | Tax mode set in settings: regular GST (tax invoice, 5% = 2.5% CGST + 2.5% SGST) or composition (bill of supply, no tax). | M |
| BILL-10 | Customer phone on the bill (optional), used for the WhatsApp bill. | M |

### 5.5 Waiter phone app (feature 5) — R1

| ID | Requirement | P |
|---|---|---|
| WAIT-1 | Mobile layout: pick table → add items → send to kitchen. | M |
| WAIT-2 | Works on staff's Android phones over restaurant Wi-Fi; installable to the home screen. | M |
| WAIT-3 | Receives "customer is calling" alerts from the table QR. | S (R2) |

### 5.6 Staff and security (feature 11) — R1

| ID | Requirement | P |
|---|---|---|
| STAFF-1 | Add/disable staff, set role and PIN. | M |
| STAFF-2 | Activity log: who did what and when (cancellations, discounts, reprints, price changes, drawer opens). Only the owner can view it. | M |
| STAFF-3 | Automatic log-out on shared devices after inactivity. | S |

### 5.7 Reports and day close (features 10, 16, 17) — R1 (owner phone view R2)

| ID | Requirement | P |
|---|---|---|
| REP-1 | Today / date range: total sales, number of bills, average bill, by payment method (cash / UPI / card / online). | M |
| REP-2 | Item-wise sales and top sellers; category-wise sales. | M |
| REP-3 | GST report for the CA: taxable value, CGST, SGST per day and per month; export to Excel/CSV. | M |
| REP-4 | Cancelled bills, discounts and removed items report. | M |
| REP-5 | Day close: expected vs counted cash, difference noted. | S |
| REP-6 | Expenses: record daily spending by category; monthly total. | S |
| REP-7 | Owner's phone view of today's sales from anywhere (through the cloud). | S (R2) |

### 5.8 QR table ordering and online payment (feature 19) — R2

| ID | Requirement | P |
|---|---|---|
| QR-1 | Each table has a printed QR code with a unique link including a signed table key (cannot be guessed or changed to another table). | M |
| QR-2 | Customer sees the live menu (only available items), adds to cart, adds a note, places the order. No app or login needed. | M |
| QR-3 | Orders are accepted only while staff have the table open (or first order on a free table needs staff approval — decide, see §10). | M |
| QR-4 | Payment choice: pay online now, or pay at the counter. | M |
| QR-5 | Online payment via the payment gateway (UPI, cards). An order is marked paid **only** after the server verifies the payment (signature and webhook), never on the browser's word. | M |
| QR-6 | Paid QR orders go straight to the kitchen. Pay-at-counter orders go to the kitchen as well (configurable: or wait for staff approval). | M |
| QR-7 | Staff see QR orders on the table with a "QR" and "Paid online" tag. Staff can add items; the bill shows the amount already paid and the balance. | M |
| QR-8 | Customer can order again on the same table; all orders join the same table bill. | M |
| QR-9 | "Call waiter" button. | S |
| QR-10 | Refund (full or partial) from the POS by the owner, through the gateway. | M |
| QR-11 | If the internet is down, the QR page shows "Please order with the waiter". | M |
| QR-12 | Daily check: gateway settlements vs payments recorded. | S |

### 5.9 Online menu for home (part of feature 20) — R0 (static), R2 (live)

| ID | Requirement | P |
|---|---|---|
| WEB-1 | Mobile menu page: restaurant name, address, timings, categories, items, prices, veg marks. View-only, no ordering. | M |
| WEB-2 | "Call us" and "Directions" (Google Maps) buttons. | M |
| WEB-3 | R0: static page edited by me. R2: served from the live menu, so price changes and "not available" show automatically. | M |
| WEB-4 | Pages required by the payment gateway: About, Contact, Terms, Privacy, Refund & Cancellation. | M (before gateway KYC) |
| WEB-5 | Shareable link and QR for Google Maps, Instagram, WhatsApp and posters. | M |

### 5.10 WhatsApp bills (feature 6) — R2

| ID | Requirement | P |
|---|---|---|
| WA-1 | After a bill is paid, and the customer phone is entered, send the bill as a PDF on WhatsApp from the restaurant's number, using an approved utility template (WhatsApp Cloud API). | M |
| WA-2 | QR customers get the bill automatically if they gave their number. | S |
| WA-3 | Fallback when the API is unavailable: "Send on WhatsApp" button that opens WhatsApp with the bill link. | M |
| WA-4 | Status shown on the bill (sent / delivered / failed) with a resend option. | S |
| WA-5 | Customer consent is recorded when the phone number is taken. | M |

### 5.11 Settings (feature 24) — R1

| ID | Requirement | P |
|---|---|---|
| SET-1 | Restaurant name, logo, address, phone, GSTIN, FSSAI no., bill footer text. | M |
| SET-2 | Tax mode and rates, service charge (on/off), packing charge. | M |
| SET-3 | Tables (number, names, areas). | M |
| SET-4 | Kitchen sections and which printer each uses; bill printer. | M |
| SET-5 | Discount limit without manager PIN. | S |

### 5.12 Offline, backup and printing (features 22, 23) — R1

| ID | Requirement | P |
|---|---|---|
| SYS-1 | Billing, KOT, printing and reports work with no internet (local server on the restaurant Wi-Fi/LAN). | M |
| SYS-2 | When the internet returns, data syncs to the cloud automatically; nothing is lost or duplicated. | M |
| SYS-3 | Automatic backups: local copy every hour; encrypted cloud copy every night; keep 30 days. | M |
| SYS-4 | Tested restore procedure: a new PC can be set up from the cloud backup in under 2 hours. | M |
| SYS-5 | Network thermal printers (ESC/POS, 80 mm); cash drawer opens from the bill printer. | M |
| SYS-6 | If a printer is offline, the ticket is kept in a queue with an alert and retried. | M |

## 6. Non-functional requirements

| Area | Requirement |
|---|---|
| Speed | Adding an item or opening a table responds in under 0.5 s on the LAN. A KOT prints within 3 s of "Send". QR order reaches the kitchen within 5 s. |
| Availability | The local system works 100% without internet. UPS keeps the server and bill printer running through short power cuts. |
| Devices | Counter: Windows PC or large tablet (Chrome/Edge). Waiters: Android phones (Chrome). Customers: any phone browser. |
| Language | English UI for V1; menu names can be in English and Hindi. Hindi UI: Later. |
| Security | HTTPS everywhere; staff PINs stored hashed; roles enforced on the server; signed table QR keys; payment verified server-side; secrets never in the browser; the local server is not open to the internet (it connects out to the cloud). |
| Data and privacy | Customer phone numbers are stored only with consent and used only for bills (DPDP Act). Data belongs to the restaurant. |
| Records | Bills and GST data kept at least 6 years (GST record-keeping rules; confirm with CA). |
| Usability | A new cashier can make a bill after 15 minutes of training. Large buttons, touch-friendly. |

## 7. Architecture (summary)

**Phase 1 (one laptop):**

- One Windows laptop runs everything. The app is a .NET 10 local service with a React + TypeScript screen, opened as an installed app window; it starts automatically with Windows.
- Database: SQLite, a single file on the laptop (no database server to install or maintain). It can move to MySQL later if Phase 2 needs it.
- Printing: bills are sent as ESC/POS commands through the Windows printer driver, so a USB or LAN thermal printer both work.
- Backup: the database is copied automatically every day (and when the app closes) to a pen drive and/or a cloud folder such as Google Drive; 30 days kept.
- No internet needed. Built so Phase 2 can let phones and a kitchen screen connect to the laptop over Wi-Fi.

**Phase 2 and 3 (full system):**

- **Local server** (mini PC at the restaurant): .NET 10 API, MySQL, SignalR for live updates to the kitchen and waiters, print service for network printers. Staff apps (React + TypeScript PWA) are served from here over Wi-Fi.
- **Cloud server** (small VPS in India): .NET 10 API + database for the QR ordering pages, home menu, payment gateway webhooks, WhatsApp sending, owner phone view and backups.
- **Sync:** the local server keeps an outbound, authenticated connection to the cloud. It pushes menu changes and table status up; the cloud pushes QR orders and payment confirmations down. No port forwarding at the restaurant.
- **Integrations:** payment gateway (Razorpay / Cashfree / PhonePe PG — choose in §10), WhatsApp Cloud API (Meta), Google Maps link.
- Details go in a separate technical design document.

## 8. Main data (outline)

Restaurant settings · Staff (role, PIN hash) · Category · Item · Variant · Add-on · Table · Order · Order line (item, variant, qty, price, note, KOT id, status) · KOT · Bill (number, FY, totals, tax, status) · Payment (method, amount, gateway ref, status) · Refund · Customer (phone, consent) · WhatsApp message · Expense · Activity log · Sync queue.

## 9. Success measures (first month after go-live)

- 0 lost KOTs and 0 wrong bill totals reported.
- Average bill settle time under 30 s.
- At least 20% of dine-in orders placed through the table QR.
- At least 50% of bills sent on WhatsApp.
- Day-close cash difference explained every day.
- 0 hours of billing stopped because of internet problems.

## 10. Open questions (need answers from the family / CA)

| # | Question | Needed for |
|---|---|---|
| 1 | Restaurant name, logo, address, phone, opening hours. | R0 |
| 2 | Full menu: categories, items, prices, Half/Full, veg/non-veg, photos (optional). | R0 |
| 3 | Number of tables and their names/areas (AC hall, outdoor, etc.). | R1 |
| 4 | GSTIN; regular GST or composition scheme? Correct SAC and rate — confirm with the CA. | R1 |
| 5 | FSSAI licence number. | R1 |
| 6 | Kitchen sections and number of printers (for example Main + Tandoor + Drinks). | R1 |
| 7 | Service charge: yes or no? Packing charge for takeaway: how much? | R1 |
| 8 | Discount limit a cashier can give without the owner. | R1 |
| 9 | Their final choices on the feature sheet (Need Now / Later / Not Needed). | R1 scope |
| 10 | Which payment gateway? (compare current fees; KYC needs PAN, GST, current account, website policy pages). | R2 |
| 11 | QR orders on a free table: allow straight away, or only after a waiter opens the table? | R2 |
| 12 | Phone number to use for WhatsApp Business (must be verified with Meta). | R2 |
| 13 | Domain name choice. | R0 |
| 14 | Laptop details: Windows version, RAM, free disk space. Is it used for other work too? | Phase 1 |
| 15 | Printer: thermal 80 mm or 58 mm, USB or LAN? Model name if already bought. | Phase 1 |
| 16 | How many bills are open at the same time at peak (number of tables)? | Phase 1 |
| 17 | Who is allowed to change menu prices and cancel bills (owner PIN)? | Phase 1 |

## 11. Risks

| Risk | Effect | Plan |
|---|---|---|
| Only about 12 days to build Phase 1 | Bugs on opening day during the festival rush | Keep Phase 1 to billing + menu only; freeze features on 6 Oct; practice billing on 8 Oct; numbered paper bill book as backup; developer available in opening week. |
| Menu or printer arrives late | Nothing to install or test | Menu and details needed by 30 Sep; printer by 3 Oct (test at home first). |
| Laptop is the only machine | A broken or stolen laptop loses the bills | Automatic daily backup to a pen drive and cloud folder; restore tested on another PC before opening; laptop kept on charger / UPS. |
| Payment gateway or WhatsApp approval is slow | R2 delayed | Apply in week 1; policy pages ready early; launch QR with "pay at counter" first if needed. |
| Wrong GST setup | Tax trouble | CA reviews a sample bill and GST report before go-live. |
| Printer or network problems during rush hours | Service stops | Network printers, print queue with retry, UPS, 4G backup, spare paper. |
| Scope grows during build | Delays | Changes after R1 sign-off go to "Later" or are priced separately. |
| Single developer | Support gaps | Written setup and restore guide; remote access for support. |

## 12. Acceptance for each phase

- **Phase 1:** 50 test bills with the real menu (dine-in and takeaway, discounts, one cancellation); totals, GST and bill numbers correct and a sample checked by the CA; every bill prints correctly and can be reprinted; works with the internet off and after a laptop restart; backup restored on a second PC.
- **R1:** a full practice service (at least 30 bills across dine-in, takeaway and delivery) with printed KOTs and bills; GST report matches the bills; internet unplugged for 1 hour with no problems; backup restored on a second PC.
- **R2:** 10 test QR orders (5 paid online in gateway test mode, 5 pay-at-counter), one refund, 10 WhatsApp bills delivered; QR page falls back correctly with internet off.

## 13. Key user flows

**Dine-in with waiter (R0)**
1. Waiter or cashier opens a free table and adds items (with Half/Full and notes).
2. "Send to kitchen": a KOT prints at each kitchen section; the table turns occupied.
3. More items later: only the new items print as a new KOT.
4. Customer asks for the bill: cashier prints it; the table shows "bill printed".
5. Customer pays by cash, card or the counter UPI QR; cashier records the method; the table becomes free.

**Takeaway / phone order (R0)**
1. Cashier creates a takeaway or delivery order (name and phone; address for delivery).
2. Items are sent to the kitchen; packing charge added if set.
3. Bill printed and paid at pickup or on delivery.

**QR table order with online payment (R2)**
1. Customer scans the table QR; the live menu opens in the phone browser.
2. Customer adds items and a note, chooses "Pay now" and pays by UPI or card.
3. The cloud server verifies the payment with the gateway, then sends the order to the restaurant server.
4. KOT prints marked "QR · PAID"; staff see the order on the table with "Paid online".
5. Anything added later by staff is shown as the balance; the table is closed once the balance is zero.
6. The bill is sent on WhatsApp if the customer gave their number.

**Day close (R1)**
1. Manager counts the cash and enters it; the system shows expected vs counted.
2. Day report printed or viewed: sales by method, top items, cancellations, discounts.

## 14. Screens

| Screen | Used by | Release |
|---|---|---|
| PIN login | All staff | R0 |
| Tables (grid with status) | Cashier, waiter | R0 |
| Order / billing (menu + current order + totals) | Cashier, waiter | R0 |
| Bill preview and payment | Cashier | R0 |
| Kitchen screen | Kitchen | R1 |
| Menu management | Owner, manager | R0 |
| Reports | Owner, manager | R0 (basic), R1 (full) |
| Settings (restaurant, tax, tables, printers, staff) | Owner | R0 |
| Activity log, day close, expenses | Owner, manager | R1 |
| QR codes (print table and home-menu QR) | Owner | R0 (home menu), R2 (tables) |
| Customer QR menu, cart, payment, order status | Customer | R2 |
| Home menu (view-only) | Visitor | R0 (static), R2 (live) |

The clickable demo (`demo/restaurant-demo.html`) shows the intended look of these screens.

## 15. Hardware and accounts

**Phase 1 needs only:** the family's Windows laptop (Windows 10/11, 8 GB RAM recommended), one thermal bill printer (80 mm, USB is fine; about ₹6–10k), a pen drive for backups, and optionally a cash drawer. Everything below is for Phase 2 and 3.

| Item | Needed by |
|---|---|
| Mini PC server (i5, 16 GB RAM, SSD) + UPS | Install day (7 Oct) |
| 80 mm thermal bill printer with LAN port + cash drawer | One printer by 1–2 Oct for testing |
| One 80 mm LAN thermal printer per kitchen section | Install day |
| Counter screen (touch preferred) | Install day |
| Wi-Fi router covering the whole dining area; broadband + 4G backup | Install day |
| Waiter phones (staff's own Android phones are fine) | Install day |
| Kitchen tablet/TV (optional) | R1 |
| Domain name | R0 (online menu) |
| Cloud server (India region) | R0 for the menu page (static hosting is enough), R2 for full use |
| Payment gateway account in the restaurant's name | Apply in week 1; needed for R2 |
| WhatsApp Business (Meta) verification + phone number | Apply in week 1; needed for R2 |

## 16. Glossary

| Term | Meaning |
|---|---|
| POS | Point of sale: the billing software at the counter |
| KOT | Kitchen order ticket: the slip that tells the kitchen what to cook |
| GSTIN | The restaurant's GST registration number |
| CGST / SGST | Central and state parts of GST, each half of the total rate |
| Composition scheme | A simpler GST scheme where the restaurant pays a fixed rate and does not charge GST on the bill |
| SAC | Service code printed on the tax invoice |
| FSSAI | Food safety licence; its number must be printed on bills |
| Payment gateway | The company that takes online payments (UPI/cards) and sends the money to the bank |
| Webhook | A message from the payment gateway to our server confirming a payment |
| PWA | A website that can be added to the phone's home screen and works like an app |
| LAN | The restaurant's own local network (Wi-Fi and cables) |
