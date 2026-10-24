'use strict';
/*
 * dwg_preview_probe.js — look for an embedded preview bitmap (BMP or PNG) inside the DWG.
 * If a self-consistent bitmap is found, write it out (to docs\recon\reports\) so a human can read
 * the drawing visually. Read-only with respect to the source file.
 */
const fs = require('fs');

const FILE = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\附件\\附录 型材断面汇总表（内附用法说明）.dwg';
const OUT_DIR = 'C:\\code\\Glass\\docs\\recon\\reports\\dwg_preview';
const buf = fs.readFileSync(FILE);
const N = buf.length;

if (!fs.existsSync(OUT_DIR)) fs.mkdirSync(OUT_DIR, { recursive: true });

// 1) PNG signature
const pngHits = [];
for (let i = 0; i + 7 < N; i++) {
  if (buf[i] === 0x89 && buf[i + 1] === 0x50 && buf[i + 2] === 0x4e && buf[i + 3] === 0x47 &&
      buf[i + 4] === 0x0d && buf[i + 5] === 0x0a && buf[i + 6] === 0x1a && buf[i + 7] === 0x0a) {
    pngHits.push(i);
  }
}
console.log('PNG signatures found at: ' + (pngHits.length ? pngHits.map((o) => '0x' + o.toString(16)).join(', ') : 'none'));
pngHits.forEach((o, idx) => {
  // walk chunk structure to find a self-consistent PNG of length <= file end
  let p = o;
  let ok = true;
  let w = 0, h = 0, total = 0;
  try {
    while (p + 8 <= N) {
      const len = buf.readUInt32BE(p);
      const type = buf.toString('latin1', p + 4, p + 8);
      if (type === 'IHDR') { w = buf.readUInt32BE(p + 8); h = buf.readUInt32BE(p + 12); }
      if (type === 'IEND') { total = p + 12 - o; break; }
      p += 12 + len;
      if (len > N) { ok = false; break; }
    }
    if (!total) ok = false;
  } catch (e) { ok = false; }
  console.log('  PNG candidate @0x' + o.toString(16) + ' structurallyValid=' + ok + ' w=' + w + ' h=' + h + ' bytes=' + total);
  if (ok && total > 0) {
    const out = OUT_DIR + '\\candidate_' + idx + '.png';
    fs.writeFileSync(out, buf.subarray(o, o + total));
    console.log('  wrote ' + out);
  }
});

// 2) BMP headers: 'BM' + valid BITMAPINFOHEADER
const bmpHits = [];
for (let i = 0; i + 54 < N; i++) {
  if (buf[i] === 0x42 && buf[i + 1] === 0x4d) {
    const fileSize = buf.readUInt32LE(i + 2);
    const bitsOff = buf.readUInt32LE(i + 10);
    const hdrSize = buf.readUInt32LE(i + 14);
    const w = buf.readInt32LE(i + 18);
    const h = buf.readInt32LE(i + 22);
    const planes = buf.readUInt16LE(i + 26);
    const bpp = buf.readUInt16LE(i + 28);
    const plausible =
      fileSize > 0 && fileSize <= 4000000 &&
      (hdrSize === 12 || hdrSize === 40 || hdrSize === 64 || hdrSize === 108 || hdrSize === 52) &&
      w > 0 && w < 8000 && Math.abs(h) > 0 && Math.abs(h) < 8000 &&
      planes >= 1 && planes <= 4 &&
      [1, 4, 8, 16, 24, 32].includes(bpp) &&
      bitsOff >= hdrSize && bitsOff < fileSize;
    if (plausible) bmpHits.push({ offset: i, fileSize, bitsOff, hdrSize, w, h, planes, bpp });
  }
}
console.log('\nBMP candidates: ' + bmpHits.length);
bmpHits.forEach((c, idx) => {
  console.log('  @0x' + c.offset.toString(16) + ' size=' + c.fileSize + ' bitsOff=' + c.bitsOff + ' hdr=' + c.hdrSize + ' ' + c.w + 'x' + c.h + ' bpp=' + c.bpp);
});

// 3) DWG header fields that historically point at the preview image (R13..R2002 style), just to report them
console.log('\nfile header first 0x40 bytes:');
for (let o = 0; o < 0x40; o += 16) {
  const hex = [];
  for (let i = 0; i < 16; i++) hex.push(buf[o + i].toString(16).padStart(2, '0'));
  console.log('  ' + o.toString(16).padStart(4, '0') + '  ' + hex.join(' '));
}
for (const o of [0x14, 0x18, 0x1c, 0x20, 0x24, 0x28, 0x2c, 0x30, 0x34, 0x38, 0x3c]) {
  console.log('  u32le@0x' + o.toString(16) + ' = ' + buf.readUInt32LE(o));
}
