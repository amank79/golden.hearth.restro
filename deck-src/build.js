const pptxgen = require("pptxgenjs");
const React = require("react");
const ReactDOMServer = require("react-dom/server");
const sharp = require("sharp");
const md = require("react-icons/md");
const fa = require("react-icons/fa");

const OUT = "C:/tools/restaurant-software/Restaurant_Software_Features.pptx";

// Palette
const DARK = "2A1E17";   // espresso
const ACCENT = "D9822B"; // turmeric
const CARD = "F7F2ED";
const TEXT = "2A1E17";
const MUTED = "5E534B";
const WHITE = "FFFFFF";
const NOW = "2E7D4F";
const LATER = "B7790E";
const NO = "7A7A7A";
const HEAD = "Cambria";
const BODY = "Calibri";

async function iconPng(Comp, color, size = 256) {
  if (!Comp) throw new Error("missing icon");
  const svg = ReactDOMServer.renderToStaticMarkup(
    React.createElement(Comp, { color: "#" + color, size: String(size) })
  );
  const buf = await sharp(Buffer.from(svg)).png().toBuffer();
  return "image/png;base64," + buf.toString("base64");
}

// Icon inside a filled circle
async function iconCircle(slide, Comp, x, y, d, circleColor, iconColor) {
  slide.addShape("ellipse", { x, y, w: d, h: d, fill: { color: circleColor }, line: { color: circleColor } });
  const pad = d * 0.22;
  slide.addImage({ data: await iconPng(Comp, iconColor), x: x + pad, y: y + pad, w: d - 2 * pad, h: d - 2 * pad });
}

const FEATURES = [
  // Daily work
  { n: 1, name: "Billing", icon: md.MdReceiptLong, d: "Make bills for dine-in, takeaway and delivery. GST bill, discounts, split bill, and cash / UPI / card payment." },
  { n: 2, name: "Table Management", icon: md.MdTableRestaurant, d: "See at a glance which tables are free or busy. Move a table's order or join two tables easily." },
  { n: 3, name: "Menu", icon: md.MdMenuBook, d: "Add dishes and prices, half / full plates and extras. Mark a dish \"not available\" anytime." },
  { n: 4, name: "Kitchen Orders (KOT)", icon: md.MdOutdoorGrill, d: "Each order goes straight to the kitchen as a printed slip or on a kitchen screen. Fewer mistakes." },
  { n: 5, name: "Waiter Orders on Phone", icon: md.MdTabletAndroid, d: "Waiters take the order at the table on a phone or tablet. It reaches the kitchen instantly." },
  { n: 6, name: "Bill on WhatsApp", icon: fa.FaWhatsapp, d: "Send the bill to the customer's phone, instead of or along with the paper bill." },
  // Money & stock
  { n: 7, name: "Stock / Inventory", icon: md.MdInventory2, d: "Know how much paneer, chicken, oil, rice etc. is left. Get a warning before anything runs out." },
  { n: 8, name: "Recipe & Food Cost", icon: md.MdCalculate, d: "Know how much each dish costs to make and how much profit it gives." },
  { n: 9, name: "Purchases & Suppliers", icon: md.MdLocalShipping, d: "Record what you buy and from whom, and how much money is still to be paid to each supplier." },
  { n: 10, name: "Expenses", icon: md.MdAccountBalanceWallet, d: "Note daily spending like rent, electricity, gas, salary and repairs. See the monthly total." },
  // People & customers
  { n: 11, name: "Staff Logins", icon: md.MdBadge, d: "Separate login for owner, manager, cashier, waiter and kitchen. Each sees only their work. Know who cancelled a bill." },
  { n: 12, name: "Customer Records", icon: md.MdPeople, d: "Save regular customers' name and phone number, what they usually order, and reward points." },
  { n: 13, name: "Home Delivery", icon: md.MdDeliveryDining, d: "Delivery orders with customer address, delivery charge and which delivery boy took it." },
  { n: 14, name: "Table Booking", icon: md.MdEventSeat, d: "Book a table in advance for a date, time and number of guests." },
  { n: 15, name: "Offers & Discounts", icon: md.MdLocalOffer, d: "Combo meals, festival offers, happy hours and coupon codes." },
  // Reports
  { n: 16, name: "Sales Reports", icon: md.MdBarChart, d: "Today's and monthly sales, cash vs UPI vs card, best-selling dishes, GST report, profit estimate." },
  { n: 17, name: "Owner's Phone Dashboard", icon: md.MdPhoneIphone, d: "Check today's sales, expenses and stock on your phone from anywhere. Needs internet." },
  { n: 18, name: "Alerts & Reminders", icon: md.MdNotificationsActive, d: "Reminders for low stock and pending supplier payments, plus a daily sales summary." },
  // Online
  { n: 19, name: "QR Code Ordering", icon: md.MdQrCode2, d: "Customer scans a QR code on the table, sees the menu and orders from their own phone." },
  { n: 20, name: "Our Own Online Orders", icon: md.MdLanguage, d: "Customers order takeaway or delivery from the restaurant's own online menu and pay online." },
  { n: 21, name: "Swiggy / Zomato", icon: md.MdTwoWheeler, d: "Swiggy and Zomato orders come into the same software, so no separate tablets to watch." },
  // Safety & setup
  { n: 22, name: "Works Without Internet", icon: md.MdWifiOff, d: "Billing and kitchen orders keep working even when the internet is down." },
  { n: 23, name: "Automatic Backup", icon: md.MdBackup, d: "All data is copied safely every day, so nothing is lost if the computer breaks." },
  { n: 24, name: "Restaurant Settings", icon: md.MdSettings, d: "Restaurant name and logo on the bill, GST number, taxes, service and packing charges, printers." },
  { n: 25, name: "More Branches (Future)", icon: md.MdStore, d: "If you open another restaurant later, manage all branches from one place." },
];
const byN = (n) => FEATURES.find((f) => f.n === n);

const GROUPS = [
  { title: "Daily Restaurant Work", nums: [1, 2, 3, 4, 5, 6], note: "The everyday basics: taking orders, the kitchen and the bill." },
  { title: "Money & Stock", nums: [7, 8, 9, 10], note: "Keep track of what comes in, what goes out and what is left." },
  { title: "Staff & Customers", nums: [11, 12, 13, 14, 15], note: "Your team, your regular customers and special offers." },
  { title: "Reports & Alerts", nums: [16, 17, 18], note: "Know how the business is doing, even from home." },
  { title: "Online Orders", nums: [19, 20, 21], note: "Get orders from customers' phones and delivery apps." },
  { title: "Safety & Setup", nums: [22, 23, 24, 25], note: "Keeps the restaurant running and your data safe." },
];

function header(slide, label, title) {
  slide.addText(label.toUpperCase(), { x: 0.6, y: 0.4, w: 12, h: 0.35, fontFace: BODY, fontSize: 13, bold: true, color: ACCENT, charSpacing: 3, margin: 0, isTextBox: true });
  slide.addText(title, { x: 0.6, y: 0.75, w: 12, h: 0.75, fontFace: HEAD, fontSize: 34, bold: true, color: TEXT, margin: 0, isTextBox: true });
}

async function build() {
  const pres = new pptxgen();
  pres.layout = "LAYOUT_WIDE"; // 13.333 x 7.5
  pres.title = "Restaurant Software Features";

  // 1. Title
  {
    const s = pres.addSlide();
    s.background = { color: DARK };
    s.addText("FOR OUR NEW RESTAURANT", { x: 0.8, y: 1.9, w: 7.5, h: 0.4, fontFace: BODY, fontSize: 16, bold: true, color: ACCENT, charSpacing: 4, margin: 0, isTextBox: true });
    s.addText("Restaurant Billing & Management Software", { x: 0.8, y: 2.4, w: 7.6, h: 1.9, fontFace: HEAD, fontSize: 44, bold: true, color: WHITE, margin: 0, valign: "top", isTextBox: true });
    s.addText("Please go through the features and choose what you need.", { x: 0.8, y: 4.5, w: 7.4, h: 0.9, fontFace: BODY, fontSize: 22, color: "E9DFD6", margin: 0, valign: "top", isTextBox: true });
    await iconCircle(s, md.MdRestaurant, 9.1, 1.9, 3.3, ACCENT, DARK);
    s.addNotes("This presentation lists everything the restaurant software can do. Nothing is fixed yet: you choose what you need.");
  }

  // 2. What it does
  {
    const s = pres.addSlide();
    s.background = { color: WHITE };
    header(s, "In simple words", "What the software does");
    const steps = [
      { icon: md.MdRoomService, t: "Take the order", d: "At the counter or at the table" },
      { icon: md.MdOutdoorGrill, t: "Kitchen gets it", d: "Printed slip or kitchen screen" },
      { icon: md.MdReceiptLong, t: "Make the bill", d: "Correct total with GST, in seconds" },
      { icon: md.MdPayments, t: "Take payment", d: "Cash, UPI or card" },
      { icon: md.MdBarChart, t: "See the reports", d: "Sales, expenses, profit" },
    ];
    const w = 2.1, gap = (12.13 - 5 * w) / 4, y = 2.3, d = 1.3;
    for (let i = 0; i < steps.length; i++) {
      const x = 0.6 + i * (w + gap);
      await iconCircle(s, steps[i].icon, x + (w - d) / 2, y, d, CARD, ACCENT);
      s.addText(steps[i].t, { x, y: y + 1.5, w, h: 0.5, fontFace: BODY, fontSize: 19, bold: true, color: TEXT, align: "center", margin: 0, isTextBox: true });
      s.addText(steps[i].d, { x, y: y + 2.0, w, h: 0.8, fontFace: BODY, fontSize: 15, color: MUTED, align: "center", valign: "top", margin: 0, isTextBox: true });
      if (i < steps.length - 1) {
        s.addImage({ data: await iconPng(md.MdArrowForward, "C9B8A8"), x: x + w + gap / 2 - 0.22, y: y + d / 2 - 0.22, w: 0.44, h: 0.44 });
      }
    }
    s.addShape("roundRect", { x: 0.6, y: 5.75, w: 12.13, h: 0.9, fill: { color: CARD }, line: { color: CARD }, rectRadius: 0.12 });
    s.addText("Everything in one place, on the counter computer, a tablet or a phone.", { x: 0.9, y: 5.75, w: 11.5, h: 0.9, fontFace: BODY, fontSize: 18, color: TEXT, valign: "middle", margin: 0, isTextBox: true });
    s.addNotes("This is the basic flow of every order. All the other features add on top of this.");
  }

  // 3. How to choose
  {
    const s = pres.addSlide();
    s.background = { color: WHITE };
    header(s, "How to choose", "For each feature, pick one of three");
    const opts = [
      { c: NOW, icon: md.MdCheckCircle, t: "Need Now", d: "Must have from the day the restaurant opens." },
      { c: LATER, icon: md.MdSchedule, t: "Later", d: "Nice to have. Can be added after opening." },
      { c: NO, icon: md.MdCancel, t: "Not Needed", d: "We don't need this. Skip it." },
    ];
    const w = 3.84, gap = 0.3, y = 1.95, h = 2.8;
    for (let i = 0; i < 3; i++) {
      const x = 0.6 + i * (w + gap);
      s.addShape("roundRect", { x, y, w, h, fill: { color: CARD }, line: { color: CARD }, rectRadius: 0.15 });
      await iconCircle(s, opts[i].icon, x + 0.35, y + 0.35, 0.9, opts[i].c, WHITE);
      s.addText(opts[i].t, { x: x + 0.35, y: y + 1.4, w: w - 0.7, h: 0.55, fontFace: HEAD, fontSize: 26, bold: true, color: opts[i].c, margin: 0, isTextBox: true });
      s.addText(opts[i].d, { x: x + 0.35, y: y + 1.95, w: w - 0.7, h: 0.7, fontFace: BODY, fontSize: 16, color: TEXT, valign: "top", margin: 0, isTextBox: true });
    }
    await iconCircle(s, md.MdLightbulb, 0.6, 5.3, 0.8, ACCENT, WHITE);
    s.addText([
      { text: "Tip: start small. ", options: { bold: true } },
      { text: "Fewer features means the software is ready sooner and costs less. Anything marked \"Later\" can be added whenever you want." },
    ], { x: 1.65, y: 5.2, w: 11.0, h: 1.0, fontFace: BODY, fontSize: 18, color: TEXT, valign: "middle", margin: 0, isTextBox: true });
    s.addNotes("Every feature has a number, 1 to 25. At the end there is a selection sheet, or you can simply reply on WhatsApp with the numbers.");
  }

  // 4-9. Feature groups
  for (const g of GROUPS) {
    const s = pres.addSlide();
    s.background = { color: WHITE };
    const first = g.nums[0], last = g.nums[g.nums.length - 1];
    header(s, `Features ${first} – ${last}`, g.title);
    s.addText(g.note, { x: 0.6, y: 1.5, w: 12, h: 0.45, fontFace: BODY, fontSize: 17, color: MUTED, margin: 0, isTextBox: true });
    const n = g.nums.length;
    const cols = n === 4 ? 2 : 3;
    const rows = Math.ceil(n / cols);
    const gap = 0.3, top = 2.2, bottom = 7.0;
    const w = (12.13 - (cols - 1) * gap) / cols;
    const h = (bottom - top - (rows - 1) * gap) / rows;
    for (let i = 0; i < n; i++) {
      const f = byN(g.nums[i]);
      const r = Math.floor(i / cols), c = i % cols;
      // Centre a short last row
      const inRow = Math.min(cols, n - r * cols);
      const offset = ((cols - inRow) * (w + gap)) / 2;
      const x = 0.6 + offset + c * (w + gap), y = top + r * (h + gap);
      s.addShape("roundRect", { x, y, w, h, fill: { color: CARD }, line: { color: CARD }, rectRadius: 0.12 });
      if (rows === 1) {
        // Tall single-row cards: big icon on top, larger text
        await iconCircle(s, f.icon, x + 0.4, y + 0.45, 1.3, ACCENT, WHITE);
        s.addText([
          { text: `${f.n}. `, options: { color: ACCENT } },
          { text: f.name, options: { color: TEXT } },
        ], { x: x + 0.4, y: y + 2.0, w: w - 0.8, h: 0.9, fontFace: BODY, fontSize: 23, bold: true, valign: "top", margin: 0, isTextBox: true });
        s.addText(f.d, { x: x + 0.4, y: y + 2.95, w: w - 0.8, h: h - 3.2, fontFace: BODY, fontSize: 18, color: TEXT, valign: "top", margin: 0, isTextBox: true });
        continue;
      }
      await iconCircle(s, f.icon, x + 0.25, y + 0.25, 0.75, ACCENT, WHITE);
      s.addText([
        { text: `${f.n}. `, options: { color: ACCENT } },
        { text: f.name, options: { color: TEXT } },
      ], { x: x + 1.15, y: y + 0.25, w: w - 1.35, h: 0.75, fontFace: BODY, fontSize: 19, bold: true, valign: "middle", margin: 0, isTextBox: true });
      s.addText(f.d, { x: x + 0.25, y: y + 1.15, w: w - 0.5, h: h - 1.3, fontFace: BODY, fontSize: 15, color: TEXT, valign: "top", margin: 0, isTextBox: true });
    }
  }

  // 10. Our suggestion
  {
    const s = pres.addSlide();
    s.background = { color: WHITE };
    header(s, "Our suggestion", "What we suggest for the start");
    const colsDef = [
      { c: NOW, t: "Start with these", nums: [1, 2, 3, 4, 10, 11, 16, 22, 23, 24] },
      { c: DARK, t: "Your choice", nums: [5, 6, 7, 8, 9, 12, 13, 14, 15, 18] },
      { c: LATER, t: "Better later", nums: [17, 19, 20, 21, 25] },
    ];
    const w = 3.84, gap = 0.3, y = 1.8;
    for (let i = 0; i < 3; i++) {
      const col = colsDef[i];
      const x = 0.6 + i * (w + gap);
      s.addShape("roundRect", { x, y, w, h: 0.65, fill: { color: col.c }, line: { color: col.c }, rectRadius: 0.1 });
      s.addText(col.t, { x: x + 0.25, y, w: w - 0.5, h: 0.65, fontFace: BODY, fontSize: 19, bold: true, color: WHITE, valign: "middle", margin: 0, isTextBox: true });
      const items = col.nums.map((n, k) => ({
        text: `${n}. ${byN(n).name}`,
        options: { breakLine: k < col.nums.length - 1 },
      }));
      s.addText(items, { x: x + 0.25, y: y + 0.85, w: w - 0.3, h: 4.4, fontFace: BODY, fontSize: 16, color: TEXT, valign: "top", paraSpaceAfter: 5, margin: 0, isTextBox: true });
    }
    s.addText("This is only a suggestion. The final choice is yours.", { x: 0.6, y: 6.6, w: 12, h: 0.4, fontFace: BODY, fontSize: 15, italic: true, color: MUTED, margin: 0, isTextBox: true });
    s.addNotes("Start with: what a restaurant needs on day one. Your choice: depends on how you want to run things. Better later: needs internet, online setup or extra partners, so it is easier after the restaurant is running.");
  }

  // 11-12. Selection sheet
  const halves = [FEATURES.slice(0, 13), FEATURES.slice(13)];
  for (let p = 0; p < 2; p++) {
    const s = pres.addSlide();
    s.background = { color: WHITE };
    header(s, `Selection sheet · page ${p + 1} of 2`, "Please tick one box for each feature");
    const hdr = (t, fill) => ({ text: t, options: { bold: true, color: WHITE, fill: { color: fill }, align: "center" } });
    const rows = [[
      { text: "#", options: { bold: true, color: WHITE, fill: { color: DARK }, align: "center" } },
      { text: "Feature", options: { bold: true, color: WHITE, fill: { color: DARK } } },
      hdr("Need Now", NOW), hdr("Later", LATER), hdr("Not Needed", NO),
    ]];
    halves[p].forEach((f, k) => {
      const fill = { color: k % 2 ? "FBF8F5" : WHITE };
      rows.push([
        { text: String(f.n), options: { align: "center", bold: true, color: ACCENT, fill } },
        { text: f.name, options: { fill } },
        { text: "", options: { fill } }, { text: "", options: { fill } }, { text: "", options: { fill } },
      ]);
    });
    s.addTable(rows, {
      x: 0.6, y: 1.65, w: 12.13, colW: [0.8, 5.83, 1.83, 1.83, 1.84],
      rowH: 0.34, fontFace: BODY, fontSize: 15, color: TEXT, valign: "middle",
      border: { type: "solid", pt: 0.75, color: "D9CFC6" },
    });
    s.addText("Or simply send the numbers on WhatsApp, for example:  1 Now,  7 Later,  21 Not needed", { x: 0.6, y: 6.75, w: 12.13, h: 0.4, fontFace: BODY, fontSize: 15, color: MUTED, margin: 0, isTextBox: true });
  }

  // 13. Next steps
  {
    const s = pres.addSlide();
    s.background = { color: DARK };
    s.addText("WHAT HAPPENS NEXT", { x: 0.6, y: 0.4, w: 12, h: 0.35, fontFace: BODY, fontSize: 13, bold: true, color: ACCENT, charSpacing: 3, margin: 0, isTextBox: true });
    s.addText("Four simple steps", { x: 0.6, y: 0.75, w: 12, h: 0.75, fontFace: HEAD, fontSize: 34, bold: true, color: WHITE, margin: 0, isTextBox: true });
    const steps = [
      { t: "You choose", d: "Mark each feature as Need Now, Later or Not Needed." },
      { t: "We finalise", d: "We fix the final feature list, the time needed and the cost." },
      { t: "We build & set up", d: "Software is made and installed in the restaurant with the printers." },
      { t: "Training & support", d: "We teach the staff how to use it and fix any problems." },
    ];
    const w = 2.8, gap = (12.13 - 4 * w) / 3, y = 2.2;
    for (let i = 0; i < 4; i++) {
      const x = 0.6 + i * (w + gap);
      s.addShape("ellipse", { x, y, w: 0.9, h: 0.9, fill: { color: ACCENT }, line: { color: ACCENT } });
      s.addText(String(i + 1), { x, y, w: 0.9, h: 0.9, fontFace: HEAD, fontSize: 30, bold: true, color: DARK, align: "center", valign: "middle", margin: 0, isTextBox: true });
      s.addText(steps[i].t, { x, y: y + 1.15, w, h: 0.5, fontFace: BODY, fontSize: 21, bold: true, color: WHITE, margin: 0, isTextBox: true });
      s.addText(steps[i].d, { x, y: y + 1.7, w, h: 1.3, fontFace: BODY, fontSize: 16, color: "E9DFD6", valign: "top", margin: 0, isTextBox: true });
    }
    s.addText("Thank you! Any question, just ask.", { x: 0.6, y: 6.0, w: 12, h: 0.6, fontFace: HEAD, fontSize: 24, italic: true, color: ACCENT, margin: 0, isTextBox: true });
  }

  await pres.writeFile({ fileName: OUT });
  console.log("wrote", OUT);
}

build().catch((e) => { console.error(e); process.exit(1); });
