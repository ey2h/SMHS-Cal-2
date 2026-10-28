'use strict';
/*
 * probe_formats.js  (read-only probe; prints observations to stdout)
 * - checks TextDecoder GBK/GB18030 availability
 * - dumps .inx samples (decoded + raw byte preview)
 * - dumps 50176\tmp.txt, 荷载规范\guifan.dat
 * - shows .mht headers (a few files)
 * - shows .dwg header bytes + ASCII strings sample
 * - shows .rar header signature bytes
 */
const fs = require('fs');

const BASE = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0';
const SPEC = BASE + '\\规范';
const FUJIAN = BASE + '\\附件';
const REGONG = BASE + '\\pic\\热工';

function dec(buf, label) {
  try {
    const td = new TextDecoder(label);
    return { ok: true, text: td.decode(buf) };
  } catch (e) {
    return { ok: false, error: e.message };
  }
}

function hex(buf, n) {
  const b = buf.subarray(0, n);
  const parts = [];
  for (let i = 0; i < b.length; i++) parts.push(b[i].toString(16).padStart(2, '0'));
  return parts.join(' ');
}

function hr(t) {
  console.log('\n' + '='.repeat(78));
  console.log('### ' + t);
  console.log('='.repeat(78));
}

// 1) TextDecoder encodings
hr('TextDecoder encodings');
for (const label of ['gbk', 'gb18030', 'gb2312', 'utf-8', 'windows-1252']) {
  const r = dec(Buffer.from([0xd6, 0xd0, 0xb9, 0xfa]), label); // 中国
  console.log(label + ' -> ' + (r.ok ? JSON.stringify(r.text) : 'ERR: ' + r.error));
}

// 2) .inx samples
const inxSamples = [
  REGONG + '\\规范\\50176\\bj.inx',
  REGONG + '\\规范\\50176\\sh.inx',
  REGONG + '\\规范\\50176\\ah.inx',
  REGONG + '\\规范\\50176\\nmg.inx',
  REGONG + '\\规范\\leiji\\bj.inx',
  REGONG + '\\规范\\leiji\\sc.inx',
];
for (const p of inxSamples) {
  hr('INX ' + p);
  if (!fs.existsSync(p)) { console.log('MISSING'); continue; }
  const buf = fs.readFileSync(p);
  console.log('size=' + buf.length);
  console.log('hex[0..64]: ' + hex(buf, 64));
  const d = dec(buf, 'gb18030');
  console.log('decoded(' + (d.ok ? 'gb18030' : 'FAILED') + '):');
  if (d.ok) {
    const lines = d.text.split(/\r\n|\r|\n/);
    lines.forEach((l, i) => console.log(`  [${i}] len=${l.length} ${JSON.stringify(l)}`));
    console.log('  lineCount=' + lines.length);
  }
}

// 3) tmp.txt + guifan.dat
for (const p of [REGONG + '\\规范\\50176\\tmp.txt', SPEC + '\\荷载规范\\guifan.dat']) {
  hr('TEXT ' + p);
  if (!fs.existsSync(p)) { console.log('MISSING'); continue; }
  const buf = fs.readFileSync(p);
  console.log('size=' + buf.length + ' hex[0..48]: ' + hex(buf, 48));
  const d = dec(buf, 'gb18030');
  if (d.ok) {
    const lines = d.text.split(/\r\n|\r|\n/);
    lines.slice(0, 60).forEach((l, i) => console.log(`  [${i}] ${JSON.stringify(l)}`));
    console.log('  totalLines=' + lines.length);
  } else {
    console.log('decode failed: ' + d.error);
  }
}

// 4) .mht headers
hr('MHT headers');
for (const p of [
  SPEC + '\\节能1.mht',
  SPEC + '\\节能2.mht',
  SPEC + '\\物理性能\\AQXS.mht',
  REGONG + '\\规范\\表B.2.mht',
]) {
  console.log('--- ' + p);
  if (!fs.existsSync(p)) { console.log('MISSING'); continue; }
  const buf = fs.readFileSync(p);
  console.log('size=' + buf.length + ' hex[0..32]: ' + hex(buf, 32));
  const head = buf.subarray(0, 1200).toString('latin1');
  console.log(head.replace(/\r/g, '\\r\n'));
}

// 5) .dwg
hr('DWG header');
{
  const p = FUJIAN + '\\附录 型材断面汇总表（内附用法说明）.dwg';
  if (fs.existsSync(p)) {
    const buf = fs.readFileSync(p);
    console.log('size=' + buf.length);
    console.log('hex[0..48]: ' + hex(buf, 48));
    console.log('head ascii: ' + JSON.stringify(buf.subarray(0, 16).toString('latin1')));
    // crude ASCII runs >= 6 printable
    const runs = [];
    let cur = '';
    for (let i = 0; i < buf.length; i++) {
      const c = buf[i];
      if (c >= 0x20 && c < 0x7f) cur += String.fromCharCode(c);
      else { if (cur.length >= 6) runs.push(cur); cur = ''; }
    }
    if (cur.length >= 6) runs.push(cur);
    console.log('asciiRuns>=6 count=' + runs.length);
    console.log('first 60 runs: ' + JSON.stringify(runs.slice(0, 60)));
  } else console.log('MISSING ' + p);
}

// 6) .rar headers
hr('RAR headers');
for (const name of fs.readdirSync(FUJIAN)) {
  if (!name.toLowerCase().endsWith('.rar')) continue;
  const p = FUJIAN + '\\' + name;
  const buf = fs.readFileSync(p);
  console.log(name + ' size=' + buf.length + ' hex[0..16]: ' + hex(buf, 16));
}

// 7) .doc headers (confirm OLE compound)
hr('DOC headers');
for (const name of fs.readdirSync(SPEC)) {
  if (!name.toLowerCase().endsWith('.doc')) continue;
  const p = SPEC + '\\' + name;
  const buf = fs.readFileSync(p);
  console.log(name + ' size=' + buf.length + ' hex[0..16]: ' + hex(buf, 16));
}

// 8) 规范*.txt encoding check
hr('规范*.txt encoding sample');
{
  const buf = fs.readFileSync(SPEC + '\\规范8.txt');
  console.log('hex[0..48]: ' + hex(buf, 48));
  const d = dec(buf, 'gb18030');
  console.log('gb18030: ' + JSON.stringify(d.text));
  const u = dec(buf, 'utf-8');
  console.log('utf8: ' + JSON.stringify(u.ok ? u.text : 'invalid utf-8'));
}

// 9) .wmf header sample
hr('WMF sample header');
{
  const p = REGONG + '\\7.wmf';
  if (fs.existsSync(p)) {
    const buf = fs.readFileSync(p);
    console.log('size=' + buf.length + ' hex[0..32]: ' + hex(buf, 32));
  }
}
