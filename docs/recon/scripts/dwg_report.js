'use strict';
/*
 * dwg_report.js  — DWG header/version analysis + printable-ASCII and UTF-16LE text scanning.
 * No third-party libraries are used and nothing is installed.
 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const SRC = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\附件';
const OUT_JSON = 'C:\\code\\Glass\\docs\\recon\\reports\\dwg_report.json';
const OUT_STRINGS = 'C:\\code\\Glass\\docs\\recon\\reports\\dwg_strings.txt';

const VERSION_MAP = {
  AC1006: 'AutoCAD R10',
  AC1009: 'AutoCAD R11/R12? (AC1009 = R11)',
  AC1012: 'AutoCAD R13/R14 family (AC1012 = R13)',
  AC1014: 'AutoCAD R14',
  AC1015: 'AutoCAD 97/98 (AC1015 = R97)',
  AC1018: 'AutoCAD 2000',
  AC1021: 'AutoCAD 2004 (R16)',
  AC1024: 'AutoCAD 2010 (R18)',
  AC1027: 'AutoCAD 2013 (R19, compressed object sections)',
  AC1032: 'AutoCAD 2018+ (R25, compressed object sections)',
};

const targets = fs.readdirSync(SRC).filter((n) => /\.dwg$/i.test(n));
const report = { generatedAt: new Date().toISOString(), files: [] };

function asciiRuns(buf, minLen) {
  const runs = [];
  let cur = '';
  for (let i = 0; i < buf.length; i++) {
    const c = buf[i];
    if (c >= 0x20 && c < 0x7f) cur += String.fromCharCode(c);
    else {
      if (cur.length >= minLen) runs.push(cur);
      cur = '';
    }
  }
  if (cur.length >= minLen) runs.push(cur);
  return runs;
}

// UTF-16LE runs of CJK / full-width characters (how AutoCAD 2007+ stores non-ASCII text)
function utf16leCjkRuns(buf, minLen) {
  const runs = [];
  let cur = '';
  for (let i = 0; i + 1 < buf.length; i += 2) {
    const code = buf[i] | (buf[i + 1] << 8);
    const ok =
      (code >= 0x4e00 && code <= 0x9fff) ||
      (code >= 0x3000 && code <= 0x303f) ||
      (code >= 0xff00 && code <= 0xffef) ||
      (code >= 0x2010 && code <= 0x203b) ||
      (code >= 0x00b0 && code <= 0x00b3) ||
      (code >= 0x0391 && code <= 0x03c9);
    if (ok) cur += String.fromCharCode(code);
    else {
      if (cur.length >= minLen) runs.push(cur);
      cur = '';
    }
  }
  if (cur.length >= minLen) runs.push(cur);
  return runs;
}

// UTF-16LE runs of ASCII digits/letters (AutoCAD unicode strings that are pure ASCII)
function utf16leAsciiRuns(buf, minLen) {
  const runs = [];
  let cur = '';
  for (let i = 0; i + 1 < buf.length; i += 2) {
    const hi = buf[i + 1];
    const lo = buf[i];
    if (hi === 0 && ((lo >= 0x30 && lo <= 0x39) || (lo >= 0x41 && lo <= 0x5a) || (lo >= 0x61 && lo <= 0x7a) || lo === 0x2e || lo === 0x2d || lo === 0x5f || lo === 0x20)) {
      cur += String.fromCharCode(lo);
    } else {
      if (cur.trim().length >= minLen) runs.push(cur.trim());
      cur = '';
    }
  }
  if (cur.trim().length >= minLen) runs.push(cur.trim());
  return runs;
}

function printableRatio(buf) {
  let p = 0;
  for (let i = 0; i < buf.length; i++) if (buf[i] >= 0x20 && buf[i] < 0x7f) p++;
  return p / buf.length;
}

function shannonEntropy(buf) {
  const freq = new Map();
  for (const b of buf) freq.set(b, (freq.get(b) || 0) + 1);
  let h = 0;
  for (const c of freq.values()) {
    const p = c / buf.length;
    h -= p * Math.log2(p);
  }
  return h;
}

for (const name of targets) {
  const full = path.join(SRC, name);
  const buf = fs.readFileSync(full);
  const verRaw = buf.subarray(0, 6).toString('latin1');
  const versionKnown = VERSION_MAP[verRaw] || null;
  const ascii = asciiRuns(buf, 6);
  const ascii4 = asciiRuns(buf, 4);
  const cjk = utf16leCjkRuns(buf, 2);
  const uAscii = utf16leAsciiRuns(buf, 3);

  // header fields (DWG summary info, little endian, from the R13-R2009 layout)
  const dv = buf.readUInt32LE ? null : null;
  let pages = null;
  try {
    // DWG R13..R2007: at 0x14: one 32-bit count(0x14), then per page: 32-bit start offset + 16-bit size
    const pageTableCount = buf.readUInt32LE(0x14);
    const map = [];
    for (let i = 0; i < Math.min(pageTableCount, 16); i++) {
      const off = 0x14 + 4 + i * 6;
      if (off + 6 > buf.length) break;
      map.push({ pageIndex: i, startOffset: buf.readUInt32LE(off), size: buf.readUInt16LE(off + 4) });
    }
    pages = { tableCount: pageTableCount, map };
  } catch (e) {
    pages = { error: e.message };
  }

  const compressedLikely = verRaw === 'AC1027' || verRaw === 'AC1032';
  const entry = {
    file: name,
    path: full,
    sizeBytes: buf.length,
    sha256: crypto.createHash('sha256').update(buf).digest('hex'),
    versionTag: verRaw,
    versionMeaning: versionKnown,
    versionNotes:
      verRaw === 'AC1021'
        ? 'AC1021 对应 AutoCAD 2004/2005/2006 系列图形格式（Drawing Versioning Format 16/17 家族，未使用 R2013+ 的压缩对象段）。'
        : '',
    printableByteRatio: +printableRatio(buf).toFixed(4),
    shannonEntropyBitsPerByte: +shannonEntropy(buf).toFixed(4),
    pageSummary: pages,
    counts: {
      asciiRuns_min6: ascii.length,
      asciiRuns_min4: ascii4.length,
      utf16le_cjk_runs_min2: cjk.length,
      utf16le_ascii_runs_min3: uAscii.length,
    },
    samples: {
      asciiRuns_min6_first40: ascii.slice(0, 40),
      utf16le_cjk_first80: cjk.slice(0, 80),
      utf16le_ascii_first60: uAscii.slice(0, 60),
    },
    verdict: '',
    recommendations: [],
  };

  const hasStructuredText = cjk.length >= 20 || uAscii.length >= 30;
  if (compressedLikely) {
    entry.verdict = 'R2013+ 压缩对象段：不能直接抽取文本（本文件不属于此情况则忽略该条）。';
  } else if (hasStructuredText) {
    entry.verdict =
      'DWG 头部为 ' +
      verRaw +
      '（非 R2013+ 压缩段格式）。文件整体是二进制对象数据（打印字节占比 ' +
      entry.printableByteRatio +
      '，香农熵 ' +
      entry.shannonEntropyBitsPerByte +
      ' bits/byte），不存在可直接搜索的明文文本段；但 AutoCAD 2004(AC1021) 起把非 ASCII 文字以 UTF-16LE 存放在对象流中，因此可从中扫描出 UTF-16LE 的中文/全角片段（本文件扫到 ' +
      cjk.length +
      ' 段，ASCII(UTF-16LE) ' +
      uAscii.length +
      ' 段）。这些片段不是完整的实体表/图纸文字结构：没有实体名(TEXT/MTEXT/ATTRIB)、插入点、图层、样式等结构信息，只可用作内容线索，不能保证完整或顺序正确。';
  } else {
    entry.verdict = '未发现可提取的文本片段。';
  }
  entry.recommendations = [
    '方案A（推荐、无需改动本机）：由用户在 AutoCAD/浩辰CAD/GstarCAD 中打开该 DWG，用“文字检索/导出 DXF”另存为 DXF（ASCII 或 UTF-8 DXF），再对 .dxf 走纯文本抽取；DXF 里 TEXT/MTEXT 实体是明文。',
    '方案B：ODA File Converter（Open Design Alliance，官方免费转换器）把 DWG 批量转 DXF/DWG 2013 后再解析。需要用户自行安装，本次未安装。',
    '方案C：用 ezdxf（Python 库，只读 DXF 而非 DWG）或 LibreDWG dwg2dxf（GNU，可把 R13-R2018 DWG 转 DXF）；都需要额外安装，本次未安装。',
    '方案D：把 DWG 送 REA/逆向通道：DWG 不是 PE/可执行文件，REA 的 open_binary 族工具对它没有价值；可用普通 Node 二进制解析器实现 R2004 的 Section/Record 解码（对象类型码需实现 AutoCAD 的 R2004 对象布局，工作量中等且无第三方库），本次未实现。',
    '注意：本文件不是 R2013+(AC1027/AC1032) 的压缩段格式，因此“对象段被压缩”不是本文件的障碍；障碍是 R2004 的对象/记录分段与对象类型编码需要专用解码器。',
  ];
  report.files.push(entry);

  fs.writeFileSync(
    OUT_STRINGS,
    [
      '# DWG candidate strings from ' + full,
      '# versionTag=' + verRaw + ' size=' + buf.length,
      '',
      '## UTF-16LE CJK / full-width runs (>=2 chars), total ' + cjk.length,
      cjk.map((s, i) => `${i}\t${s}`).join('\n'),
      '',
      '## UTF-16LE ASCII runs (>=3 chars), total ' + uAscii.length,
      uAscii.map((s, i) => `${i}\t${s}`).join('\n'),
      '',
      '## printable-ASCII runs (>=6 chars), total ' + ascii.length,
      ascii.map((s, i) => `${i}\t${s}`).join('\n'),
      '',
    ].join('\n'),
    'utf8',
  );
  fs.mkdirSync(path.dirname(OUT_JSON), { recursive: true });
  fs.writeFileSync(OUT_JSON, JSON.stringify(report, null, 2), 'utf8');
  console.log(name + ' ver=' + verRaw + ' size=' + buf.length + ' cjkRuns=' + cjk.length + ' uAsciiRuns=' + uAscii.length + ' asciiRuns=' + ascii.length + ' entropy=' + entry.shannonEntropyBitsPerByte);
}
console.log('wrote ' + OUT_JSON);
console.log('wrote ' + OUT_STRINGS);
