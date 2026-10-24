// Look inside the packed binary: raw layout, entropy map, embedded zlib blobs, and its own data files.
const fs = require('fs');
const zlib = require('zlib');
const gbk = new TextDecoder('gbk', { fatal: false });

function peLayout(buf) {
  const pe = buf.readUInt32LE(0x3c);
  const nSec = buf.readUInt16LE(pe + 6);
  const optSize = buf.readUInt16LE(pe + 20);
  const secOff = pe + 24 + optSize;
  const secs = [];
  for (let i = 0; i < nSec; i++) {
    const o = secOff + i * 40;
    secs.push({
      name: Buffer.from(buf.subarray(o, o + 8)).toString('latin1').replace(/\0.*/, ''),
      vsize: buf.readUInt32LE(o + 8), vaddr: buf.readUInt32LE(o + 12),
      rawsize: buf.readUInt32LE(o + 16), rawptr: buf.readUInt32LE(o + 20),
    });
  }
  console.log('=== 段原始数据分布 (file=' + buf.length + ' B)');
  let maxEnd = 0;
  for (const s of secs) {
    const end = s.rawptr + s.rawsize;
    if (end > maxEnd) maxEnd = end;
    console.log('  ' + s.name.padEnd(8) + ' file[' + String(s.rawptr).padStart(9) + '..' + String(end).padStart(9)
      + '] raw=' + String(s.rawsize).padStart(8) + '  vsize=' + s.vsize);
  }
  console.log('  段表末尾=' + maxEnd + '  → overlay(段表外)=' + (buf.length - maxEnd) + ' B');
  const sorted = secs.filter((s) => s.rawsize > 0).sort((a, b) => a.rawptr - b.rawptr);
  for (let i = 1; i < sorted.length; i++) {
    const gap = sorted[i].rawptr - (sorted[i - 1].rawptr + sorted[i - 1].rawsize);
    if (gap > 512) console.log('  段间空隙 ' + gap + ' B 在 ' + sorted[i - 1].name + ' 之后');
  }
}

function entropy(a) {
  const h = new Array(256).fill(0);
  for (let i = 0; i < a.length; i++) h[a[i]]++;
  let e = 0;
  for (let i = 0; i < 256; i++) { const p = h[i] / a.length; if (p) e -= p * Math.log2(p); }
  return e;
}

function entropyMap(buf, blk) {
  console.log('\n=== 熵图（每 64 KB：C=压缩/加密, P=明文可直读, M=混合）');
  const runs = [];
  let cur = null;
  for (let o = 0; o + blk <= buf.length; o += blk) {
    const e = entropy(buf.subarray(o, o + blk));
    const kind = e > 7.3 ? 'C' : e < 5 ? 'P' : 'M';
    if (!cur || cur.kind !== kind) { cur = { kind, from: o, to: o + blk, hi: e, lo: e }; runs.push(cur); }
    else { cur.to = o + blk; cur.hi = Math.max(cur.hi, e); cur.lo = Math.min(cur.lo, e); }
  }
  for (const r of runs) if (r.to - r.from >= 5 * blk) {
    console.log('  ' + r.kind + ' [' + String(Math.floor(r.from / 1024)).padStart(6) + '..' + String(Math.floor(r.to / 1024)).padStart(6)
      + ' KB] ' + ((r.to - r.from) / 1024).toFixed(0).padStart(6) + ' KB  熵 ' + r.lo.toFixed(2) + '-' + r.hi.toFixed(2));
  }
}

function printable(s) { return s.replace(/[^\u4e00-\u9fa5A-Za-z0-9\u3001\uff08\uff09()\.\-_=,;:%\/\s]/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 140); }

function zlibBlobs(buf, label, tryMax, show) {
  console.log('\n=== ' + label + ': 内嵌 zlib/deflate 候选');
  const hits = [];
  for (let i = 0; i + 2 < buf.length; i++) {
    const b0 = buf[i], b1 = buf[i + 1];
    if (b0 === 0x78 && (b1 === 0x9c || b1 === 0xda || b1 === 0x01)) hits.push(i);
  }
  console.log('  候选 ' + hits.length + ' 处，试解压前 ' + tryMax + ' 处');
  const ok = [];
  for (const off of hits.slice(0, tryMax)) {
    try {
      const out = zlib.inflateSync(buf.subarray(off));
      if (out.length > 512) ok.push({ off, out });
    } catch { /* 不是真流 */ }
  }
  ok.sort((a, b) => b.out.length - a.out.length);
  for (const r of ok.slice(0, show)) {
    console.log('  @' + String(r.off).padStart(9) + ' 解压后=' + String(r.out.length).padStart(8) + ' B | ' + printable(gbk.decode(r.out.subarray(0, 800))));
  }
  if (!ok.length) console.log('  （无可解压块）');
}

function stringsTop(buf, n) {
  const text = gbk.decode(buf);
  const RE = /[\u4e00-\u9fa5A-Za-z][\u4e00-\u9fa5A-Za-z0-9\u3001\uff08\uff09()\.\-_=,;:%\/]{3,}/g;
  const m = text.match(RE) || [];
  const freq = new Map();
  for (const s of m) freq.set(s, (freq.get(s) || 0) + 1);
  return [...freq.entries()].sort((a, b) => b[1] - a[1]).slice(0, n);
}

const target = process.argv[2];
const buf = fs.readFileSync(target);
console.log('目标: ' + target + '  ' + buf.length + ' B');
peLayout(buf);
entropyMap(buf, 65536);
zlibBlobs(buf, 'EXE', 60, 6);

for (const extra of process.argv.slice(3)) {
  console.log('\n############ 数据文件: ' + extra.split('\\').pop());
  const b = fs.readFileSync(extra);
  console.log('大小=' + b.length + ' B  首块熵=' + entropy(b.subarray(0, Math.min(b.length, 65536))).toFixed(3));
  zlibBlobs(b, extra.split('\\').pop(), 30, 3);
  console.log('  明文关键词 Top:');
  for (const [s, c] of stringsTop(b, 16)) console.log('    ' + String(c).padStart(4) + '  ' + s.slice(0, 70));
}
