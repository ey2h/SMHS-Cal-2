// Identify the embedded PE module and decode the app's own data files as UTF-16LE.
const fs = require('fs');
const zlib = require('zlib');

const EXE = process.argv[2];
const OUTDIR = process.argv[3];
const DATA = process.argv.slice(4);
fs.mkdirSync(OUTDIR, { recursive: true });

const u16 = new TextDecoder('utf-16le', { fatal: false });
const gbk = new TextDecoder('gbk', { fatal: false });
const buf = fs.readFileSync(EXE);

// --- pull every inflatable stream that starts with MZ, save and describe
function describePe(b, label) {
  const pe = b.readUInt32LE(0x3c);
  if (b.readUInt32LE(pe) !== 0x00004550) return label + ': 不是 PE';
  const machine = b.readUInt16LE(pe + 4).toString(16);
  const chars = b.readUInt16LE(pe + 22);
  const opt = pe + 24;
  const magic = b.readUInt16LE(opt);
  const is64 = magic === 0x20b;
  const dllBit = (chars & 0x2000) !== 0;
  const ddOff = opt + (is64 ? 112 : 96);
  const rvaToOff = (rva) => {
    const nSec = b.readUInt16LE(pe + 6);
    const so = opt + b.readUInt16LE(pe + 20);
    for (let i = 0; i < nSec; i++) {
      const o = so + i * 40;
      const va = b.readUInt32LE(o + 12), vs = Math.max(b.readUInt32LE(o + 8), b.readUInt32LE(o + 16)), rp = b.readUInt32LE(o + 20);
      if (rva >= va && rva < va + vs) return rp + (rva - va);
    }
    return null;
  };
  const cstr = (o, max = 80) => { let s = ''; for (let i = 0; i < max && b[o + i]; i++) s += String.fromCharCode(b[o + i]); return s; };
  let imports = [];
  const impDir = b.readUInt32LE(ddOff + 8);
  const impOff = rvaToOff(impDir) ?? impDir;
  try {
    for (let p = impOff, n = 0; n < 30; n++) {
      const nr = b.readUInt32LE(p); if (!nr) break;
      const no = rvaToOff(nr); if (no) imports.push(cstr(no, 30));
      p += 20;
    }
  } catch { }
  let exports = [];
  const expDir = b.readUInt32LE(ddOff);
  if (expDir) {
    const eOff = rvaToOff(expDir);
    if (eOff) {
      const nNames = b.readUInt32LE(eOff + 24);
      const fnOff = rvaToOff(b.readUInt32LE(eOff + 12));
      const nmOff = rvaToOff(b.readUInt32LE(eOff + 32));
      const orOff = rvaToOff(b.readUInt32LE(eOff + 36));
      const nameOff = rvaToOff(b.readUInt32LE(eOff + 4));
      const mod = nameOff ? cstr(nameOff, 40) : '';
      try {
        for (let i = 0; i < Math.min(nNames, 40); i++) {
          const o = rvaToOff(b.readUInt32LE(nmOff + i * 4));
          if (o) exports.push(cstr(o, 60));
        }
      } catch { }
      exports.unshift('(模块名 ' + mod + ')');
    }
  }
  // version info strings from resources (UTF-16LE runs)
  const t = u16.decode(b);
  const vs = [];
  for (const k of ['CompanyName', 'FileDescription', 'ProductName', 'ProductVersion', 'FileVersion', 'OriginalFilename']) {
    const i = t.indexOf(k);
    if (i >= 0) vs.push(k + '=' + t.slice(i + k.length, i + k.length + 40).replace(/[^\u4e00-\u9fa5A-Za-z0-9 .\-_,]/g, '').trim());
  }
  return {
    text: label + ' | machine=0x' + machine + ' ' + (is64 ? 'PE32+' : 'PE32') + ' ' + (dllBit ? 'DLL' : 'EXE')
      + '\n  imports: ' + imports.join(', ')
      + '\n  exports(' + exports.length + '): ' + exports.slice(0, 18).join(', ')
      + '\n  version: ' + vs.join('  '),
  };
}
function n0(x) { return x || 0; }

const blobs = [];
for (let i = 0; i + 2 < buf.length; i++) {
  if (buf[i] === 0x78 && (buf[i + 1] === 0x9c || buf[i + 1] === 0xda || buf[i + 1] === 0x01)) {
    try {
      const out = zlib.inflateSync(buf.subarray(i));
      if (out.length > 4096 && out[0] === 0x4d && out[1] === 0x5a) blobs.push({ off: i, out });
    } catch { }
  }
}
console.log('=== 解压出的内嵌 PE 模块: ' + blobs.length + ' 个');
for (const b of blobs) {
  const p = OUTDIR + '\\embedded_' + b.off + '.bin';
  fs.writeFileSync(p, b.out);
  const d = describePe(b.out, '  @' + b.off + ' → ' + b.out.length + ' B');
  console.log(typeof d === 'string' ? d : d.text);
  console.log('  落盘: ' + p);
}

// --- decode the app's own data files
for (const f of DATA) {
  const b = fs.readFileSync(f);
  const t = u16.decode(b);
  const printableRatio = (t.match(/[\u4e00-\u9fa5A-Za-z0-9]/g) || []).length / Math.max(1, t.length);
  console.log('\n############ ' + f.split('\\').pop() + '  [' + b.length + ' B]  UTF-16LE 可读字符占比=' + (printableRatio * 100).toFixed(1) + '%');
  const lines = t.split(/[\r\n]+/).map((s) => s.replace(/\0/g, ' ').trim()).filter((s) => s.length > 1);
  console.log('  非空行=' + lines.length);
  for (const l of lines.slice(0, 22)) console.log('    ' + l.slice(0, 110));
  if (lines.length > 22) console.log('    … 中段样本: ' + (lines[Math.floor(lines.length / 2)] || '').slice(0, 110));
}
