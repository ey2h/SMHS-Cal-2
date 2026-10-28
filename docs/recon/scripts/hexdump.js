'use strict';
/* hexdump.js — 简易 hexdump: node hexdump.js <file> [start] [len] */
const fs = require('fs');
const f = process.argv[2];
const start = parseInt(process.argv[3] || '0', 10);
const b = fs.readFileSync(f);
const len = parseInt(process.argv[4] || String(b.length - start), 10);
for (let i = start; i < Math.min(b.length, start + len); i += 16) {
  const slice = b.subarray(i, Math.min(b.length, i + 16));
  const hex = [...slice].map((x) => x.toString(16).padStart(2, '0')).join(' ');
  const asc = [...slice].map((x) => (x >= 32 && x < 127 ? String.fromCharCode(x) : '.')).join('');
  console.log(i.toString(16).padStart(6, '0') + '  ' + hex.padEnd(48, ' ') + '  |' + asc + '|');
}
console.log('file size', b.length);
