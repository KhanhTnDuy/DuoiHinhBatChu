// Sinh anh cau do "duoi hinh bat chu".
// Ten file xuat ra = dap an.
//
// Chay:  node tools/gen.js "duong/dan/thu/muc/xuat"
// Can:   npm install     (chay mot lan trong thu muc tools)
//
// NGUON ANH cho tung vat, uu tien tu tren xuong:
//   1. tools/nguyenlieu/<ten vat>.png|jpg|webp   <- anh BAN tu tim, dep nhat
//   2. icon OpenMoji (du phong, theo bang ICON ben duoi)
// Chi can tha file vao nguyenlieu la lan chay sau tu dong dung anh do.
// Ten file khong phan biet hoa thuong va khong phan biet dau.
//
// Moi phan tu trong "parts":
//   { img: 'con chó' }                     -> tra trong nguyenlieu, roi den OpenMoji
//   { text: 'FATHER', color: '#F2B705' }   -> ve chu
//
const fs = require('fs');
const path = require('path');
const { createCanvas, loadImage } = require('@napi-rs/canvas');

const W = 1280, H = 720;
const DIR_ICON = path.join(__dirname, 'icons');
const DIR_NGUYENLIEU = path.join(__dirname, 'nguyenlieu');
const OUT_DIR = process.argv[2] || path.join(__dirname, 'out');

// Phong cach mau (phuong an B - game show)
const BG_FROM = '#3B2FD6', BG_TO = '#8B4EE8';
const CARD = '#FFFFFF', CARD_SHADOW = 'rgba(20,10,60,0.45)';
const PLUS = '#FFD84D', TEXT_DEFAULT = '#2A2440';

// Icon OpenMoji du phong khi chua co anh that
const ICON = {
  'con cá': '1F41F', 'con heo': '1F437', 'con ngựa': '1F434', 'thanh kiếm': '1F5E1',
  'ngôi sao': '2B50', 'cây chổi': '1F9F9', 'mặt biển': '1F30A', 'con gấu': '1F43B',
  'bụi trúc': '1F38B', 'con ong': '1F41D', 'hũ mật': '1F36F', 'con mắt': '1F441',
  'cặp kính': '1F453', 'con chim': '1F426', 'con sâu': '1F41B', 'con chó': '1F415',
  'con bướm': '1F98B', 'mặt trăng': '1F319', 'quả dưa': '1F348', 'con chuột': '1F42D',
  'mặt trời': '2600', 'con rồng': '1F409', 'giọt nước': '1F4A7', 'con tàu': '1F6A2',
  'cần câu': '1F3A3', 'cuộn giấy': '1F4DC', 'bàn tay vẫy': '1F44B', 'con mèo': '1F408',
  'bông hoa': '1F338', 'khẩu súng': '1F52B', 'tờ báo': '1F4F0', 'cuộn chỉ': '1F9F5',
  'con rắn': '1F40D', 'con hổ': '1F405', 'ba lô': '1F392', 'quả cầu bói': '1F52E',
  'cục đá': '1FAA8',
};

const PUZZLES = [
  { answer: 'CÁ HEO',      parts: [{ img: 'con cá' },   { img: 'con heo' }] },
  { answer: 'CÁ NGỰA',     parts: [{ img: 'con cá' },   { img: 'con ngựa' }] },
  { answer: 'CÁ KIẾM',     parts: [{ img: 'con cá' },   { img: 'thanh kiếm' }] },
  { answer: 'SAO CHỔI',    parts: [{ img: 'ngôi sao' }, { img: 'cây chổi' }] },
  { answer: 'SAO BIỂN',    parts: [{ img: 'ngôi sao' }, { img: 'mặt biển' }] },
  { answer: 'GẤU TRÚC',    parts: [{ img: 'con gấu' },  { img: 'bụi trúc' }] },
  { answer: 'ONG MẬT',     parts: [{ img: 'con ong' },  { img: 'hũ mật' }] },
  { answer: 'MẮT KÍNH',    parts: [{ img: 'con mắt' },  { img: 'cặp kính' }] },
  { answer: 'CHIM SÂU',    parts: [{ img: 'con chim' }, { img: 'con sâu' }] },
  { answer: 'HẢI CẨU',     parts: [{ img: 'mặt biển' }, { img: 'con chó' }] },
  { answer: 'BƯỚM ĐÊM',    parts: [{ img: 'con bướm' }, { img: 'mặt trăng' }] },
  { answer: 'DƯA CHUỘT',   parts: [{ img: 'quả dưa' },  { img: 'con chuột' }] },
  { answer: 'HẠ LONG',     parts: [{ img: 'mặt trời' }, { img: 'con rồng' }] },
  { answer: 'VŨNG TÀU',    parts: [{ img: 'giọt nước' },{ img: 'con tàu' }] },
  { answer: 'CẦN THƠ',     parts: [{ img: 'cần câu' },  { img: 'cuộn giấy' }] },
  { answer: 'CHÀO MÀO',    parts: [{ img: 'bàn tay vẫy' }, { img: 'con mèo' }] },
  { answer: 'HOA SÚNG',    parts: [{ img: 'bông hoa' }, { img: 'khẩu súng' }] },
  { answer: 'BÁO GẤM',     parts: [{ img: 'tờ báo' },   { img: 'cuộn chỉ' }] },
  { answer: 'RẮN HỔ MANG', parts: [{ img: 'con rắn' },  { img: 'con hổ' }, { img: 'ba lô' }] },
  { answer: 'CHIM BÓI CÁ', parts: [{ img: 'con chim' }, { img: 'quả cầu bói' }, { img: 'con cá' }] },
  { answer: 'BỐ CỤC',      parts: [{ text: 'FATHER', color: '#F2B705' }, { img: 'cục đá' }] },
];

const LAYOUT = {
  2: { centers: [345, 935],       cardW: 470, cardH: 470, box: 350, slot: 380 },
  3: { centers: [203, 639, 1075], cardW: 326, cardH: 420, box: 262, slot: 268 },
};

// bo dau + thuong hoa, de ten file khong phai go dau cho chinh xac
function chuanHoa(s) {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd').replace(/Đ/g, 'D')
    .toLowerCase().replace(/\s+/g, ' ').trim();
}

// Quet thu muc nguyenlieu mot lan
const khoNguyenLieu = {};
if (fs.existsSync(DIR_NGUYENLIEU)) {
  for (const f of fs.readdirSync(DIR_NGUYENLIEU)) {
    const ext = path.extname(f).toLowerCase();
    if (!['.png', '.jpg', '.jpeg', '.webp'].includes(ext)) continue;
    khoNguyenLieu[chuanHoa(path.basename(f, ext))] = path.join(DIR_NGUYENLIEU, f);
  }
}

const daDungAnhThat = new Set(), conThieu = new Set();

function timAnh(ten) {
  const key = chuanHoa(ten);
  if (khoNguyenLieu[key]) { daDungAnhThat.add(ten); return khoNguyenLieu[key]; }
  const cp = ICON[ten];
  if (cp) { conThieu.add(ten); return path.join(DIR_ICON, cp + '.png'); }
  throw new Error('khong co anh cho "' + ten + '" — hay them file tools/nguyenlieu/' + key + '.png');
}

function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

function drawPlus(ctx, cx, cy) {
  const L = 90, T = 22;
  ctx.fillStyle = PLUS;
  roundRect(ctx, cx - L / 2, cy - T / 2, L, T, T / 2); ctx.fill();
  roundRect(ctx, cx - T / 2, cy - L / 2, T, L, T / 2); ctx.fill();
}

function drawText(ctx, part, cx, cy, slotW) {
  let size = 120;
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  do {
    ctx.font = 'bold ' + size + 'px Arial';
    if (ctx.measureText(part.text).width <= slotW) break;
    size -= 4;
  } while (size > 20);
  ctx.lineWidth = Math.max(4, size * 0.08);
  ctx.strokeStyle = '#1A1A1A'; ctx.lineJoin = 'round';
  ctx.strokeText(part.text, cx, cy);
  ctx.fillStyle = part.color || TEXT_DEFAULT;
  ctx.fillText(part.text, cx, cy);
}

// Ve anh vua trong o vuong box, GIU NGUYEN ti le (anh that thuong khong vuong)
function drawFit(ctx, img, cx, cy, box) {
  const k = Math.min(box / img.width, box / img.height);
  const w = img.width * k, h = img.height * k;
  ctx.drawImage(img, cx - w / 2, cy - h / 2, w, h);
}

async function build(p) {
  const canvas = createCanvas(W, H);
  const ctx = canvas.getContext('2d');
  const g = ctx.createLinearGradient(0, 0, W, H);
  g.addColorStop(0, BG_FROM); g.addColorStop(1, BG_TO);
  ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);

  const n = p.parts.length, L = LAYOUT[n];
  if (!L) throw new Error('chi ho tro 2 hoac 3 phan tu, cau nay co ' + n);
  const cy = 360;

  for (let i = 0; i < n; i++) {
    const part = p.parts[i], cx = L.centers[i];

    ctx.save();
    ctx.shadowColor = CARD_SHADOW; ctx.shadowBlur = 34; ctx.shadowOffsetY = 12;
    ctx.fillStyle = CARD;
    roundRect(ctx, cx - L.cardW / 2, cy - L.cardH / 2, L.cardW, L.cardH, 48); ctx.fill();
    ctx.restore();

    if (part.text) drawText(ctx, part, cx, cy, L.slot);
    else drawFit(ctx, await loadImage(timAnh(part.img)), cx, cy, L.box);

    if (i < n - 1) drawPlus(ctx, (L.centers[i] + L.centers[i + 1]) / 2, cy);
  }

  fs.mkdirSync(OUT_DIR, { recursive: true });
  fs.writeFileSync(path.join(OUT_DIR, p.answer + '.png'), canvas.toBuffer('image/png'));
}

(async () => {
  let ok = 0;
  for (const p of PUZZLES) {
    try { await build(p); console.log('OK   ' + p.answer + '.png'); ok++; }
    catch (e) { console.log('LOI  ' + p.answer + ' -> ' + e.message); }
  }
  console.log('\nXong: ' + ok + '/' + PUZZLES.length + ' anh -> ' + OUT_DIR);
  console.log('Dung anh that: ' + daDungAnhThat.size + ' vat');
  if (conThieu.size) {
    console.log('\nCON DUNG ICON DU PHONG (' + conThieu.size + ' vat) — tha anh vao tools/nguyenlieu/ de dep hon:');
    for (const t of [...conThieu].sort()) console.log('   ' + chuanHoa(t) + '.png     (' + t + ')');
  }
})();
