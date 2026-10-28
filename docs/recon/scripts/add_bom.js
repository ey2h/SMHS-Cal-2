'use strict';
/*
 * add_bom.js <file...>
 * Ensures the given text files are UTF-8 **with BOM** so Windows PowerShell 5.1
 * does not misread them as ANSI (which garbles Chinese literals in .ps1 files).
 */
const fs = require('fs');
for (const p of process.argv.slice(2)) {
  const buf = fs.readFileSync(p);
  if (buf.length >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) {
    console.log('already BOM: ' + p);
    continue;
  }
  const text = buf.toString('utf8');
  fs.writeFileSync(p, '\ufeff' + text, 'utf8');
  console.log('BOM added: ' + p);
}
