'use strict';
/*
 * wmf_extract.js — 汇宝 WMF/WMZ 文字抽取主程序（Node 标准库，只读安装目录）
 *
 * 已实测确定的记录布局（本软件产出的 WMF）：
 *   [Size:DWORD = 整条记录字数(含 Size+Function)][Function:WORD][data...]
 *   Size 字段=4 时 data 长 2 字节；Size 字段整体 = size*2 字节。
 * 文本记录（0x0A32 EXTTEXTOUT 实测）：x(WORD) y(WORD) cbString(WORD) 0(WORD) String[cbString](补零到偶数字节)，
 *   之后还有若干与字符数相关的 WORD 块（本软件私有，不参与文本）。
 * 解析失败/无文本一律如实报告。
 *
 * 用法:
 *   node wmf_extract.js all            -> 扫描安装目录全部 wmf/emf/wmz + chm_extract，产出 JSON
 *   node wmf_extract.js report         -> 由 JSON 产出 markdown
 *   node wmf_extract.js dump <file>    -> 单文件转储
 */
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const ROOT = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0';
const OUT_DIR = 'C:\\code\\Glass\\docs\\recon\\reports';
const JSON_PATH = path.join(OUT_DIR, 'wmf_formulas.json');
const MD_PATH = path.join(OUT_DIR, 'wmf_formulas.md');
const CHM_DIR = 'C:\\code\\Glass\\docs\\recon\\chm_extract';

const GBK = new TextDecoder('gbk', { fatal: false });
const GB18030 = new TextDecoder('gb18030', { fatal: false });
const U16 = new TextDecoder('utf-16le', { fatal: false });

/* ---------------- 工具 ---------------- */
function decGbk(buf) { return GBK.decode(buf); }
function hasBad(s) { return s.indexOf('\uFFFD') >= 0; }
function cleanText(s) {
  return s.replace(/\u0000+$/g, '').replace(/[\u0000\u0001-\u0008]+/g, '').trim();
}
function scoreText(s) {
  if (!s) return -100;
  let good = 0, bad = 0, n = 0;
  for (const ch of s) {
    const c = ch.codePointAt(0);
    n++;
    if (c === 0xfffd) { bad += 6; continue; }
    if (c >= 0x20 && c <= 0x7e) { good += 2; continue; }
    if (c >= 0x4e00 && c <= 0x9fff) { good += 4; continue; }
    if (c >= 0x370 && c <= 0x3ff) { good += 5; continue; }
    if (c >= 0x300 && c <= 0x36f) { good += 5; continue; }
    if (c >= 0x2000 && c <= 0x22ff) { good += 5; continue; }
    if (c >= 0x2070 && c <= 0x209f) { good += 6; continue; }
    if (c >= 0x3000 && c <= 0x30ff) { good += 3; continue; }
    if (c >= 0xb0 && c <= 0xff) { good += 4; continue; }
    if (c < 0x20) { bad += 4; continue; }
    good += 1;
  }
  const ratio = (good) / Math.max(1, good + bad);
  return (good - bad) + (ratio > 0.85 ? 8 : ratio > 0.6 ? 2 : -6) - Math.max(0, 8 - n);
}

/* ---------------- WMF 记录遍历（实测布局） ---------------- */
const FUNC_NAMES = {
  0x0000: 'META_EOF',
  0x000f: 'META_ESCAPE',
  0x00f7: 'META_CREATEPALETTE',
  0x0102: 'META_SETTEXTALIGN',
  0x0103: 'META_SETTEXTCHAREXTRA',
  0x0104: 'META_SETTEXTCOLOR?',
  0x0201: 'META_CREATEPEN?',
  0x0209: 'META_SELECTOBJECT?',
  0x020b: 'META_SETWINDOWEXT?',
  0x020c: 'META_SETWINDOWORG?',
  0x0234: 'META_REALIZEPALETTE?',
  0x0035: 'META_SELECTPALETTE?',
  0x012d: 'META_SELECTOBJECT?(obs)',
  0x012e: 'META_DELETEOBJECT?(obs)',
  0x01f0: 'META_MOVETO?(obs)',
  0x02fa: 'META_CREATEBRUSH?(obs)',
  0x02fb: 'META_CREATEBRUSH?(obs)',
  0x02fc: 'META_CREATEBRUSHINDIRECT',
  0x0324: 'META_POLYLINE?(obs,点数+点列)',
  0x0325: 'META_POLYLINE2?(obs)',
  0x0414: 'META_MOVETOEX',
  0x0415: 'META_LINETO',
  0x0416: 'META_POLYLINE',
  0x0417: 'META_POLYGON',
  0x0418: 'META_POLYPOLYGON',
  0x0438: 'META_SELECTOBJECT',
  0x0512: 'META_CREATEFONTINDIRECT',
  0x0912: 'META_CREATEFONTINDIRECTW',
  0x0521: 'META_TEXTOUT',
  0x0921: 'META_TEXTOUTW',
  0x0a32: 'META_EXTTEXTOUT',
  0x0e32: 'META_EXTTEXTOUTW',
  0x0a31: 'META_POLYTEXTOUT',
  0x041f: 'META_RESTOREDC',
  0x041e: 'META_SAVEDC',
};
const TEXT_FUNCS = new Set([0x0521, 0x0921, 0x0a32, 0x0e32, 0x0a31]);
const FONT_FUNCS = new Set([0x0512, 0x0912]);
const DRAW_FUNCS = new Set([0x0324, 0x0325, 0x0414, 0x0415, 0x0416, 0x0417, 0x0418, 0x01f0, 0x0106, 0x0436]);

function walkRecords(buf, start) {
  const recs = [];
  let p = start;
  let guard = 0;
  while (p + 6 <= buf.length && guard++ < 300000) {
    const size = buf.readUInt32LE(p);
    const func = buf.readUInt16LE(p + 4);
    if (size < 3) return null;
    const total = size * 2;
    if (p + total > buf.length) return null;
    recs.push({ p, size, func, dataAt: p + 6, dataLen: total - 6 });
    if (func === 0 && size === 3) return { recs, end: p + total, start };
    p += total;
  }
  return null;
}

function findRecords(buf, hdrEnd) {
  const cands = [];
  const starts = [];
  for (const s of [hdrEnd, 22, 34, 40, 46, 54, 56, 58, 64, 68]) {
    if (s >= buf.length) continue;
    if (!starts.includes(s)) starts.push(s);
  }
  for (const s of starts) {
    const r = walkRecords(buf, s);
    if (r && r.end === buf.length) cands.push(r);
  }
  if (!cands.length) return { recs: null, note: '记录链无法干净走到 EOF（Size 字段布局不匹配或文件被截断）' };
  // 取起点最早的一条（覆盖最多记录），并确认其起点唯一
  cands.sort((a, b) => a.start - b.start || b.recs.length - a.recs.length);
  return { recs: cands[0], note: null, altStarts: cands.map((c) => c.start) };
}

/* ---------------- 头解析 ---------------- */
function parseHeader(buf) {
  const out = { kind: null, placeable: null, standard: null, recordsAt: null, notes: [] };
  if (buf.length < 24) { out.kind = 'too-small'; return out; }
  let off = 0;
  const w0 = buf.readUInt16LE(0);
  if (w0 === 0xcdd7 || w0 === 0x9ac6) {
    out.placeable = {
      magic: '0x' + w0.toString(16),
      version: '0x' + buf.readUInt16LE(2).toString(16),
      lower: buf.readInt16LE(4), upper: buf.readInt16LE(6),
      right: buf.readInt16LE(8), bottom: buf.readInt16LE(10),
      inset: buf.readUInt16LE(12), wordAvg: buf.readUInt16LE(14),
      creation: buf.readUInt32LE(16), lastModified: buf.readUInt32LE(20),
    };
    out.kind = 'placeable';
    off = 22;
  }
  if (off + 18 > buf.length) { out.kind = out.kind || 'too-small'; return out; }
  const type = buf.readUInt16LE(off);
  const f2 = buf.readUInt16LE(off + 2);
  const ver = buf.readUInt16LE(off + 4);
  if (type === 0x0001 || type === 0x0002) {
    out.kind = out.kind || (type === 2 ? 'standard-file' : 'standard');
    out.standard = { type, field2: '0x' + f2.toString(16), version: '0x' + ver.toString(16) };
    // 16 位风格：Type+0x0009+0x0300 共 18 字节头；现代 WMF：HeaderSize 0x22
    const headerLen = f2 === 0x0022 ? 0x22 : 18;
    out.recordsAt = off + headerLen;
  } else {
    out.kind = out.kind || 'not-wmf';
    out.notes.push('Type=0x' + type.toString(16) + ' 非 0x0001/0x0002');
  }
  return out;
}

/* ---------------- 文本解码（多布局候选） ---------------- */
function decodeTextFromRecord(buf, rec, func) {
  const d = rec.dataAt;
  const dl = rec.dataLen;
  const cands = [];
  const addCand = (bytes, tag, fields) => {
    if (!bytes || !bytes.length) return;
    let s = decGbk(bytes);
    s = cleanText(s);
    if (!s) return;
    const sc = scoreText(s);
    const bad = hasBad(s);
    cands.push({ text: s, layout: tag, enc: bad ? 'gbk(含非法字节)' : 'gbk', score: sc - (bad ? 8 : 0), fields });
  };
  if (dl >= 8) {
    const x = buf.readInt16LE(d), y = buf.readInt16LE(d + 2);
    const w2 = buf.readUInt16LE(d + 4), w3 = buf.readUInt16LE(d + 6);
    const region = buf.subarray(d + 8, d + dl);
    const fields = { x, y, field3: w2, field4: w3, dataLen: dl };
    // 布局 A：field3 = 字节数（实测本软件 0x0A32）
    if (w2 > 0 && w2 <= region.length) {
      addCand(region.subarray(0, w2), 'A: x,y,cbString,body', fields);
      // 奇数字节时校验 pad 字节
    }
    // 布局 B：标准 x,y,cch,cbString
    if (w3 > 0 && w3 <= region.length) addCand(region.subarray(0, w3), 'B: x,y,cch,cbString', fields);
    // 布局 C：标准 cch=field4, cbString=field3（部分实现）
    if (w2 > 0 && w2 <= region.length && w3 === 0) addCand(region.subarray(0, w2), 'C: x,y,cbString,0', fields);
    // 布局 D：整段区域
    addCand(region, 'D: 整段数据区', fields);
    // 布局 E：去掉尾部 WORD 块（每字符 2 WORD）后
    const cchGuess = w2 > 0 ? Math.ceil(w2 / 2) : 0;
    if (cchGuess > 0) {
      const trimmed = region.length - cchGuess * 4;
      if (trimmed > 8) addCand(region.subarray(0, trimmed), 'E: 去尾部字块', fields);
    }
    // UTF-16 变体
    if (func === 0x0921 || func === 0x0e32) {
      const s = cleanText(U16.decode(region));
      if (s) cands.push({ text: s, layout: 'W:utf16le', enc: 'utf16le', score: scoreText(s), fields });
    }
    // 按点数布局（ polyline 误判保护）：若 field3 很小且区域全为坐标，则不当作文本
  } else {
    const region = buf.subarray(d, d + dl);
    addCand(region, 'D: 整段数据区(短记录)', null);
  }
  cands.sort((a, b) => b.score - a.score);
  return cands;
}

/* ---------------- 通用可打印串扫描（兜底，含字体名） ---------------- */
function scanPrintableRuns(buf, minLen) {
  const runs = [];
  let i = 0;
  while (i < buf.length) {
    // 逐字节尝试 GBK：CJK 双字节或 ASCII 可打印
    const start = i;
    let out = '';
    let j = i;
    while (j < buf.length) {
      const b1 = buf[j];
      if (b1 >= 0x20 && b1 <= 0x7e) { out += String.fromCharCode(b1); j++; continue; }
      if (b1 >= 0x81 && b1 <= 0xfe && j + 1 < buf.length) {
        const b2 = buf[j + 1];
        if ((b2 >= 0x40 && b2 <= 0x7e) || (b2 >= 0x80 && b2 <= 0xfe)) {
          out += decGbk(buf.subarray(j, j + 2));
          j += 2;
          continue;
        }
      }
      break;
    }
    if (out.length >= minLen) runs.push({ at: start, text: cleanText(out) });
    i = Math.max(i + 1, j);
  }
  return runs;
}

/* ---------------- 字体记录 ---------------- */
const CHARSETS = { 0: 'ANSI', 1: 'DEFAULT', 2: 'SYMBOL', 86: 'SHIFTJIS', 128: 'GB2312(中文)', 129: 'GB2312(中文)', 134: 'JOHAB', 136: 'GB2312(中文)', 161: 'GREEK', 162: 'TURKISH', 177: 'RUSSIAN', 178: 'EASTEUROPE', 186: 'OEM' };
function parseLogFont(buf, rec, wide) {
  const variants = [];
  for (const skip of [0, 2, 4]) {
    const o = rec.dataAt + skip;
    if (o + 15 > buf.length) continue;
    const height = buf.readInt16LE(o);
    const width = buf.readInt16LE(o + 2);
    const weight = buf.readInt16LE(o + 8);
    const charset = buf[o + 13];
    const faceAt = o + 15;
    const rawLen = wide ? 64 : 32;
    const raw = buf.subarray(faceAt, Math.min(buf.length, faceAt + rawLen));
    const faceName = cleanText(wide ? U16.decode(raw) : decGbk(raw)).split('\u0000')[0];
    const okName = faceName && /^[\x20-\x7e\u4e00-\u9fff]/.test(faceName) && !hasBad(faceName) &&
      /^[A-Za-z\u4e00-\u9fff][A-Za-z0-9 _\-.一-鿿]{1,30}$/.test(faceName);
    if (okName && Math.abs(height) < 8000 && Math.abs(weight) <= 1000) {
      variants.push({ skip, height, width, weight, charset, charsetName: CHARSETS[charset] || ('0x' + charset.toString(16)), italic: !!buf[o + 10], underline: !!buf[o + 11], strikeout: !!buf[o + 12], faceName });
    }
  }
  return variants[0] || null;
}

/* ---------------- 判定是否公式图 ---------------- */
const MATH_RE = /[=≤≥±×÷∑∏∫√≈∞⌈⌉⁰¹²³⁴⁵⁶⁷⁸⁹₀₁₂₃₄₅₆₇₈₉αβγδεζηθικλμνξοπρστυφχψωΑΒΓΔΕΖΗΘΛΜΝΞΟΠΡΣΤΥΦΩ∕\/·℃μHz]|[-–]?\d\s*\/\s*\d|\/[a-zA-Z]|[a-zA-Z]\/|\^\d|_\d/i;
const MATH_TOKEN = /(标准值|设计值|系数|荷载|弯矩|剪力|挠度|应力|强度|模量|惯性矩|截面|面积|传热|换热|阻值|导热|换气|单位|面积|高度|跨度|风压|地震|重力|自重)/;
function classifyFormula(texts, geom, fonts) {
  const joined = texts.join(' ');
  const hits = [];
  if (/[=≤≥±×÷∑∫√≈]/.test(joined)) hits.push('含等号/关系符/运算符');
  if (/[αβγδεζηθικλμνξοπρστυφχψωΑΒΓΔΕΖΗΘΛΜΝΞΟΠΡΣΤΥΦΩ]/.test(joined)) hits.push('含希腊字母');
  if (/[⁰¹²³⁴⁵⁶⁷⁸⁹₀₁₂₃₄₅₆₇₈₉]/.test(joined)) hits.push('含上下标字符');
  if (/(\d|字母|[\u4e00-\u9fff])\s*\/\s*(\d|[\u4e00-\u9fff]|[a-zA-Z])/.test(joined)) hits.push('含分数/斜杠结构');
  if (MATH_TOKEN.test(joined)) hits.push('含力学/热工术语');
  if (fonts.some((f) => /symbol|euclid|mt extra|cmmnt|mathtext/i.test(f.faceName || ''))) hits.push('使用 Symbol/Math 字体');
  if (geom.polyline >= 3 && texts.length >= 2) hits.push('有文本+多条折线（分数横线/坐标轴特征）');
  return { isFormula: hits.length >= 2, reasons: hits, joined };
}

/* ---------------- 主流程 ---------------- */
function listFiles(dir, exts, out = []) {
  let ents;
  try { ents = fs.readdirSync(dir, { withFileTypes: true }); } catch (e) { return out; }
  for (const e of ents) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) listFiles(full, exts, out);
    else if (e.isFile() && exts.includes(path.extname(e.name).toLowerCase())) out.push(full);
  }
  return out;
}

function readWmz(buf) {
  let eocd = -1;
  for (let i = buf.length - 22; i >= 0 && i > buf.length - 65558; i--) {
    if (buf.readUInt32LE(i) === 0x06054b50) { eocd = i; break; }
  }
  if (eocd < 0) return { error: 'WMZ 内未找到 ZIP EOCD' };
  const n = buf.readUInt16LE(eocd + 10);
  let p = buf.readUInt32LE(eocd + 16);
  const entries = [];
  for (let i = 0; i < n; i++) {
    if (p + 46 > buf.length || buf.readUInt32LE(p) !== 0x02014b50) break;
    const method = buf.readUInt16LE(p + 10);
    const compSize = buf.readUInt32LE(p + 20);
    const uncompSize = buf.readUInt32LE(p + 24);
    const nameLen = buf.readUInt16LE(p + 28);
    const extraLen = buf.readUInt16LE(p + 30);
    const commentLen = buf.readUInt16LE(p + 32);
    const localOff = buf.readUInt32LE(p + 42);
    const name = decGbk(buf.subarray(p + 46, p + 46 + nameLen));
    if (buf.readUInt32LE(localOff) === 0x04034b50) {
      const ln = buf.readUInt16LE(localOff + 26);
      const le = buf.readUInt16LE(localOff + 28);
      const at = localOff + 30 + ln + le;
      const comp = buf.subarray(at, at + compSize);
      let data = null, derr = null;
      try {
        if (method === 0) data = comp;
        else if (method === 8) data = zlib.inflateRawSync(comp);
        else derr = 'ZIP 压缩方法 ' + method + ' 不支持';
      } catch (e) { derr = 'inflate 失败: ' + e.message; }
      entries.push({ name, method, compSize, uncompSize, data, error: derr });
    }
    p += 46 + nameLen + extraLen + commentLen;
  }
  if (!entries.length) return { error: 'WMZ 内没有可解条目' };
  return { entries };
}

function analyzeWmfBuf(buf, origin) {
  const res = {
    origin, bytes: buf.length, kind: null, header: null, parseError: null,
    records: 0, textRecords: [], fonts: [], geometry: { polyline: 0, polygon: 0, line: 0, ellipse: 0, draw: 0 },
    funcHistogram: {}, extraRuns: [], notes: [],
  };
  if (buf.length >= 8 && buf.readUInt32LE(0) === 1 && buf.length >= 88) {
    // EMF（本目录未发现，保底）
    res.kind = 'emf';
    let p = buf.readUInt32LE(4);
    while (p + 8 <= buf.length) {
      const type = buf.readUInt32LE(p), size = buf.readUInt32LE(p + 4);
      if (size < 8 || p + size > buf.length) { res.parseError = 'EMF 记录越界'; break; }
      if (type === 12 || type === 8 || type === 14) {
        const at = p + 24;
        const s = cleanText(U16.decode(buf.subarray(at, p + size)));
        if (s) res.textRecords.push({ at: p, func: type, text: s, layout: 'EMF EXTTEXTOUTW(utf16le)', score: scoreText(s) });
      }
      if (type === 0) break;
      p += size;
    }
    res.records = 0;
    return res;
  }
  const h = parseHeader(buf);
  res.kind = h.kind;
  res.header = h;
  res.notes.push(...h.notes);
  if (h.kind === 'not-wmf' || h.kind === 'too-small') { res.parseError = '不是 WMF（头不识别）'; return res; }
  const found = findRecords(buf, h.recordsAt);
  if (!found.recs) { res.parseError = found.note; return res; }
  const recs = found.recs;
  res.records = recs.length;
  res.notes.push('记录起点=' + found.start + (found.altStarts && found.altStarts.length > 1 ? '（多解起点: ' + found.altStarts.join(',') + '）' : ''));
  for (const rec of recs) {
    const nm = FUNC_NAMES[rec.func] || ('未知_0x' + rec.func.toString(16).padStart(4, '0'));
    res.funcHistogram[nm] = (res.funcHistogram[nm] || 0) + 1;
    if (TEXT_FUNCS.has(rec.func)) {
      const cands = decodeTextFromRecord(buf, rec, rec.func);
      if (cands.length && cands[0].score > 0) {
        const best = cands[0];
        res.textRecords.push({
          at: rec.p, func: '0x' + rec.func.toString(16).padStart(4, '0'), name: nm,
          x: best.fields ? best.fields.x : null, y: best.fields ? best.fields.y : null,
          cbString: best.fields ? best.fields.field3 : null,
          text: best.text, layout: best.layout, enc: best.enc, score: best.score,
          alternates: cands.slice(1, 3).map((c) => ({ text: c.text, layout: c.layout, score: c.score })),
        });
      } else {
        res.notes.push(`@${rec.p} ${nm} 记录无有效文本（候选：${cands.map((c) => JSON.stringify(c.text) + '/' + c.layout).join(' | ') || '无'}）`);
      }
    } else if (FONT_FUNCS.has(rec.func)) {
      const f = parseLogFont(buf, rec, rec.func === 0x0912);
      if (f) res.fonts.push({ at: rec.p, name: nm, ...f });
      else res.notes.push(`@${rec.p} ${nm} 无法解析 LOGFONT`);
    }
    if (DRAW_FUNCS.has(rec.func)) {
      res.geometry.draw++;
      if (rec.func === 0x0324 || rec.func === 0x0416 || rec.func === 0x0418 || rec.func === 0x0325) res.geometry.polyline++;
      if (rec.func === 0x0417) res.geometry.polygon++;
      if (rec.func === 0x0414 || rec.func === 0x0415 || rec.func === 0x01f0) res.geometry.line++;
    }
  }
  // 兜底：非文本记录里是否有字体名/文本块
  const runs = scanPrintableRuns(buf, 3).filter((r) => r.text.length >= 3 && !hasBad(r.text));
  const already = new Set(res.textRecords.map((t) => t.text));
  for (const r of runs) {
    if (already.has(r.text)) continue;
    if (scoreText(r.text) < 6) continue;
    res.extraRuns.push({ at: r.at, text: r.text, score: scoreText(r.text) });
  }
  return res;
}

function main() {
  const mode = process.argv[2] || 'all';
  if (mode === 'dump') {
    const buf = fs.readFileSync(process.argv[3]);
    const r = analyzeWmfBuf(buf, process.argv[3]);
    console.log(JSON.stringify(r, null, 1));
    return;
  }
  const files = listFiles(ROOT, ['.wmf', '.wmz', '.emf']).concat(listFiles(CHM_DIR, ['.wmf', '.wmz', '.emf']));
  const results = [];
  for (const f of files) {
    const ext = path.extname(f).toLowerCase();
    let buf = fs.readFileSync(f);
    let innerOrigin = f;
    if (ext === '.wmz') {
      const z = readWmz(buf);
      if (z.error) {
        results.push({ file: f, parseError: z.error, kind: 'wmz', bytes: buf.length });
        continue;
      }
      const pick = (z.entries || []).find((e) => e.data && /\.wmf$/i.test(e.name)) || (z.entries || []).find((e) => e.data);
      if (!pick || !pick.data) { results.push({ file: f, parseError: 'WMZ 无数据条目', kind: 'wmz', bytes: buf.length }); continue; }
      buf = pick.data;
      innerOrigin = f + ' :: ' + pick.name;
    }
    const r = analyzeWmfBuf(buf, innerOrigin);
    r.file = f;
    results.push(r);
    process.stderr.write('parsed ' + path.relative(ROOT, f) + ' text=' + r.textRecords.length + (r.parseError ? ' ERR=' + r.parseError : '') + '\n');
  }
  // 分类
  for (const r of results) {
    const texts = (r.textRecords || []).map((t) => t.text);
    const cl = classifyFormula(texts, r.geometry || { polyline: 0 }, r.fonts || []);
    r.formula = cl.isFormula;
    r.formulaReasons = cl.reasons;
    r.joinedText = cl.joined;
  }
  fs.mkdirSync(OUT_DIR, { recursive: true });
  fs.writeFileSync(JSON_PATH, JSON.stringify({ generatedAt: new Date().toISOString(), root: ROOT, count: results.length, results }, null, 2), 'utf8');
  console.log('files=', results.length);
  console.log('withText=', results.filter((r) => (r.textRecords || []).length > 0).length);
  console.log('noText=', results.filter((r) => (r.textRecords || []).length === 0 && !r.parseError).length);
  console.log('parseErrors=', results.filter((r) => r.parseError).length);
  console.log('formula=', results.filter((r) => r.formula).length);
  console.log('total text items=', results.reduce((a, r) => a + (r.textRecords || []).length, 0));
  console.log('wrote', JSON_PATH);
}
main();
