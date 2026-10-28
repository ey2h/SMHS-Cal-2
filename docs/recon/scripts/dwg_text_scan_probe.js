'use strict';
/*
 * dwg_text_scan_probe.js — deeper text-availability scan of the DWG.
 * Fixes the earlier flaw: fixed 2-byte stride. Here every byte alignment is tried.
 * Read-only. No third-party libraries.
 */
const fs = require('fs');

const FILE = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\附件\\附录 型材断面汇总表（内附用法说明）.dwg';
const buf = fs.readFileSync(FILE);
console.log('file=' + FILE + ' size=' + buf.length);

const isCjk = (code) =>
  (code >= 0x4e00 && code <= 0x9fff) ||
  (code >= 0x3400 && code <= 0x4dbf) ||
  (code >= 0x3000 && code <= 0x303f) ||
  (code >= 0xff00 && code <= 0xff65) ||
  (code >= 0x2010 && code <= 0x203b) ||
  (code >= 0x00b0 && code <= 0x00b7) ||
  (code >= 0x0391 && code <= 0x03c9);

// sliding-window UTF-16LE and UTF-16BE CJK runs, every starting offset
function utf16Runs(minLen) {
  const found = { le: [], be: [] };
  for (let start = 0; start < 2; start++) {
    let cur = '';
    for (let i = start; i + 1 < buf.length; i += 2) {
      const code = buf[i] | (buf[i + 1] << 8);
      if (isCjk(code)) {
        cur += String.fromCharCode(code);
      } else {
        if (cur.length >= minLen) found.le.push({ offset: i - cur.length * 2, text: cur });
        cur = '';
      }
    }
    if (cur.length >= minLen) found.le.push({ offset: buf.length - cur.length * 2, text: cur });
  }
  for (let start = 0; start < 2; start++) {
    let cur = '';
    for (let i = start; i + 1 < buf.length; i += 2) {
      const code = (buf[i] << 8) | buf[i + 1];
      if (isCjk(code)) {
        cur += String.fromCharCode(code);
      } else {
        if (cur.length >= minLen) found.be.push({ offset: i - cur.length * 2, text: cur });
        cur = '';
      }
    }
    if (cur.length >= minLen) found.be.push({ offset: buf.length - cur.length * 2, text: cur });
  }
  return found;
}

// GBK / GB18030 two-byte pair runs, tried at both byte alignments
function gbkRuns(minPairs) {
  const out = [];
  for (let start = 0; start < 2; start++) {
    let pairs = [];
    let i = start;
    while (i < buf.length) {
      const b1 = buf[i];
      if (b1 >= 0x81 && b1 <= 0xfe && i + 1 < buf.length) {
        const b2 = buf[i + 1];
        if ((b2 >= 0x40 && b2 <= 0x7e) || (b2 >= 0x80 && b2 <= 0xfe)) {
          const ch = new TextDecoder('gbk').decode(Buffer.from([b1, b2]));
          if (ch.length === 1 && isCjk(ch.codePointAt(0))) {
            pairs.push({ offset: i, ch });
            i += 2;
            continue;
          }
        }
      }
      if (pairs.length >= minPairs) out.push({ startOffset: pairs[0].offset, text: pairs.map((p) => p.ch).join(''), count: pairs.length });
      pairs = [];
      i += 1;
    }
    if (pairs.length >= minPairs) out.push({ startOffset: pairs[0].offset, text: pairs.map((p) => p.ch).join(''), count: pairs.length });
  }
  return out;
}

const lebe = utf16Runs(3);
const gbk = gbkRuns(3);

const KEYWORDS = ['型材', '断面', '铝', '玻璃', '幕墙', '门窗', '说明', '用法', '厚度', '截面', '立柱', '横梁', '面板', '索引', '编号', '规格', '毫米', '系列'];
function keywordHits(list) {
  const hits = [];
  for (const item of list) {
    const text = item.text || item;
    for (const k of KEYWORDS) if (text.includes(k)) hits.push({ text, keyword: k });
  }
  return hits;
}

console.log('\n--- UTF-16LE (所有字节对齐) CJK 片段 >=3 字: ' + lebe.le.length);
for (const r of lebe.le.slice(0, 40)) console.log('  0x' + r.offset.toString(16) + '  ' + r.text);
console.log('  keyword hits: ' + JSON.stringify(keywordHits(lebe.le).slice(0, 20)));

console.log('\n--- UTF-16BE CJK 片段 >=3 字: ' + lebe.be.length);
for (const r of lebe.be.slice(0, 20)) console.log('  0x' + r.offset.toString(16) + '  ' + r.text);
console.log('  keyword hits: ' + JSON.stringify(keywordHits(lebe.be).slice(0, 20)));

console.log('\n--- GBK 双字节连续 CJK 片段 >=3 字: ' + gbk.length);
for (const r of gbk.slice(0, 40)) console.log('  0x' + r.startOffset.toString(16) + '  ' + r.text);
console.log('  keyword hits: ' + JSON.stringify(keywordHits(gbk).slice(0, 20)));

// zlib / deflate signatures anywhere in the file
const magics = [[0x78, 0x9c], [0x78, 0x01], [0x78, 0xda], [0x50, 0x4b], [0x1f, 0x8b]];
console.log('\n--- 压缩签名搜索 ---');
for (const m of magics) {
  const offsets = [];
  for (let i = 0; i + 1 < buf.length; i++) {
    if (buf[i] === m[0] && buf[i + 1] === m[1]) offsets.push('0x' + i.toString(16));
    if (offsets.length > 30) break;
  }
  console.log('  magic ' + m.map((x) => x.toString(16)).join(' ') + ' -> ' + (offsets.length ? offsets.slice(0, 30).join(',') : 'none found'));
}

// longest ASCII runs (identifiers, names)
let cur = '';
const asciiLong = [];
for (let i = 0; i < buf.length; i++) {
  const c = buf[i];
  if (c >= 0x20 && c < 0x7f) cur += String.fromCharCode(c);
  else { if (cur.length >= 4) asciiLong.push({ offset: i - cur.length, text: cur }); cur = ''; }
}
if (cur.length >= 4) asciiLong.push({ offset: buf.length - cur.length, text: cur });
asciiLong.sort((a, b) => b.text.length - a.text.length);
console.log('\n--- 最长的可打印 ASCII 片段 (前 40 条, >=4 字符, 共 ' + asciiLong.length + ') ---');
for (const r of asciiLong.slice(0, 40)) console.log('  0x' + r.offset.toString(16) + '  ' + r.text);

// printable ratio of the region after the 0x64-byte file header
let printableTail = 0;
for (let i = 0x64; i < buf.length; i++) if (buf[i] >= 0x20 && buf[i] < 0x7f) printableTail++;
console.log('\n0x64 之后可打印字节占比: ' + (printableTail / (buf.length - 0x64)).toFixed(4));
