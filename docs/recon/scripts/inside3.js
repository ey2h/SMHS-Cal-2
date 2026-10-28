// Read the app's own data with the right encoding: .CAM/.inx as text, 参数库.mdb (Jet) strings.
const fs = require('fs');
const gbk = new TextDecoder('gbk', { fatal: false });
const clean = (s) => s.replace(/[^\u4e00-\u9fa5A-Za-z0-9\u3001\u3002\uff08\uff09()\.\-_=,;:%\s]/g, '').replace(/\s+/g, ' ').trim();

for (const f of process.argv.slice(2)) {
  const name = f.split('\\').pop();
  const b = fs.readFileSync(f);
  if (/\.CAM$/i.test(name)) {
    const lines = gbk.decode(b).split(/[\r\n]+/).map(clean).filter((s) => s);
    const asciiish = lines.length && /^[0-9.,;:\-+\sA-Za-z]+$/.test(lines.slice(0, 2).join(''));
    console.log('### ' + name + ' [' + b.length + ' B] 行数=' + lines.length + (asciiish ? '  → 数值文本' : '  → 含文字'));
    for (const l of lines.slice(0, 6)) console.log('    ' + l.slice(0, 118));
    if (lines.length > 6) console.log('    ……末行: ' + lines[lines.length - 1].slice(0, 118));
  } else if (/\.inx$/i.test(name)) {
    const lines = gbk.decode(b).split(/[\r\n]+/).map(clean).filter((s) => s);
    console.log('### ' + name + ' [' + b.length + ' B] 行数=' + lines.length);
    for (const l of lines.slice(0, 10)) console.log('    ' + l.slice(0, 100));
  } else if (/\.mdb$/i.test(name)) {
    const head = gbk.decode(b.subarray(0, 64));
    console.log('### ' + name + ' [' + b.length + ' B] 头 16 字节魔数: ' + b.subarray(0x14, 0x24).toString('latin1').replace(/[^\x20-\x7e]/g, '.') + ' | 前段可读: ' + clean(head).slice(0, 60));
    const text = gbk.decode(b);
    const RE = /[\u4e00-\u9fa5][\u4e00-\u9fa5A-Za-z0-9\u3001\uff08\uff09()\.\-_=,;:%\/]{2,}/g;
    const m = text.match(RE) || [];
    const freq = new Map();
    for (const s of m) freq.set(s, (freq.get(s) || 0) + 1);
    console.log('  GBK 词条总数=' + m.length + '，去重=' + freq.size);
    console.log('  高频词条 Top 22:');
    for (const [s, c] of [...freq.entries()].sort((a, b) => b[1] - a[1]).slice(0, 22)) console.log('    ' + String(c).padStart(4) + '  ' + s.slice(0, 46));
    for (const key of ['型材', '混凝土', 'Q235', '6063', 'MPa', '惯性矩']) {
      const i = text.indexOf(key);
      if (i < 0) { console.log('  「' + key + '」: 未出现'); continue; }
      const ctx = clean(text.slice(Math.max(0, i - 90), i + 150));
      console.log('  「' + key + '」上下文: ' + ctx.slice(0, 190));
    }
  }
}
