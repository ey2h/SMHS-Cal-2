// PE + data-file probe for 汇宝 V35 artifacts. Node >= 20, standard library only.
// Usage:
//   node pe_probe.js pe <file...>        -> headers, sections, data dirs, imports/exports, version info, strings
//   node pe_probe.js blob <file...>      -> magic/entropy/first-bytes probe for non-PE files
//   node pe_probe.js strings <file> [--utf16 --gbk --min 4]
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

function sha256(buf) { return require('crypto').createHash('sha256').update(buf).digest('hex'); }
function hex(off, len) {
  const s = [];
  for (let i = 0; i < len; i++) s.push(buf2hex(off[i]));
  return s.join(' ');
}
function buf2hex(b) { return b.toString('hex').padStart(2, '0'); }

function entropy(buf) {
  const c = new Array(256).fill(0);
  for (const b of buf) c[b]++;
  let H = 0;
  for (const n of c) if (n) { const p = n / buf.length; H -= p * Math.log2(p); }
  return H;
}

function rvaToOffset(rva, sections) {
  for (const s of sections) {
    const sizeOfRaw = Math.max(s.sizeOfRawData, 0);
    if (rva >= s.virtualAddress && rva < s.virtualAddress + Math.max(sizeOfRaw, s.virtualSize)) {
      return s.pointerToRawData + (rva - s.virtualAddress);
    }
  }
  return null;
}
function readZ(buf, off, max = 4096) {
  if (off == null || off >= buf.length) return null;
  let s = '';
  for (let i = off; i < Math.min(buf.length, off + max); i++) {
    if (buf[i] === 0) break;
    s += String.fromCharCode(buf[i]);
  }
  return s;
}

function parsePE(buf, file) {
  const out = { file, bytes: buf.length, sha256: sha256(buf) };
  if (buf.length < 0x40 || buf[0] !== 0x4d || buf[1] !== 0x5a) { out.error = 'no MZ'; return out; }
  const e_lfanew = buf.readUInt32LE(0x3c);
  out.e_lfanew = e_lfanew;
  if (buf.readUInt32LE(e_lfanew) !== 0x00004550) { out.error = 'no PE signature at ' + e_lfanew; return out; }
  const fh = e_lfanew + 4;
  const machine = buf.readUInt16LE(fh);
  const numSections = buf.readUInt16LE(fh + 2);
  const timeDateStamp = buf.readUInt32LE(fh + 4);
  const sizeOfOptional = buf.readUInt16LE(fh + 16);
  const characteristics = buf.readUInt16LE(fh + 18);
  out.machine = '0x' + machine.toString(16);
  out.numSections = numSections;
  out.timeDateStamp = timeDateStamp;
  out.timeDateStampISO = new Date(timeDateStamp * 1000).toISOString();
  out.characteristics = '0x' + characteristics.toString(16);
  const opt = fh + 20;
  const magic = buf.readUInt16LE(opt);
  out.optionalMagic = magic === 0x10b ? 'PE32' : magic === 0x20b ? 'PE32+' : '0x' + magic.toString(16);
  out.imageBase = '0x' + buf.readUInt32LE(opt + 28).toString(16);
  out.entryPoint = '0x' + buf.readUInt32LE(opt + 40).toString(16);
  out.sizeOfImage = buf.readUInt32LE(opt + 56);
  out.sizeOfHeaders = buf.readUInt32LE(opt + 60);
  const ddCount = 16;
  // PE32 data directories start at opt+96, PE32+ at opt+112
  const ddOff = opt + (magic === 0x20b ? 112 : 96);
  const ddNames = ['EXPORT','IMPORT','RESOURCE','EXCEPTION','SECURITY','BASERELOC','DEBUG','ARCHITECTURE','GLOBALPTR','TLS','LOAD_CONFIG','BOUND_IMPORT','IAT','INT10','PDT','COR20'];
  out.dataDirectories = [];
  for (let i = 0; i < ddCount; i++) {
    const rva = buf.readUInt32LE(ddOff + i * 8);
    const size = buf.readUInt32LE(ddOff + i * 8 + 4);
    if (rva || size) out.dataDirectories.push({ name: ddNames[i] || ('DD' + i), rva: '0x' + rva.toString(16), size });
  }
  const sections = [];
  const secOff = opt + sizeOfOptional;
  for (let i = 0; i < numSections; i++) {
    const o = secOff + i * 40;
    const name = readZ(buf, o, 8) || '';
    const virtualSize = buf.readUInt32LE(o + 8);
    const virtualAddress = buf.readUInt32LE(o + 12);
    const sizeOfRawData = buf.readUInt32LE(o + 16);
    const pointerToRawData = buf.readUInt32LE(o + 20);
    const flags = buf.readUInt32LE(o + 36);
    sections.push({ name, virtualSize, virtualAddress, sizeOfRawData, pointerToRawData, flags: '0x' + flags.toString(16) });
  }
  out.sections = sections;
  // per-section entropy + entry-section mapping
  const epRva = parseInt(out.entryPoint, 16);
  for (const s of sections) {
    const slice = buf.slice(s.pointerToRawData, s.pointerToRawData + s.sizeOfRawData);
    s.entropy = slice.length ? +entropy(slice).toFixed(4) : null;
    s.raw_bytes_available = slice.length;
    if (epRva >= s.virtualAddress && epRva < s.virtualAddress + Math.max(s.virtualSize, s.sizeOfRawData)) out.entry_section = s.name;
  }
  const lastEnd = sections.reduce((m, s) => Math.max(m, s.pointerToRawData + s.sizeOfRawData), 0);
  out.overlay = { offset: lastEnd, bytes: buf.length - lastEnd };
  if (lastEnd < buf.length) out.overlay.entropy = +entropy(buf.slice(lastEnd)).toFixed(4);

  // imports
  const imp = out.dataDirectories.find(d => d.name === 'IMPORT');
  if (imp) {
    const off = rvaToOffset(parseInt(imp.rva, 16), sections);
    out.imports = [];
    if (off != null) {
      for (let i = 0; i < 64; i++) {
        const d = off + i * 20;
        if (d + 20 > buf.length) break;
        const orig = buf.readUInt32LE(d);
        const nameRva = buf.readUInt32LE(d + 12);
        const firstThunk = buf.readUInt32LE(d + 16);
        if (!orig && !nameRva && !firstThunk) break;
        const dll = readZ(buf, rvaToOffset(nameRva, sections));
        const funcs = [];
        let t = firstThunk || orig;
        const tOff = rvaToOffset(t, sections);
        if (tOff != null) {
          for (let j = 0; j < 4096; j++) {
            const p = tOff + j * 4;
            if (p + 4 > buf.length) break;
            const v = buf.readUInt32LE(p);
            if (!v) break;
            if (v & 0x80000000) { funcs.push('ordinal#' + (v & 0xffff)); continue; }
            const fOff = rvaToOffset(v, sections);
            if (fOff == null || fOff + 2 > buf.length) { funcs.push('?'); continue; }
            const hint = buf.readUInt16LE(fOff);
            const nm = readZ(buf, fOff + 2, 256);
            if (nm) funcs.push(nm);
          }
        }
        out.imports.push({ dll, functions: funcs, count: funcs.length });
      }
    } else out.imports = 'import dir not file-backed';
  }
  // exports
  const exp = out.dataDirectories.find(d => d.name === 'EXPORT');
  if (exp) {
    const off = rvaToOffset(parseInt(exp.rva, 16), sections);
    if (off != null && off + 40 <= buf.length) {
      const nNames = buf.readUInt32LE(off + 24);
      const addrNames = rvaToOffset(buf.readUInt32LE(off + 32), sections);
      const names = [];
      if (addrNames != null) for (let i = 0; i < Math.min(nNames, 2000); i++) {
        const nr = buf.readUInt32LE(addrNames + i * 4);
        const nm = readZ(buf, rvaToOffset(nr, sections), 256);
        if (nm) names.push(nm);
      }
      out.exports = { name: readZ(buf, rvaToOffset(buf.readUInt32LE(off + 12), sections)), count: nNames, names };
    } else out.exports = 'export dir not file-backed';
  }
  // TLS
  const tls = out.dataDirectories.find(d => d.name === 'TLS');
  if (tls) out.tls = tls;
  // .pdata (bound/unwind info on x86 is unusual)
  const pd = out.dataDirectories.find(d => d.name === 'PDT');
  if (pd) out.pdata_dir = pd;
  // resources: version info
  const res = out.dataDirectories.find(d => d.name === 'RESOURCE');
  if (res) {
    const off = rvaToOffset(parseInt(res.rva, 16), sections);
    out.resource_dir_offset = off;
    const vs = findVersionInfo(buf, sections, res);
    if (vs) out.version = vs;
  }
  // Rich header
  const richIdx = buf.indexOf(Buffer.from([0x52, 0x69, 0x63, 0x68])); // 'Rich'
  if (richIdx > 0 && richIdx < 0x400) {
    const key = buf.readUInt32LE(richIdx - 4);
    const recs = [];
    for (let i = richIdx; ; i += 8) {
      if (i + 8 > buf.length) break;
      const id = buf.readUInt16LE(i) ^ (key & 0xffff);
      const ver = buf.readUInt16LE(i + 2) ^ (key & 0xffff);
      const cnt = buf.readUInt32LE(i + 4) ^ key;
      if (id === 0x536e) break; // 'Sn' terminator
      recs.push({ id: '0x' + id.toString(16), ver, count: cnt });
      if (recs.length > 40) break;
    }
    out.rich = recs;
  }
  // Rich signature: 'Rich' magic is present, but the XOR key precedes it
  return out;
}

function findVersionInfo(buf, sections, res) {
  const base = rvaToOffset(parseInt(res.rva, 16), sections);
  if (base == null) return null;
  // search for VS_VERSIONINFO signature "VS_VERSION_INFO" as UTF-16LE
  const sig = Buffer.from('VS_VERSION_INFO', 'utf16le');
  const idx = buf.indexOf(sig, base);
  if (idx < 0 || idx > base + 60000) return null;
  // VS_VERSIONINFO header: len(2) valueLen(2) type(2) key(z) pad -> Value = FixedFileInfo
  const valueLen = buf.readUInt16LE(idx - 6);
  const type = buf.readUInt16LE(idx - 4);
  let p = idx + sig.length;
  while (p % 4 && p < buf.length) p++; // align to 4
  const ffi = {
    signature: readZ(buf, p, 8),
    structure: '0x' + buf.readUInt16LE(p + 4).toString(16),
    fileVersionMS: buf.readUInt32LE(p + 12), fileVersionLS: buf.readUInt32LE(p + 16),
    productVersionMS: buf.readUInt32LE(p + 20), productVersionLS: buf.readUInt32LE(p + 24),
    fileFlagsMask: '0x' + buf.readUInt32LE(p + 28).toString(16),
    fileFlags: '0x' + buf.readUInt32LE(p + 32).toString(16),
    fileOS: '0x' + buf.readUInt32LE(p + 36).toString(16),
    fileType: '0x' + buf.readUInt32LE(p + 40).toString(16),
  };
  p += buf.readUInt16LE(p); // FixedFileInfo length
  while (p % 4 && p < buf.length) p++;
  const strings = {};
  const end = idx - 6 + valueLen;
  for (let guard = 0; guard < 64 && p < end; guard++) {
    const blockLen = buf.readUInt16LE(p);
    if (!blockLen || p + blockLen > buf.length) break;
    const keyStart = p + 6;
    let q = keyStart;
    let key = '';
    while (q + 1 < buf.length) {
      const ch = buf.readUInt16LE(q); q += 2;
      if (!ch) break; key += String.fromCharCode(ch);
    }
    while (q % 4 && q < buf.length) q++;
    const valLen = buf.readUInt16LE(p + 4);
    if (key && valLen) {
      // valLen in bytes for string values = chars+1
      const chars = Math.max(0, Math.floor(valLen / 2) - 1);
      let s = '';
      for (let i = 0; i < chars; i++) {
        const c = buf.readUInt16LE(q + i * 2);
        if (!c) break; s += String.fromCharCode(c);
      }
      strings[key] = s;
    }
    p += blockLen;
    while (p % 4 && p < buf.length) p++;
  }
  return { valueLen, type, fixed: ffi, strings };
}

function extractStrings(buf, { min = 4, utf16 = true, gbk = false } = {}) {
  const res = { ascii: [], wide: [], gbk: [] };
  // ASCII (printable)
  let cur = [];
  for (let i = 0; i < buf.length; i++) {
    const b = buf[i];
    if (b >= 0x20 && b < 0x7f) cur.push(String.fromCharCode(b));
    else { if (cur.length >= min) res.ascii.push(cur.join('')); cur = []; }
  }
  if (cur.length >= min) res.ascii.push(cur.join(''));
  // UTF-16LE runs (allow CJK + ascii)
  if (utf16) {
    let run = [];
    for (let i = 0; i + 1 < buf.length; i += 2) {
      const u = buf.readUInt16LE(i);
      const ok = (u >= 0x20 && u < 0x7f) || u >= 0x3000 || u >= 0x00c0 && u <= 0x024f || u >= 0xff00 && u <= 0xffef || u >= 0x2000 && u <= 0x2103;
      if (ok) run.push(String.fromCharCode(u));
      else { if (run.length >= min) res.wide.push(run.join('')); run = []; }
    }
    if (run.length >= min) res.wide.push(run.join(''));
  }
  // GBK runs: lead 0x81-0xfe + trail 0x40-0xfe (excluding 0x7f), or ascii printable
  if (gbk) {
    const dec = (() => { try { return new TextDecoder('gbk'); } catch (e) { return null; } })();
    let run = [];
    for (let i = 0; i < buf.length;) {
      const b = buf[i];
      if (b >= 0x81 && b <= 0xfe && i + 1 < buf.length) {
        const t = buf[i + 1];
        if ((t >= 0x40 && t <= 0x7e) || t >= 0x80) {
          const pair = Buffer.from([b, t]);
          const s = dec ? dec.copyDecode ? null : null : null;
          run.push(dec ? safeDecode(dec, pair) : '<' + buf2hex(pair) + '>');
          i += 2; continue;
        }
      }
      if (b >= 0x20 && b < 0x7f) { run.push(String.fromCharCode(b)); i++; continue; }
      if (run.length >= min) { const s = run.join(''); if (/[\u4e00-\u9fff]/.test(s)) res.gbk.push(s); }
      run = []; i++;
    }
    if (run.length >= min) { const s = run.join(''); if (/[\u4e00-\u9fff]/.test(s)) res.gbk.push(s); }
  }
  return res;
}
function safeDecode(dec, pair) {
  const prev = dec.fatal;
  try { const s = new TextDecoder('gbk', { fatal: false }).decode(pair); return s; } catch (e) { return '?'; }
}

function blobProbe(buf, file) {
  const out = { file, bytes: buf.length, sha256: sha256(buf) };
  const head = buf.slice(0, Math.min(64, buf.length));
  out.head_hex = buf2hex(head);
  out.head_ascii = head.map(b => (b >= 0x20 && b < 0x7f) ? String.fromCharCode(b) : '.').join('');
  out.entropy = +entropy(buf).toFixed(4);
  out.entropy_1k = (() => {
    const step = Math.max(1024, Math.floor(buf.length / 32));
    const rows = [];
    for (let o = 0; o < buf.length; o += step) {
      const slice = buf.slice(o, Math.min(buf.length, o + step));
      rows.push({ off: o, ent: +entropy(slice).toFixed(3) });
    }
    return rows.slice(0, 64);
  })();
  // look for zlib streams
  const zoff = [];
  for (let i = 0; i + 2 < buf.length && zoff.length < 40; i++) {
    if ((buf[i] === 0x78) && (buf[i + 1] === 0x9c || buf[i + 1] === 0x01 || buf[i + 1] === 0xda)) {
      try {
        const infl = zlib.inflateSync(buf.slice(i), { finishFlush: zlib.constants.ZLIB_SYNC_FLUSH });
        if (infl.length >= 512) { zoff.push({ offset: i, inflated: infl.length, sha256: sha256(infl), head_hex: buf2hex(infl.slice(0, 24)), ascii: infl.slice(0, 80).map(b => (b >= 0x20 && b < 0x7f) ? String.fromCharCode(b) : '.').join('') }); i += 8; }
      } catch (e) { /* not a valid stream here */ }
    }
  }
  out.zlib_candidates = zoff;
  return out;
}

const mode = process.argv[2];
const files = process.argv.slice(3).filter(a => !a.startsWith('--'));
const opts = { gbk: process.argv.includes('--gbk'), utf16: !process.argv.includes('--no-utf16') };
const results = [];
for (const f of files) {
  const buf = fs.readFileSync(f);
  if (mode === 'pe') {
    const r = parsePE(buf, f);
    const s = extractStrings(buf, { min: 4, ...opts });
    r.strings = { ascii_count: s.ascii.length, wide_count: s.wide.length, gbk_count: s.gbk.length, gbk_sample: s.gbk.slice(0, 400), wide_sample: s.wide.slice(0, 150) };
    results.push(r);
  } else if (mode === 'blob') {
    results.push(blobProbe(buf, f));
  } else if (mode === 'strings') {
    results.push({ file: f, bytes: buf.length, strings: extractStrings(buf, { min: 4, ...opts }) });
  } else { console.error('mode?'); process.exit(2); }
}
const outPath = process.env.OUT || null;
const text = JSON.stringify(results, null, 1);
if (outPath) { fs.mkdirSync(path.dirname(outPath), { recursive: true }); fs.writeFileSync(outPath, text, 'utf8'); console.error('wrote ' + outPath + ' (' + text.length + ' chars)'); }
else console.log(text);
