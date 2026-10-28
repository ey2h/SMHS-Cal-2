'use strict';
/* sig_scan.js — 在 WMF 文件里搜索已知记录函数签名（小端 2 字节），
 * 打印偏移与紧随其后的 4 字节 Size，用于反推头长度/记录对齐。 */
const fs = require('fs');
const FUNCS = [0x0521, 0x0921, 0x0a32, 0x0e32, 0x0512, 0x0912, 0x000f, 0x0420, 0x0421, 0x0414, 0x0415, 0x0416, 0x02fc, 0x0438, 0x0437, 0x0102, 0x041e, 0x041f, 0x00f7, 0x0142, 0x0a31];
const files = process.argv.slice(2);
for (const f of files) {
  const b = fs.readFileSync(f);
  console.log('=== ' + f + ' (' + b.length + ' bytes)');
  for (const fn of FUNCS) {
    const lo = fn & 0xff, hi = (fn >> 8) & 0xff;
    const hits = [];
    for (let i = 0; i + 5 < b.length; i++) {
      if (b[i] === lo && b[i + 1] === hi) {
        const size = b.readUInt32LE(i + 2);
        hits.push({ i, parity: i % 2, size, plausible: size * 2 + 6 <= b.length - i && size > 0 && size < 5000 });
        if (hits.length > 6) break;
      }
    }
    if (hits.length) {
      console.log('  0x' + fn.toString(16).padStart(4, '0') + ':', hits.slice(0, 6).map((h) => `@${h.i}(p${h.parity}) sz=${h.size}${h.plausible ? '' : '!'}`).join(' '));
    }
  }
  console.log('  tail hex:', b.subarray(b.length - 16).toString('hex'));
}
