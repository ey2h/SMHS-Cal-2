'use strict';
/* rec_dump.js — 沿 WMF 记录链（Size DWORD 先、Size=整条记录字数）遍历，
 * 打印每条记录的功能码、数据 hex、GBK 解码，用于识别哪些记录含文本。
 * 起点自动探测：尝试 22..80，取能干净走到 EOF 的最早起点。*/
const fs = require('fs');
const GBK = new TextDecoder('gbk', { fatal: false });

const f = process.argv[2];
const buf = fs.readFileSync(f);

function tryWalk(start) {
  let p = start;
  const recs = [];
  let guard = 0;
  while (p + 6 <= buf.length && guard++ < 100000) {
    const size = buf.readUInt32LE(p);
    const func = buf.readUInt16LE(p + 4);
    const dataWords = size - 3;
    if (dataWords < 0 || p + size * 2 > buf.length) return null;
    recs.push({ p, size, func, dataAt: p + 6, dataLen: dataWords * 2 });
    if (func === 0 && dataWords === 0) return { recs, end: p + size * 2 };
    p += size * 2;
  }
  return null;
}
let best = null;
for (let s = 22; s <= 80; s += 2) {
  const r = tryWalk(s);
  if (r && r.end === buf.length) { best = r; break; }
}
if (!best) { console.log('NO CLEAN WALK'); process.exit(0); }
console.log('start=', best.recs[0].p, 'records=', best.recs.length, 'size=', buf.length);

// 功能码直方图
const hist = new Map();
for (const r of best.recs) hist.set(r.func, (hist.get(r.func) || 0) + 1);
console.log('func histogram:', [...hist.entries()].sort((a, b) => b[1] - a[1]).map(([k, v]) => '0x' + k.toString(16).padStart(4, '0') + '×' + v).join(' '));

// 逐条打印（跳过大的 palette），带 GBK 解码预览
for (const r of best.recs) {
  if (r.dataLen > 40) {
    console.log(`@${r.p} func=0x${r.func.toString(16).padStart(4, '0')} size=${r.size} dataLen=${r.dataLen} [大记录，仅前 32 字节]`,
      buf.subarray(r.dataAt, r.dataAt + 32).toString('hex'), '|', GBK.decode(buf.subarray(r.dataAt, r.dataAt + 32)).replace(/[^\x20-\x7e\u4e00-\u9fff]+/g, '?'));
    continue;
  }
  const hex = buf.subarray(r.dataAt, r.dataAt + r.dataLen).toString('hex');
  const words = [];
  for (let i = 0; i + 1 < r.dataLen; i += 2) words.push('0x' + buf.readUInt16LE(r.dataAt + i).toString(16));
  const txt = GBK.decode(buf.subarray(r.dataAt, r.dataAt + r.dataLen)).replace(/[^\x20-\x7e\u4e00-\u9fff]+/g, '?');
  console.log(`@${r.p} func=0x${r.func.toString(16).padStart(4, '0')} size=${r.size} data=${hex} words=[${words.join(' ')}] gbk=${JSON.stringify(txt)}`);
}
