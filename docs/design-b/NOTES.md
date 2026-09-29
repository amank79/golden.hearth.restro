# Design B: "Navy & Champagne"

An alternative look and layout for the POS screens, to compare with the current premium design on the
`ui-redesign` branch (design A: emerald / ivory / noir, Playfair Display + Inter, left rail).

Branch: `ui-redesign-b` (made from `ui-redesign`). Only `src/web` changed: no API, database, migration, money or
GST changes.

## The idea in plain English

Design B treats the counter screen like a well-run restaurant desk.

- **A navy frame.** The screen has a deep navy bar across the top with the restaurant's name as a gold wordmark
  (THE GOLDEN HEARTH / RESTAURANT) and the four screens as tabs. The navy comes back wherever money is decided:
  the total band and the Cash / UPI / Card buttons.
- **Champagne gold as the accent.** Gold is used sparingly: the active tab underline, the Print button, the
  quantity badges on dishes, the selected category marker and thin rules. It never carries body text.
- **The bill is a paper receipt.** The right-hand column looks like a receipt: warm paper, dashed separators, a
  torn edge, and under it a navy band with the total in large gold numbers and the four pay buttons. The band and
  the buttons never scroll away, however long the bill is.
- **Open bills are tickets.** Every open bill is a small ticket stub (with notches cut in its sides) in one row
  across the full width, right after the big Dine-in and Takeaway buttons. Printed bills have a dashed blue
  outline so staff can see who is waiting to pay.
- **Menu down the side.** Categories are a vertical list to the left of the dishes (like Petpooja and Toast),
  so the dish grid gets the whole middle of the screen.

## Palette

Three palettes share one set of colour tokens (`src/web/src/index.css`, top of the file). Components use only
the tokens; there are no raw colours in the `.tsx` files. The choice is in **Settings → Colours** and is saved on
the laptop.

| Token | Navy & Champagne (default, light) | Burgundy & Champagne (light) | Midnight (dark) |
|---|---|---|---|
| Page (`--bg`) | `#f3f1ec` warm porcelain | `#f5f0eb` | `#090e1c` |
| Panels (`--panel`) | `#ffffff` | `#ffffff` | `#10172d` |
| Receipt paper (`--paper`) | `#fffdf8` | `#fffcf8` | `#131b34` |
| Text (`--ink`) | `#131a2c` navy ink | `#23161b` | `#eceef5` |
| Frame (top bar, dialog titles) | `#16244a → #0e1830` | `#561a2c → #3a0e1c` wine | `#0d1428 → #070b17` |
| Signature (`--brand`: total band, pay buttons, selected) | `#14213d` | `#5a1a2c` | `#1c2850` (pay buttons turn gold) |
| Gold text on the frame (`--gold-hi`) | `#e9d39e` champagne | `#e9d39e` | `#ecd49a` |
| Gold accent (`--gold`) | `#c6a25d` | `#c6a25d` | `#d2b06c` |
| Gold text on light surfaces (`--gold-text`) | `#86672a` | `#86672a` | `#e1c27f` |
| Veg / non-veg / egg marks | `#1f9d55` / `#d32f2f` / `#c98a0e` | same | brighter: `#3cc47a` / `#ff6b61` / `#f0b43a` |

In Midnight, the Dine-in / Takeaway and pay buttons become champagne gold with navy text, because navy buttons
disappear on a navy page.

**Fonts** (bundled from npm `@fontsource`, so everything works offline):
- **Marcellus**: classical Roman capitals, for the wordmark and word-only titles (page titles, panel headings,
  "No bill open").
- **Manrope**: everything else, including every number. Marcellus' zero looks like the letter O ("Table 1O",
  "2O26-27/OOOO12"), so table numbers, bill numbers and prices are always in Manrope.

Design A's fonts (Inter, Playfair Display) are removed from `package.json` on this branch.

## Layout changes compared with `ui-redesign` (design A)

| Area | Design A | Design B |
|---|---|---|
| Navigation | Dark left rail (86 px) with icons | Navy top bar with tabs; the full width goes to the screen |
| Status (backup, GSTIN, today's total, clock, keys) | Separate white status bar | Inside the top bar, right side |
| Open bills | Strip above the dishes (left side only) | Full-width row of ticket stubs under the top bar |
| Categories | Pills that wrap above the grid | Vertical list left of the grid, with counts |
| Dish tiles | 110 px, Half/Full as two small chips | 142 px, larger names, price strip with a +, Half/Full as two full-width halves of the tile bottom (52 px tall) |
| Bill panel | Card with a gold top edge | Paper receipt with a torn edge, then the navy total band and pay buttons |
| Bill tools | Small icon buttons in the header | A row of labelled 44 px buttons: Discount, Table, View, Cancel |
| Discount | "+ Add" link inside the totals | "Discount" button in the receipt's tool row (shows ✓ when one is applied) |
| Totals | One column | Two columns on the billing receipt (GST on its own row); one column in history |
| Quantity stepper | 31 px round buttons | 44 × 44 px square buttons |
| Bill history | Five stat cards + list + detail card | One navy "ledger" band for today's figures + list + the chosen bill as a receipt |
| Menu | Category pills above the table | Vertical category list left of the table; sizes as small price tags |
| Settings | Two columns | Section list on the left (Restaurant details, GST, Backup, Colours, About) + panels |
| Dialogs | White with a gold top edge, serif title | Navy title band with a gold rule |

The logic is identical. `BillTotals` gained a `hideGrand` option (the receipt shows the total in its own band);
`App.tsx`, the billing, history, menu and settings screens only changed their markup and class names.
`lib/brand.ts` (the two-line wordmark) is new and has a unit test; `lib/theme.ts` has the new palette ids, and
saved choices from design A carry over (Noir → Midnight, the light ones → Navy).

## Screenshots

`docs/design-b/screenshots/`, named `<screen>-<light|dark>-<width>x<height>.png`, at 1366×768 and 1536×864, in
Navy & Champagne (light) and Midnight (dark):

| Screen | File prefix |
|---|---|
| Billing with a bill of 10 dishes (with a discount) | `billing-bill` |
| Billing with no bill selected | `billing-no-bill` |
| Table picker (F3) | `table-picker` |
| Payment dialog (cash with change) | `payment` |
| Bill history with a bill selected | `history` |
| Menu | `menu` |
| Settings | `settings` |

## How it was checked

- `dotnet build`, `dotnet test` (167 tests), `npm run lint`, `npm test` (11 tests), `npm run build`: all pass.
- A headless-browser script (Playwright, kept outside the repo) went through every flow at 1536×864 in Navy and
  at 1366×768 in Midnight, 21 checks each, all passing with no page errors:
  - no bill → tap a dish → table picker with Takeaway → dish added;
  - F3 table picker, F4 takeaway, several open bills as tickets;
  - search by code with Enter, `2*bn`, arrow keys and Esc;
  - Half / Full one tap on the tile, H / F keys in the size picker;
  - quantity + / −, line note, 10% discount with reason;
  - F9 print (number on first print, bill locked, second print is DUPLICATE);
  - F8 cash with change, split Cash + UPI (auto-print after paying an unprinted bill);
  - change table, cancel with copy to a new bill;
  - F1 help;
  - history: filters, reprint DUPLICATE, cancel, open in Billing;
  - menu: category list, available switch, remove / bring back, dish and category editors, add a Half / Full dish;
  - settings: section list, back up now, colour choice, save.
- Bill lines fully visible: **8 at 1536×864** (7 when a discount adds totals rows), 6 at 1366×768.
- Every visible button, link and input on the seven screens is at least 44 px. The on/off switch and the status
  pills in the top bar look smaller, but have an invisible 44 px tap area.

## Things I was unsure about

1. **Many open bills.** The ticket row fits about 7 tickets at 1536 wide; more scroll sideways (swipe, or
   Shift + mouse wheel). Design A has the same limit in less space. If the restaurant often has 10+ open bills,
   a second row or a "more" menu might be better.
2. **1366×768.** Only 6 bill lines are fully visible there (the 7-line target was for 1536×864). The pay
   buttons and total always stay visible.
3. **Marcellus.** It gives the wordmark a classic, hotel-like feel, but its zero looks like an O. So it is only
   used for words, and the bill title ("Table 12") is in Manrope. If the family prefers a serif bill title, a
   serif with clear digits would be needed.
4. **Discount moved** from a link in the totals to a button in the receipt's tool row. Removing a discount is in
   the discount dialog, as before.
5. **Burgundy & Champagne** is offered as a second light palette. It is not in the screenshots (only Navy and
   Midnight are), to keep the comparison to light vs dark.
6. **Tile height is fixed** (142 px, 138 px on short screens) and dish names are cut to two lines. The grid did
   not grow a row when a long name wrapped inside the tile's button, so the size buttons spilled out. Very long
   names end with "…" on the grid; the full name is on the bill.
7. Checked only in headless Chromium on Linux, not on the Windows laptop, its touch screen or at Windows display
   scaling (125% / 150%). The layout is plain CSS grid/flex and should look the same in Edge, but please check
   it there.
