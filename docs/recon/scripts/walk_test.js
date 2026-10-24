'use strict';
/* walk_test.js — 反推 WMF 记录布局：对同一文件尝试多种 (起始偏移, 字段顺序, Size 语义)
 * 组合，看哪个能干净地走到文件末尾（EOF 记录）。*/
const fs = require('fs');
const f = process.argv[2];
const buf = fs.readFileSync(f);

function walk(start, sizeFirst, sizeSemantics) {
  let p = start;
  const recs = [];
  const issues = [];
  let guard = 0;
  while (p + 6 <= buf.length && guard++ < 5000) {
    let size, func;
    if (sizeFirst) {
      size = buf.readUInt32LE(p);
      func = buf.readUInt16LE(p + 4);
    } else {
      func = buf.readUInt16LE(p);
      size = buf.readUInt32LE(p + 2);
    }
    const dataWords = sizeSemantics === 'total' ? size - 3 : size;
    if (dataWords < 0) { issues.push({ p, why: 'negative data words size=' + size }); return { recs, issues, end: p, ok: false }; }
    const dataBytes = dataWords * 2;
    const totalLen = 6 + dataBytes;
    if (p + totalLen > buf.length) { issues.push({ p, func: '0x' + func.toString(16), why: 'overrun len=' + totalLen }); return { recs, issues, end: p, ok: false }; }
    recs.push({ p, func: '0x' + func.toString(16).padStart(4, '0'), size, dataWords });
    if (func === 0 && dataBytes === 0) return { recs, issues, end: p + totalLen, ok: true };
    p += totalLen;
  }
  return { recs, issues, end: p, ok: false };
}

for (let start = 22; start <= 80; start += 2) {
  for (const sf of [true, false]) {
    for (const sem of ['data', 'total']) {
      const r = walk(start, sf, sem);
      if (r.ok && r.end === buf.length) {
        console.log(`CLEAN: start=${start} sizeFirst=${sf} semantics=${sem} records=${r.recs.length}`);
        console.log('   first 8:', r.recs.slice(0, 8).map((x) => `${x.p}:${x.func}/sz${x.size}`).join(' '));
        console.log('   last 5 :', r.recs.slice(-5).map((x) => `${x.p}:${x.func}/sz${x.size}`).join(' '));
      }
    }
  }
}
console.log('done, filesize', buf.length);
