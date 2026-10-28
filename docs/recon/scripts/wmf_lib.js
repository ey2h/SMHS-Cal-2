'use strict';
/*
 * wmf_lib.js — 纯 Node 标准库的 WMF/WMZ/(EMF) 解析器
 * 目标：抽取文本记录（META_TEXTOUT / META_EXTTEXTOUT / W 变体）、
 *       字体记录（META_CREATEFONTINDIRECT(W)）里的 facename/字高/字符集，
 *       以及 META_ESCAPE 注释（OLE Equation 常见）。
 * 不依赖任何第三方包。
 */
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const GBK = (() => {
  try {
    return new TextDecoder('gbk', { fatal: false });
  } catch (e) {
    try {
      return new TextDecoder('gb18030', { fatal: false });
    } catch (e2) {
      return new TextDecoder('utf-8', { fatal: false });
    }
  }
})();
const GB18030 = (() => {
  try {
    return new TextDecoder('gb18030', { fatal: false });
  } catch (e) {
    return GBK;
  }
})();

function decGbk(buf) {
  return GBK.decode(buf).replace(/\u0000+$/, '');
}
function decUtf16le(buf) {
  return new TextDecoder('utf-16le').decode(buf).replace(/\u0000+$/, '');
}

/* ---------------- WMF 记录函数表 ---------------- */
const FUNC = {
  0x0000: 'META_EOF',
  0x0001: 'META_SAVEFILE',
  0x000f: 'META_ESCAPE',
  0x0014: 'META_MOVETO',
  0x0015: 'META_LINETO',
  0x0016: 'META_POLYLINE',
  0x0017: 'META_POLYGON',
  0x0018: 'META_POLYPOLYGON',
  0x001e: 'META_SAVEDC',
  0x001f: 'META_RESTOREDC',
  0x0020: 'META_SETWINDOWEXT',
  0x0021: 'META_SETWINDOWORG',
  0x0022: 'META_SETSCALEWINDOWEXT',
  0x0023: 'META_SETSCALEWINDOWORG',
  0x0027: 'META_DELETEOBJECT',
  0x0032: 'META_SETMAPMODE',
  0x0033: 'META_SETBKCOLOR',
  0x0037: 'META_CREATEPEN',
  0x0038: 'META_SELECTOBJECT',
  0x0039: 'META_COPYPALETTE',
  0x0043: 'META_SETEXTBKCOLOR',
  0x0102: 'META_SETTEXTALIGN',
  0x0104: 'META_COMMENTW',
  0x0108: 'META_EXCLUDECLIPRECT',
  0x010c: 'META_INTERSECTCLIPRECT',
  0x0128: 'META_SELECTPALETTE',
  0x0134: 'META_ANIMATEPALETTE',
  0x0135: 'META_REALIZEPALETTE',
  0x0142: 'META_EXTTEXTUREEXT',
  0x0143: 'META_SETEXTBKCOLOR',
  0x0145: 'META_OFFSETCLIPRGN',
  0x0201: 'META_SETBKCOLOR',
  0x0202: 'META_SETTEXTCOLOR',
  0x0203: 'META_SETPIXELV',
  0x0204: 'META_OFFSETCLIPRGN',
  0x0205: 'META_OFFSETWINDOWORG',
  0x0206: 'META_SCALEWINDOWEXT',
  0x0207: 'META_FLOODFILL',
  0x020c: 'META_POLYPOLYLINE',
  0x0210: 'META_SETBRUSHORGEX',
  0x0212: 'META_CREATEPAT',
  0x0213: 'META_CREATEPATTERNBRUSH',
  0x0220: 'META_DIBCREATEPATTERNBRUSH',
  0x0228: 'META_SETICMPROFILE',
  0x022c: 'META_STRETCHDIBITS',
  0x0257: 'META_EXTSELECTCLIPRGN',
  0x025d: 'META_BITBLT',
  0x0261: 'META_STRETCHBLT',
  0x027e: 'META_CREATEBRUSHINDIRECT',
  0x02fc: 'META_CREATEBRUSHINDIRECT',
  0x02fe: 'META_PATBLT',
  0x0402: 'META_SCALEWINDOWEXT',
  0x0410: 'META_SETBRUSHORGEX',
  0x0414: 'META_MOVETOEX',
  0x0415: 'META_LINETO',
  0x0416: 'META_POLYLINE',
  0x0417: 'META_POLYGON',
  0x0418: 'META_POLYPOLYGON',
  0x041e: 'META_SAVEDC',
  0x041f: 'META_RESTOREDC',
  0x0420: 'META_SETWINDOWEXT',
  0x0421: 'META_SETWINDOWORG',
  0x0426: 'META_CREATEPENINDIRECT',
  0x0434: 'META_SELECTPALETTE',
  0x0437: 'META_CREATEPEN',
  0x0438: 'META_SELECTOBJECT',
  0x0439: 'META_COPYPALETTE',
  0x043b: 'META_CREATEPALETTE',
  0x043f: 'META_RESIZEPALETTE',
  0x0443: 'META_SETEXTBKCOLOR',
  0x0445: 'META_OFFSETCLIPRGN',
  0x0512: 'META_CREATEFONTINDIRECT',
  0x0521: 'META_TEXTOUT',
  0x0538: 'META_SELECTOBJECT',
  0x0583: 'META_SETLAYOUT',
  0x0912: 'META_CREATEFONTINDIRECTW',
  0x0921: 'META_TEXTOUTW',
  0x0938: 'META_SELECTOBJECT',
  0x0a32: 'META_EXTTEXTOUT',
  0x0e32: 'META_EXTTEXTOUTW',
  0x0a31: 'META_POLYTEXTOUT',
  0x0830: 'META_ESCAPE2',
  0x00f7: 'META_CREATEPALETTE',
  0x00fd: 'META_ANIMATEPALETTE',
};

const TEXT_FUNCS = new Set([0x0521, 0x0921, 0x0a32, 0x0e32, 0x0a31]);
const FONT_FUNCS = new Set([0x0512, 0x0912]);

const CHARSETS = {
  0: 'ANSI', 1: 'DEFAULT', 2: 'SYMBOL', 77: 'MAC', 86: 'SHIFTJIS', 88: 'HANGEUL',
  128: 'GB2312(中文)', 129: 'GB2312(中文)', 134: 'JOHAB', 136: 'GB2312(中文)',
  161: 'GREEK', 162: 'TURKISH', 177: 'RUSSIAN', 178: 'EASTEUROPE', 186: 'OEM',
};

/* ---------------- 头解析 ---------------- */
function parseHeaders(buf) {
  const info = {
    kind: null, placeable: null, standard: null, recordStart: 0,
    notes: [],
  };
  if (buf.length < 34) { info.kind = 'too-small'; return info; }

  const w0 = buf.readUInt16LE(0);
  const placeableMagics = [0xd7cd, 0x9ac6, 0xcdd7, 0xf9ac];
  const b = buf.readUInt32LE(0);

  let off = 0;
  // Aldus placeable header (22 bytes)
  if (w0 === 0x9ac6 || w0 === 0xcdd7 || (b & 0xffff) === 0xd7cd) {
    info.placeable = {
      magic: '0x' + w0.toString(16),
      version: buf.readUInt16LE(2),
      lower: buf.readInt16LE(4), upper: buf.readInt16LE(6),
      right: buf.readInt16LE(8), bottom: buf.readInt16LE(10),
      inset: buf.readUInt16LE(12),
      wordAvg: buf.readUInt16LE(14),
      creation: buf.readUInt32LE(16),
      lastModified: buf.readUInt32LE(20),
    };
    off = 22;
    info.kind = 'placeable';
  }
  const type = buf.readUInt16LE(off);
  const headerSize = buf.readUInt16LE(off + 2);
  const version = buf.readUInt16LE(off + 4);
  if (type !== 0x0001 && type !== 0x0002) {
    // 不是标准 WMF
    info.kind = info.kind || 'not-wmf';
    info.notes.push('标准头 Type=0x' + type.toString(16) + ' 不是 0x0001/0x0002');
    return info;
  }
  info.kind = info.kind || (type === 2 ? 'standard-file' : 'standard');
  info.standard = {
    type, headerSize, version: '0x' + version.toString(16),
    noDiscardable: buf.readUInt16LE(off + 6),
    reserved: buf.readUInt16LE(off + 8),
    checksum: buf.readUInt16LE(off + 10),
    // windows version / device / sizes 在 0x0C..
    maxRecordSizeWords: buf.readUInt16LE(off + 0x1E),
    numObjects: buf.readUInt16LE(off + 0x1C),
  };
  info.recordStart = off + headerSize;
  if (info.recordStart >= buf.length) {
    info.notes.push('记录起点越界 recordStart=' + info.recordStart + ' fileLen=' + buf.length);
  }
  return info;
}

/* ---------------- 文本抽取 ---------------- */
function scoreText(s) {
  if (!s) return -1;
  let good = 0, bad = 0;
  for (const ch of s) {
    const c = ch.codePointAt(0);
    if (c === 0xfffd) { bad += 3; continue; }
    if (c === 0) continue;
    if (c >= 0x20 && c <= 0x7e) { good += 2; continue; }
    if (c >= 0x4e00 && c <= 0x9fff) { good += 3; continue; }
    if (c >= 0x370 && c <= 0x3ff) { good += 4; continue; } // 希腊
    if (c >= 0x300 && c <= 0x36f) { good += 4; continue; } // 组合符
    if (c >= 0x2000 && c <= 0x22ff) { good += 4; continue; } // 通用标点/数学符号
    if (c >= 0x3000 && c <= 0x30ff) { good += 2; continue; } // CJK 标点/假名
    if (c === 0xb5 || c === 0xb1 || c === 0xd7 || c === 0xf7 || c >= 0xb0 && c <= 0xff) { good += 3; continue; }
    if (c >= 0x2070 && c <= 0x209f) { good += 5; continue; } // 上下标
    if (c < 0x20) { bad += 2; continue; }
    good += 1;
  }
  return good - bad;
}

function tryDecodeText(region) {
  // 返回 [{text, enc, offset}] 候选
  const cands = [];
  const push = (text, enc, offset) => {
    if (!text) return;
    const t = text.replace(/[\u0000\r\n\t]+/g, ' ').trim();
    if (t) cands.push({ text: t, enc, offset, score: scoreText(t) });
  };
  // GBK 全文
  push(decGbk(region), 'gbk', 0);
  push(decGb18030Safe(region), 'gb18030', 0);
  // 去掉尾部奇数字节 padding
  if (region.length > 1) push(decGbk(region.subarray(0, region.length - 1)), 'gbk(去pad)', 0);
  // UTF-16LE 全文
  if (region.length >= 2) {
    push(decUtf16le(region), 'utf16le', 0);
    if (region.length >= 3) push(decUtf16le(region.subarray(2)), 'utf16le(+2)', 2);
    if (region.length >= 6) push(decUtf16le(region.subarray(4)), 'utf16le(+4)', 4);
    if (region.length >= 8) push(decUtf16le(region.subarray(6)), 'utf16le(+6)', 6);
  }
  // GBK 从各 options 偏移起
  for (const o of [4, 6, 8, 10, 12, 14, 16, 18, 20]) {
    if (region.length > o) {
      push(decGbk(region.subarray(o)), 'gbk(+' + o + ')', o);
    }
  }
  cands.sort((a, b) => b.score - a.score);
  return cands;
}
function decGb18030Safe(buf) { return GB18030.decode(buf).replace(/\u0000+$/, ''); }

/* ---------------- WMF 记录遍历 ---------------- */
function walkWmf(buf, start, hdr, opts = {}) {
  const recs = [];
  const errors = [];
  let p = start;
  let guard = 0;
  const maxRecs = opts.maxRecords || 200000;
  while (p + 6 <= buf.length && guard++ < maxRecs) {
    const func = buf.readUInt16LE(p);
    const sizeWords = buf.readUInt32LE(p + 2);
    const name = FUNC[func] || ('UNKNOWN_0x' + func.toString(16).toUpperCase());
    if (func === 0x0000 && sizeWords === 0) {
      recs.push({ func, name, at: p, sizeWords, dataAt: p + 6, dataLen: 0 });
      break;
    }
    // 合理性检查：WMF 允许 size=0 的记录（NOOP 等）
    const dataLen = sizeWords * 2;
    if (dataLen > buf.length - (p + 6)) {
      errors.push({ at: p, func, name, reason: `Size=${sizeWords}词 越界（剩余 ${buf.length - (p + 6)} 字节）` });
      break;
    }
    const rec = { func, name, at: p, sizeWords, dataAt: p + 6, dataLen };
    recs.push(rec);

    if (TEXT_FUNCS.has(func)) {
      const d = rec.dataAt;
      if (rec.dataLen >= 8) {
        const x = buf.readInt16LE(d), y = buf.readInt16LE(d + 2);
        const cch = buf.readUInt16LE(d + 4);
        const cbString = buf.readUInt16LE(d + 6);
        const region = buf.subarray(d + 8, d + rec.dataLen);
        const cands = tryDecodeText(region);
        rec.text = { x, y, cch, cbString, regionLen: region.length, candidates: cands.slice(0, 4) };
      } else {
        errors.push({ at: p, func, name, reason: '文本记录长度 < 8 字节' });
      }
    } else if (FONT_FUNCS.has(func)) {
      const f = parseLogFont(buf, rec.dataAt, rec.dataLen, func === 0x0912);
      rec.font = f;
      if (f && f.error) errors.push({ at: p, func, name, reason: f.error });
    } else if (func === 0x000f) {
      const esc = parseEscape(buf, rec.dataAt, rec.dataLen);
      rec.escape = esc;
    }

    p += 6 + dataLen;
  }
  return { recs, errors, endedAt: p };
}

function parseLogFont(buf, at, len, wide) {
  // WMF MetafileLogFont: 通常 4 字节占位 + LOGFONT(15 字节) + FaceName(32*2 for wide)
  try {
    let base = at;
    // 探测是否有 4 字节占位（常见实现里 WMF 记录含 unused DWORD）
    const variants = [];
    for (const skip of [0, 4]) {
      const o = base + skip;
      if (o + 15 > buf.length) continue;
      const height = buf.readInt16LE(o);
      const width = buf.readInt16LE(o + 2);
      const escapement = buf.readInt16LE(o + 4);
      const orientation = buf.readInt16LE(o + 6);
      const weight = buf.readInt16LE(o + 8);
      const italic = buf[o + 10], underline = buf[o + 11], strikeout = buf[o + 12];
      const charset = buf[o + 13], outprecision = buf[o + 14];
      const faceAt = o + 15;
      let faceName = null, faceEnc = null;
      if (wide) {
        const raw = buf.subarray(faceAt, Math.min(buf.length, faceAt + 64));
        faceName = decUtf16le(raw).replace(/\u0000.*/s, '');
        faceEnc = 'utf16le';
      } else {
        const raw = buf.subarray(faceAt, Math.min(buf.length, faceAt + 32));
        faceName = decGbk(raw).replace(/\u0000.*/s, '');
        faceEnc = 'gbk';
      }
      variants.push({
        skip, height, width, escapement, orientation, weight,
        italic: !!italic, underline: !!underline, strikeout: !!strikeout,
        charset, charsetName: CHARSETS[charset] || ('0x' + charset.toString(16)),
        outprecision, faceName, faceEnc,
      });
    }
    // 选择 faceName 更像字体名的候选（可打印 ASCII 为主）
    variants.sort((a, b) => {
      const q = (v) => {
        const n = v.faceName || '';
        if (!n) return -10;
        let ok = 0;
        for (const ch of n) { const c = ch.codePointAt(0); if (c >= 0x20 && c <= 0x7e) ok += 2; else if (c >= 0x4e00 && c <= 0x9fff) ok += 2; else ok -= 4; }
        return ok - (v.skip ? 0 : 0);
      };
      return q(b) - q(a);
    });
    const best = variants[0];
    if (!best) return { error: 'LOGFONT 区域不足' };
    if (!best.faceName || !/^[\x20-\x7e\u4e00-\u9fff]/.test(best.faceName)) {
      return { ...best, error: 'facename 不可打印（可能是布局变体不同）', rawHex: buf.subarray(base, Math.min(buf.length, base + len)).toString('hex').slice(0, 160) };
    }
    return best;
  } catch (e) {
    return { error: e.message };
  }
}

function parseEscape(buf, at, len) {
  // WMF MetafileEscape: escape(2), function(2) or count..., 变体多；保守提取可打印串
  const raw = buf.subarray(at, at + len);
  const ascii = decGbk(raw).replace(/[^\x20-\x7e\u4e00-\u9fff]+/g, ' ').trim();
  return { len, headHex: raw.subarray(0, Math.min(32, len)).toString('hex'), printable: ascii.slice(0, 400) };
}

/* ---------------- WMZ (zip) ---------------- */
function readWmz(buf) {
  // 最小 ZIP 读取（stored / deflate）
  const EOCD_SIG = 0x06054b50;
  let eocd = -1;
  for (let i = buf.length - 22; i >= 0 && i > buf.length - 65558; i--) {
    if (buf.readUInt32LE(i) === EOCD_SIG) { eocd = i; break; }
  }
  if (eocd < 0) return { error: '未找到 ZIP EOCD（WMZ 不是标准 zip？）' };
  const nEntries = buf.readUInt16LE(eocd + 10);
  const cdOff = buf.readUInt32LE(eocd + 16);
  const out = [];
  let p = cdOff;
  for (let i = 0; i < nEntries; i++) {
    if (p + 46 > buf.length || buf.readUInt32LE(p) !== 0x02014b50) break;
    const method = buf.readUInt16LE(p + 10);
    const compSize = buf.readUInt32LE(p + 20);
    const uncompSize = buf.readUInt32LE(p + 24);
    const nameLen = buf.readUInt16LE(p + 28);
    const extraLen = buf.readUInt16LE(p + 30);
    const commentLen = buf.readUInt16LE(p + 32);
    const localOff = buf.readUInt32LE(p + 42);
    const name = decGbk(buf.subarray(p + 46, p + 46 + nameLen));
    // 本地头
    if (buf.readUInt32LE(localOff) === 0x04034b50) {
      const ln = buf.readUInt16LE(localOff + 26);
      const le = buf.readUInt16LE(localOff + 28);
      const dataAt = localOff + 30 + ln + le;
      const comp = buf.subarray(dataAt, dataAt + compSize);
      let data = null, derr = null;
      try {
        if (method === 0) data = comp;
        else if (method === 8) data = zlib.inflateRawSync(comp);
        else derr = '不支持的压缩方法 ' + method;
      } catch (e) { derr = 'inflate 失败: ' + e.message; }
      out.push({ name, method, compSize, uncompSize, data, error: derr });
    }
    p += 46 + nameLen + extraLen + commentLen;
  }
  return { entries: out };
}

/* ---------------- EMF（轻量支持） ---------------- */
function isEmf(buf) {
  return buf.length >= 88 && buf.readUInt32LE(0) === 1 && buf.readUInt32LE(4) >= 88;
}
const EMR = { 8: 'EMR_TEXTOUT', 12: 'EMR_EXTTEXTOUTW', 108: 'EMR_SMALLTEXTOUT', 14: 'EMR_POLYTEXTOUTW', 84: 'EMR_EXTCREATEGLYPHS' };
function walkEmf(buf) {
  const recs = [];
  const errors = [];
  let p = 88; // 头部大小 (EMFHEADER size 字段决定)
  const hdrSize = buf.readUInt32LE(4);
  p = hdrSize;
  let guard = 0;
  while (p + 8 <= buf.length && guard++ < 200000) {
    const type = buf.readUInt32LE(p);
    const size = buf.readUInt32LE(p + 4);
    if (size < 8 || p + size > buf.length) { errors.push({ at: p, type, reason: 'size 越界' }); break; }
    const rec = { type, name: EMR[type] || ('EMR_' + type), at: p, size };
    if (type === 8 || type === 12 || type === 14) {
      // EMREXTTEXTOUTW: bounds(16) iGraphicsMode(4) exColor(4) emrText(EmrTextW: count(4) start(4) String)
      const textAt = p + 16 + 8;
      if (textAt + 8 <= p + size) {
        const count = buf.readUInt32LE(textAt);
        const strAt = textAt + 8;
        const raw = buf.subarray(strAt, Math.min(p + size, strAt + count * 2));
        const s = decUtf16le(raw);
        rec.text = { count, candidates: [{ text: s.replace(/[\u0000]+/g, ' ').trim(), enc: 'utf16le', score: scoreText(s) }] };
      }
    } else if (type === 108) {
      // EMR_SMALLTEXTOUT: bounds(16) flags(4) x(4) y(4) cch(4) uString(4) String variable
      const flags = buf.readUInt32LE(p + 16);
      const cchOff = p + 16 + 4 + 4 + 4 + 4 + 4;
      const cch = buf.readUInt32LE(p + 16 + 4 + 4 + 4 + 4);
      const isUnicode = (flags & 0x2) === 0; // EM_STF_UNICODE = 0x2 when NOT unicode? 保守：两种都试
      const raw = buf.subarray(cchOff + 4, p + size);
      const cands = tryDecodeText(raw);
      rec.text = { count: cch, isUnicode, candidates: cands.slice(0, 3) };
    } else if (type === 25) {
      // EMR_COMMENT: comment(4) Data
      const raw = buf.subarray(p + 12, p + size);
      rec.escape = { printable: decGbk(raw).replace(/[^\x20-\x7e]+/g, ' ').trim().slice(0, 200) };
    }
    if (type === 0) { recs.push(rec); break; }
    recs.push(rec);
    p += size;
  }
  return { recs, errors };
}

module.exports = { parseHeaders, walkWmf, walkEmf, isEmf, readWmz, decGbk, decUtf16le, scoreText, FUNC, TEXT_FUNCS, CHARSETS };
