# Restaurant POS

Billing and menu software for one new family restaurant in India. The restaurant opens on or before Navratri (about 11 Oct 2026), so **Phase 1 must be finished and tested by 6 Oct 2026**.

Full requirements: `docs/PRD.md` (source of truth; the .docx/.pdf in `docs/` are generated from it). Requirement IDs like `MENU-5` or `BILL-2` refer to that file.

## Current scope: Phase 1 only

Installed on the family's Windows laptop, fully offline, one user at a time.

- **Menu management:** categories, items, Half/Full variants with their own price, veg/non-veg/egg, "not available" switch, search by name or short code.
- **Billing:** dine-in (table number) and takeaway; several bills open at once; add items with variant, quantity and note; discount (percent or amount, with reason); GST calculation; payment by cash / UPI / card (split allowed); bill history with search and reprint; today's total.
- **Bill printing** on an 80 mm thermal printer (ESC/POS through the Windows printer driver).
- **Settings:** restaurant name, address, phone, GSTIN, FSSAI no., tax mode, bill footer.
- **Safety:** owner PIN for menu changes, cancellations and discounts; automatic daily backup of the database file.

Do **not** build Phase 2/3 features (kitchen tickets, table map, waiter phones, staff logins, reports beyond today's total, QR ordering, online payment, WhatsApp) unless asked.

## Stack and layout

| Path | What |
|---|---|
| `src/RestaurantPos.Api` | ASP.NET Core (.NET 10) minimal API, EF Core + SQLite. Serves the built frontend from `wwwroot`. Listens on `http://localhost:5080`. |
| `src/RestaurantPos.Api/Data` | Entities (`Entities.cs`), `PosDbContext`, EF migrations (`Data/Migrations`). |
| `src/web` | React 19 + TypeScript + Vite. `npm run build` outputs to the API's `wwwroot`. |
| `tests/RestaurantPos.Tests` | xUnit tests; `ApiFactory` runs the API against a throwaway SQLite file. |
| `docs/` | PRD, planning transcript. |
| `demo/restaurant-demo.html` | Clickable UI demo shown to the family — the intended look and flow. |

The database file lives at `%LOCALAPPDATA%\RestaurantPos\pos.db` unless `ConnectionStrings:Pos` is set.

## Commands

```bash
dotnet tool restore                          # once: restores dotnet-ef
dotnet build
dotnet test
dotnet run --project src/RestaurantPos.Api   # API on :5080 (applies migrations on start)

cd src/web
npm install
npm run dev      # UI on :5173, /api is proxied to :5080
npm run build    # type-check + build into the API's wwwroot
npm run lint

# After changing entities:
dotnet ef migrations add <Name> --project src/RestaurantPos.Api -o Data/Migrations
```

Before finishing any task: `dotnet build`, `dotnet test`, and `npm run build` + `npm run lint` in `src/web` must all pass.

## Rules that must not be broken

- **Money is whole paise (`long`)** in the database, API and calculations. Convert to rupees only for display. Never use `double`/`float` for money. Rates are basis points (`500` = 5%).
- **GST (regular mode):** taxable value = subtotal − discount; CGST and SGST are each half of the GST rate (2.5% + 2.5% by default), each rounded to the nearest paisa; the grand total is rounded to the nearest rupee and the difference is shown as "Round off". **Composition mode:** no tax lines, the document is titled "Bill of Supply" instead of "Tax Invoice".
- **Bill numbers** are consecutive with no gaps within a financial year (1 April – 31 March), formatted `2026-27/000123`. A number is assigned only when a bill is finalised (first print), inside a transaction, so open or abandoned orders never use up a number.
- **Bills are never deleted.** Cancelling keeps the bill with status `Cancelled`, the reason and the time. Menu items used on bills are deactivated, never deleted.
- **Bill lines copy the item name, variant name and price** at the time of ordering, so menu changes never alter old bills.
- The printed bill shows: restaurant name, address, phone, GSTIN, FSSAI no., bill no., date/time, table or "Takeaway", items (qty, rate, amount), subtotal, discount, taxable value, CGST/SGST with rate and amount, round off, grand total, payment method(s), footer. Reprints are marked "DUPLICATE".
- **Schema changes go through EF migrations** (the app runs `Database.Migrate()` at start-up). Never edit an existing migration after it has been committed.
- **Everything works offline:** no CDN fonts, scripts or images; bundle all assets.
- Money, GST, rounding and bill-numbering logic must have unit tests.

## UI guidelines

Staff are not technical and bill during a festival rush. Large touch-friendly buttons, simple English labels, prices as `₹1,234.50` (Indian digit grouping), the fewest taps possible, and keyboard shortcuts for the counter (search box focus, quick quantity). Follow the look and flow of `demo/restaurant-demo.html`.

## Claude Code on the web

`scripts/cloud-setup.sh` installs the .NET 10 SDK and restores packages on the Linux cloud machine. Printing to a real printer, the Windows installer/auto-start, and checks on the family laptop can only be done locally on Windows. In the cloud, test print output by asserting on the generated ESC/POS bytes or a text preview.
