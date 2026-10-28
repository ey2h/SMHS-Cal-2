'use strict';
/*
 * spec_txt2md.js
 * Decode 规范\规范1..8.txt (GBK/GB18030) to UTF-8 Markdown, preserving original text verbatim.
 * Also emits a machine-readable index of headings/standard-codes per file.
 * Read-only on source.
 */
const fs = require('fs');
const path = require('path');

const SRC = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\规范';
const OUT_DIR = 'C:\\code\\Glass\\docs\\recon\\specs';
const OUT_INDEX = 'C:\\code\\Glass\\docs\\recon\\reports\\specs_txt_index.json';

fs.mkdirSync(OUT_DIR, { recursive: true });

function decodeGb(buf) {
  return new TextDecoder('gb18030').decode(buf);
}

function looksLikeHeading(line) {
  const t = line.trim();
  if (!t) return false;
  if (t.length > 90) return false;
  // 《标准名》+ code, 第X章/条, numbered sections, 附录
  if (/^《.+》/.test(t)) return true;
  if (/^第[0-9一二三四五六七八九十百]+[章条款节]/.test(t)) return true;
  if (/^[0-9]+(\.[0-9]+)*\s+\S/.test(t)) return true;
  if (/^(附录|Appendix)/i.test(t)) return true;
  return false;
}

const results = [];
const files = fs
  .readdirSync(SRC)
  .filter((n) => /^规范\d+\.txt$/i.test(n))
  .sort((a, b) => a.localeCompare(b, 'zh-Hans-CN'));

for (const name of files) {
  const srcPath = path.join(SRC, name);
  const buf = fs.readFileSync(srcPath);
  const text = decodeGb(buf);
  const normalized = text.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  const lines = normalized.split('\n');

  const nonEmpty = lines.map((l) => l.trim()).filter((l) => l.length);
  const headings = [];
  lines.forEach((l, i) => {
    if (looksLikeHeading(l)) headings.push({ line: i + 1, text: l.trim() });
  });

  // detect standard codes anywhere in the text
  const codes = new Set();
  const codeRe = /(GB\/T|GB|JGJ|JG\/T|JG|CJJ|DB\d{2}|GB\d{5}|YS|CJ)\s?\/?\s?[A-Z]?\d{2,5}(?:[-–]\d{4})?/g;
  let m;
  while ((m = codeRe.exec(text))) codes.add(m[0].replace(/\s+/g, ''));

  // crude "key points": lines that mention 公式/系数/限值/不应/应不少于 etc.
  const ruleRe = /(不应|不宜|不得|应不少于|应不大于|限值|系数|公式|计算|取值|等级|不低于)/;
  const ruleLines = [];
  nonEmpty.forEach((l, i) => {
    if (ruleRe.test(l) && l.length > 6) ruleLines.push({ approxLine: i + 1, text: l });
  });

  const base = path.basename(name, '.txt');
  const outPath = path.join(OUT_DIR, base + '.md');
  const header = [
    '# ' + base + '（源：规范\\' + name + '）',
    '',
    '- 源文件：' + srcPath,
    '- 源字节数：' + buf.length,
    '- 源编码（判定）：GBK/GB18030 单字节锚定，无 BOM；输出 UTF-8',
    '- 行数：' + lines.length + '（非空 ' + nonEmpty.length + '）',
    '- 说明：以下为源文件原文逐行保留（未翻译、未改写）。',
    '',
    '---',
    '',
  ].join('\n');
  fs.writeFileSync(outPath, header + normalized + '\n', 'utf8');

  results.push({
    sourceFile: name,
    sourcePath: srcPath,
    bytes: buf.length,
    encodingGuess: 'gb18030 (source appears to be GBK/ANSI, no BOM)',
    outputMd: outPath,
    lineCount: lines.length,
    nonEmptyLines: nonEmpty.length,
    charCount: normalized.length,
    firstLines: nonEmpty.slice(0, 6),
    headings,
    standardCodes: Array.from(codes),
    ruleLikeLines: ruleLines.slice(0, 40),
    ruleLikeLineCount: ruleLines.length,
  });
  console.log(name + ' -> ' + outPath + ' lines=' + lines.length + ' headings=' + headings.length + ' codes=' + results.at(-1).standardCodes.length);
}

fs.mkdirSync(path.dirname(OUT_INDEX), { recursive: true });
fs.writeFileSync(OUT_INDEX, JSON.stringify({ generatedAt: new Date().toISOString(), files: results }, null, 2), 'utf8');
console.log('wrote ' + OUT_INDEX);
