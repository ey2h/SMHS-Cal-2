// Show raw context around v:imagedata occurrences for a given page.
const fs = require('fs');
const path = require('path');
const HTML_DIR = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\html';
const page = process.argv[2] || 'B21.htm';
const t = new TextDecoder('gb18030', { fatal: false }).decode(fs.readFileSync(path.join(HTML_DIR, page)));
const re = /imagedata/gi;
let m, n = 0;
while ((m = re.exec(t))) {
  n++;
  const a = Math.max(0, m.index - 700), b = Math.min(t.length, m.index + 400);
  console.log('=== occurrence ' + n + ' at ' + m.index + ' ===');
  console.log(t.slice(a, b).replace(/\r/g, ''));
  console.log();
}
console.log('total', n);
