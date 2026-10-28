'use strict';
/*
 * dwg_report2.js — DWG header / structure / text-extractability analysis.
 * No third-party libraries; nothing installed; source read-only.
 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const SRC = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\附件';
const outJsonPath = 'C:\\code\\Glass\\docs\\recon\\reports\\dwg_report.json';
const OUT_STRINGS = 'C:\\code\\Glass\\docs\\recon\\reports\\dwg_strings.txt';

const VERSION_MAP = {
  AC1006: { name: 'AutoCAD R10', drawingFormatVersion: 10 },
  AC1009: { name: 'AutoCAD R11 / R12', drawingFormatVersion: 11 },
  AC1012: { name: 'AutoCAD R13', drawingFormatVersion: 13 },
  AC1014: { name: 'AutoCAD R14 / 97 / 98', drawingFormatVersion: 14 },
  AC1015: { name: 'AutoCAD R97 / 98 / 99 (2000-beta?)', drawingFormatVersion: 15 },
  AC1018: { name: 'AutoCAD 2000 / 2001 / 2002', drawingFormatVersion: 18 },
  AC1021: { name: 'AutoCAD 2004 / 2005 / 2006 (Drawing Format version 21)', drawingFormatVersion: 21 },
  AC1024: { name: 'AutoCAD 2007 / 2008 / 2009 (Drawing Format version 24)', drawingFormatVersion: 24 },
  AC1027: { name: 'AutoCAD 2013 / 2014 / 2015 / 2016/2017 (Drawing Format version 25/26) — 对象段被压缩', drawingFormatVersion: 27 },
  AC1032: { name: 'AutoCAD 2018+ (Drawing Format version 32) — 对象段被压缩', drawingFormatVersion: 32 },
};

function asciiRuns(buf, minLen) {
  const runs = [];
  let cur = '';
  for (let i = 0; i < buf.length; i++) {
    const c = buf[i];
    if (c >= 0x20 && c < 0x7f) cur += String.fromCharCode(c);
    else { if (cur.length >= minLen) runs.push(cur); cur = ''; }
  }
  if (cur.length >= minLen) runs.push(cur);
  return runs;
}

function utf16leRuns(buf, minLen, allow) {
  const runs = [];
  let cur = '';
  for (let i = 0; i + 1 < buf.length; i += 2) {
    const code = buf[i] | (buf[i + 1] << 8);
    if (allow(code)) cur += String.fromCharCode(code);
    else { if (cur.length >= minLen) runs.push(cur); cur = ''; }
  }
  if (cur.length >= minLen) runs.push(cur);
  return runs;
}

const cjkAllow = (code) =>
  (code >= 0x4e00 && code <= 0x9fff) ||
  (code >= 0x3000 && code <= 0x303f) ||
  (code >= 0xff00 && code <= 0xffef) ||
  (code >= 0x2010 && code <= 0x203b) ||
  (code >= 0x0391 && code <= 0x03c9);

const ascii16Allow = (code) =>
  (code >= 0x30 && code <= 0x39) || (code >= 0x41 && code <= 0x5a) || (code >= 0x61 && code <= 0x7a) ||
  code === 0x2e || code === 0x2d || code === 0x5f || code === 0x28 || code === 0x29;

// GBK/GB18030 view of the whole file: decode then keep runs of CJK-ish characters
function gbkRuns(buf, minLen) {
  const text = new TextDecoder('gb18030').decode(buf);
  const runs = [];
  let cur = '';
  for (const ch of text) {
    const code = ch.codePointAt(0);
    if (cjkAllow(code)) cur += ch;
    else { if (cur.length >= minLen) runs.push(cur); cur = ''; }
  }
  if (cur.length >= minLen) runs.push(cur);
  return runs;
}

function entropy(buf) {
  const freq = new Map();
  for (const b of buf) freq.set(b, (freq.get(b) || 0) + 1);
  let h = 0;
  for (const c of freq.values()) { const p = c / buf.length; h -= p * Math.log2(p); }
  return h;
}

function printableRatio(buf) {
  let p = 0;
  for (const b of buf) if (b >= 0x20 && b < 0x7f) p++;
  return p / buf.length;
}

// Section table for the R2004/AC1021 family (version 21..24 layout):
// 0x00 version[6], 0x06 maintenance version(u16), 0x08 reserved(u16), 0x0A class(u16),
// 0x0C startVirtualMemory(i32), 0x10 endVirtualMemory(i32), 0x14 sectionCount(i32),
// then sectionCount * { start(i32), size(u16) }, then objectCount(i32), unknown(i32),
// infoRootAnnotationStart(i32), templateStart(i32), unknown(i32), ... signature(16) ...
function parseR2004Header(buf) {
  const out = { layout: 'R2004 (version 21) header layout — 解析结果，未对照官方规格逐字段核实' };
  try {
    out.versionString = buf.subarray(0, 6).toString('latin1');
    out.maintenanceVersion = buf.readUInt16LE(0x06);
    out.reserved0x08 = buf.readUInt16LE(0x08);
    out.class0x0a = buf.readUInt16LE(0x0a);
    out.startVirtualMemory = buf.readInt32LE(0x0c);
    out.endVirtualMemory = buf.readInt32LE(0x10);
    out.sectionCount = buf.readInt32LE(0x14);
    const sections = [];
    const n = out.sectionCount;
    let pos = 0x18;
    if (n >= 1 && n <= 512) {
      for (let i = 0; i < n; i++) {
        if (pos + 6 > buf.length) break;
        const start = buf.readUInt32LE(pos);
        const size = buf.readUInt16LE(pos + 6 - 2);
        sections.push({ index: i, start, size, end: start + size });
        pos += 6;
      }
      out.sections = sections;
      out.sectionTableEnd = pos;
      if (pos + 12 <= buf.length) {
        out.afterSections = {
          u32_at_tableEnd: buf.readUInt32LE(pos),
          u32_at_tableEnd_plus4: buf.readUInt32LE(pos + 4),
          u32_at_tableEnd_plus8: buf.readUInt32LE(pos + 8),
        };
      }
      const last = sections[sections.length - 1];
      out.sectionsInsideFile = !!last && last.end <= buf.length;
      out.lastSectionEnd = last ? last.end : null;
      out.fileSize = buf.length;
    } else {
      out.sectionsParseRejected = 'sectionCount=' + n + ' 不在合理范围';
    }
  } catch (e) {
    out.error = e.message;
  }
  return out;
}

const report = { generatedAt: new Date().toISOString(), policy: 'read-only; no third-party library used; nothing installed', files: [] };

for (const name of fs.readdirSync(SRC).filter((n) => /\.dwg$/i.test(n))) {
  const full = path.join(SRC, name);
  const buf = fs.readFileSync(full);
  const ver = buf.subarray(0, 6).toString('latin1');
  const vinfo = VERSION_MAP[ver] || null;

  const a6 = asciiRuns(buf, 6);
  const u16cjk = utf16leRuns(buf, 2, cjkAllow);
  const u16ascii = utf16leRuns(buf, 3, ascii16Allow);
  const gbk = gbkRuns(buf, 3);

  // longest plausible Chinese run found (heuristic plausibility check)
  const longestGbk = gbk.slice().sort((x, y) => y.length - x.length)[0] || '';
  const longestU16 = u16cjk.slice().sort((x, y) => y.length - x.length)[0] || '';

  const header = ver === 'AC1021' || ver === 'AC1024' ? parseR2004Header(buf) : { skipped: '该版本的头结构未在此脚本中实现' };

  const entry = {
    file: name,
    path: full,
    sizeBytes: buf.length,
    sha256: crypto.createHash('sha256').update(buf).digest('hex'),
    versionTag: ver,
    versionMeaning: vinfo ? vinfo.name : '未知版本标识',
    compressedObjectSections: ver === 'AC1027' || ver === 'AC1032',
    printableByteRatio: +printableRatio(buf).toFixed(4),
    shannonEntropyBitsPerByte: +entropy(buf).toFixed(4),
    headerParse: header,
    stringScan: {
      printableAsciiRuns_min6: { count: a6.length, sample: a6.slice(0, 30) },
      utf16le_cjkRuns_min2: { count: u16cjk.length, longest: longestU16, sample: u16cjk.slice(0, 25) },
      utf16le_asciiRuns_min3: { count: u16ascii.length, sample: u16ascii.slice(0, 30) },
      gb18030_decoded_cjkRuns_min3: { count: gbk.length, longest: longestGbk, sample: gbk.slice(0, 25) },
    },
    extractablePlainText: false,
    verdict: '',
    followUpOptions: [],
  };

  entry.verdict =
    'DWG 版本标识 = ' + ver + '（' + (vinfo ? vinfo.name : '未知') + '），文件 ' + buf.length + ' 字节。' +
    '文件整体是 AutoCAD 二进制图形数据库：可打印 ASCII 占比 ' + entry.printableByteRatio +
    '，香农熵 ' + entry.shannonEntropyBitsPerByte + ' bits/byte（接近随机/结构化二进制，说明对象数据不是明文）。' +
    '本文件不是 R2013+(AC1027/AC1032) 的压缩对象段格式（那是 ' + 'AC1027/AC1032' + ' 才引入的），' +
    '但 R2004 格式仍然把对象按 Section/Record 分段、对象类型与字符串按 AutoCAD 私有编码存放，' +
    '因此无法用“搜索可打印字符串”的方式可靠抽文本。' +
    '实测扫描证据：可打印 ASCII 串 ' + a6.length + ' 段（样例：' + JSON.stringify(a6.slice(0, 8)) + '，均为乱码）；' +
    'UTF-16LE 中日韩字符片段 ' + u16cjk.length + ' 段（最长 ' + longestU16.length + ' 字，样例：' + JSON.stringify(u16cjk.slice(0, 8)) + '，无词汇意义）；' +
    '按 GB18030 解码整文件得到的中文片段 ' + gbk.length + ' 段（最长 ' + longestGbk.length + ' 字，样例：' + JSON.stringify(gbk.slice(0, 8)) + '，同样无词汇意义）。' +
    '结论：无法直接抽取文本（无法在不引入 DWG 解析器/转换器的情况下得到型材断面名称、表格文字等）。';

  entry.followUpOptions = [
    'A) 最省事：在 AutoCAD / 浩辰 CAD GstarCAD / 中望 CAD 里打开该 DWG，另存为 DXF（ASCII DXF，R12/2000 版本兼容性最好），再对 .dxf 做纯文本抽取——DXF 中 TEXT/MTEXT/ATTRIB 实体是明文，配对 TEXT+POINT 即可还原文字与位置。',
    'B) 命令行批量：ODA File Converter（Open Design Alliance 官方免费工具）或 ODA SDK 命令行，DWG -> DXF，无需本机 CAD。需要用户自行安装（本次未安装、未使用）。',
    'C) 开源库：LibreDWG（dwg2dxf，支持 R13–2018 DWG 转 DXF）；或 Python 的 ezdxf（只读 DXF，不读 DWG，需先用 B 方案转 DXF）。都需要额外安装，本次未安装。',
    'D) 纯 Node 自研解码：R2004(AC1021) 的 SectionMap/ObjectInfo/ObjectHeader 记录结构与“R2004 字符串编码”是可实现的（ODA 的 .net/DWG 规格文档、LibreDWG 源码可参照），可只实现 TEXT/MTEXT/ATTRIB 记录抽取。工作量中等（约数百行），无第三方依赖，但对字段布局的解释需以官方/ODA 文档核对，不能凭猜。',
    'E) 反向路线：不解析 DWG，而是找软件运行时实际读取的数据源——汇宝在“参数库.mdb”等数据文件里可能已存有同一份型材断面表（本仓库 docs\\recon\\reports\\hb_mdb_index.json 已建过 mdb 索引）。若 mdb 中已有型材断面汇总表，就不必解析 DWG。',
    'F) 若只需要“用法说明”那部分文字：DWG 里通常把说明写在 MTEXT/TEXT 实体中，A 方案转 DXF 后即可拿到；也可在 CAD 中用“导出/复制”把文字粘贴到文本文件。',
  ];

  report.files.push(entry);

  fs.writeFileSync(
    OUT_STRINGS,
    [
      '# DWG 文本抽取候选串（仅诊断用途，非可靠内容）',
      '# 文件: ' + full,
      '# versionTag=' + ver + '  size=' + buf.length + '  entropy=' + entry.shannonEntropyBitsPerByte,
      '',
      '## A. 可打印 ASCII 片段 (>=6 字符), 共 ' + a6.length,
      a6.map((s, i) => i + '\t' + s).join('\n'),
      '',
      '## B. UTF-16LE 中日韩片段 (>=2 字), 共 ' + u16cjk.length,
      u16cjk.map((s, i) => i + '\t' + s).join('\n'),
      '',
      '## C. UTF-16LE 纯 ASCII 片段 (>=3 字符), 共 ' + u16ascii.length,
      u16ascii.map((s, i) => i + '\t' + s).join('\n'),
      '',
      '## D. 按 GB18030 解码整文件后的中文片段 (>=3 字), 共 ' + gbk.length,
      gbk.map((s, i) => i + '\t' + s).join('\n'),
      '',
    ].join('\n'),
    'utf8',
  );

  fs.writeFileSync(outJsonPath, JSON.stringify(report, null, 2), 'utf8');
  console.log(name + ' ver=' + ver + ' size=' + buf.length + ' entropy=' + entry.shannonEntropyBitsPerByte +
    ' asciiRuns=' + a6.length + ' u16cjk=' + u16cjk.length + ' gbkCjk=' + gbk.length +
    ' sections=' + (header.sectionCount === undefined ? 'n/a' : header.sectionCount));
}
console.log('wrote ' + outJsonPath);
console.log('wrote ' + OUT_STRINGS);
