// Dump selected GBK/GB18030 .htm files to UTF-8 text for inspection (no HTML parsing).
const fs = require('fs');
const path = require('path');

const SRC = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\html';
const OUT = 'C:\\code\\Glass\\docs\\recon\\help\\_inspect';

const targets = process.argv.slice(2);
fs.mkdirSync(OUT, { recursive: true });
for (const t of targets) {
  const buf = fs.readFileSync(path.join(SRC, t));
  let text;
  try {
    text = new TextDecoder('gb18030', { fatal: false }).decode(buf);
  } catch (e) {
    text = new TextDecoder('windows-1252').decode(buf);
  }
  const outPath = path.join(OUT, t.replace(/[\\/]/g, '__') + '.utf8.txt');
  fs.writeFileSync(outPath, text, 'utf8');
  console.log(t, '->', outPath, 'bytes=' + buf.length, 'chars=' + [...text].length);
}
