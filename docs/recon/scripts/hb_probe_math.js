// Probe how formulas are represented: OMML (m:oMath), sup/sub, symbol fonts, <w:p> etc.
const fs = require('fs');
const path = require('path');
const HTML_DIR = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\html';
for (const name of fs.readdirSync(HTML_DIR).filter(n => /\.htm$/i.test(n))) {
  const t = new TextDecoder('gb18030', { fatal: false }).decode(fs.readFileSync(path.join(HTML_DIR, name)));
  const c = {
    oMath: (t.match(/m:oMath/g) || []).length,
    sup: (t.match(/<sup[\s>]/gi) || []).length,
    sub: (t.match(/<sub[\s>]/gi) || []).length,
    table: (t.match(/<table[\s>]/gi) || []).length,
    fontTimes: (t.match(/Times New Roman/gi) || []).length,
    symbolFont: (t.match(/Symbol/gi) || []).length,
    eqArray: (t.match(/m:eqArr/g) || []).length,
    commentVml: (t.match(/if !vml/gi) || []).length,
    oembed: (t.match(/<o:p>/gi) || []).length,
  };
  const greek = (t.match(/[α-ωΑ-Ω∑∏√±≤≥×÷∞≈∫]/g) || []).length;
  console.log(name.padEnd(9), JSON.stringify(c), 'mathchars=' + greek);
}
