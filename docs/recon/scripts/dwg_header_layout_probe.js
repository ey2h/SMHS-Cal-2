'use strict';
/*
 * dwg_header_layout_probe.js — find the R2004(AC1021) section-table layout empirically.
 * Read-only probe. Prints raw header hex and candidate (count, table-offset) interpretations.
 */
const fs = require('fs');

const FILE = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\附件\\附录 型材断面汇总表（内附用法说明）.dwg';
const buf = fs.readFileSync(FILE);

console.log('size=' + buf.length);
process.stdout.write('raw first 0x80 bytes:\n');
for (let o = 0; o < 0x80 && o < buf.length; o += 16) {
  const hex = [];
  const dec = [];
  for (let i = 0; i < 16; i++) {
    const b = buf[o + i];
    hex.push(b === undefined ? '   ' : b.toString(16).padStart(2, '0'));
    dec.push(b === undefined || b < 0x20 || b > 0x7e ? '.' : String.fromCharCode(b));
  }
  process.stdout.write(o.toString(16).padStart(4, '0') + '  ' + hex.join(' ') + '  |' + dec.join('') + '|\n');
}

// Candidate: at some offset T there is an int32 count n, followed by n records of
//   { uint32 start; uint16 size }   (6 bytes each)
// Validate that records are increasing, inside the file, and (ideally) contiguous.
function testRecordTable(countOffset, recordSize) {
  const n = buf.readInt32LE(countOffset);
  if (n < 1 || n > 400) return null;
  const recs = [];
  let p = countOffset + 4;
  for (let i = 0; i < n; i++) {
    if (p + recordSize > buf.length) return null;
    const start = buf.readUInt32LE(p);
    const size = buf.readUInt16LE(p + 4);
    recs.push({ i, start, size });
    p += recordSize;
  }
  let increasing = true;
  let inside = true;
  let contiguous = true;
  let prevEnd = null;
  let maxStart = 0;
  for (const r of recs) {
    if (r.size === 0) inside = false;
    if (r.start + r.size > buf.length) inside = false;
    if (prevEnd !== null) {
      if (r.start < prevEnd) increasing = false;
      if (r.start !== prevEnd) contiguous = false;
    }
    prevEnd = r.start + r.size;
    if (r.start > maxStart) maxStart = r.start;
  }
  const tableEnd = countOffset + 4 + n * recordSize;
  return {
    countOffset: '0x' + countOffset.toString(16),
    n,
    recordSize,
    increasing,
    contiguous,
    insideFile: inside,
    tableEnd: '0x' + tableEnd.toString(16),
    lastEnd: prevEnd,
    gapToSectionData: prevEnd - tableEnd,
    sample: recs.slice(0, 6).map((r) => 'start=0x' + r.start.toString(16) + ' size=0x' + r.size.toString(16)),
  };
}

const candidates = [];
for (let recordSize of [6, 8, 10]) {
  for (let o = 0x08; o < 0x100; o += 2) {
    const r = testRecordTable(o, recordSize);
    if (r && r.increasing && r.insideFile && r.n >= 2) candidates.push(r);
  }
}
console.log('\ncandidate record tables (increasing && inside file):');
for (const c of candidates) {
  console.log(JSON.stringify(c));
}
