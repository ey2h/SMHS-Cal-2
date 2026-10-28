// Correct encoding: .CAM as quote-delimited records; 参数库.mdb (Jet) read as UTF-16LE.
const fs = require('fs');
const u16 = new TextDecoder('utf-16le', { fatal: false });
const gbk = new TextDecoder('gbk', { fatal: false });

const cam = process.argv[2];
const mdb = process.argv[3];

// .CAM: prove the record delimiter and dump a couple of rows
const raw = fs.readFileSync(cam);
for (const [encName, txt] of [['ansi/gbk', gbk.decode(raw)], ['utf16le', u16.decode(raw)]]) {
  const seps = { '","': (txt.match(/","/g) || []).length, '",";': 0, '\n': (txt.match(/[\r\n]/g) || []).length, ',': (txt.match(/,/g) || []).length };
  console.log('# ' + cam.split('\\').pop() + ' as ' + encName + '  分隔符计数: ' + JSON.stringify(seps));
}
const t = gbk.decode(raw);
const rows = t.split(/[\r\n]+/).filter((r) => r.trim().length > 2);
console.log('  记录数=' + rows.length);
for (const r of rows.slice(0, 3)) {
  const f = r.replace(/^"|"$/g, '').split('","');
  console.log('  行' + rows.indexOf(r) + ' 字段数=' + f.length + ': ' + f.slice(0, 14).map((x) => x.slice(0, 12)).join(' | '));
}

// .mdb: Jet stores text as UTF-16LE; mine aligned 16-bit runs
const b = fs.readFileSync(mdb);
const T = u16.decode(b);
const RE = /[\u4e00-\u9fa5A-Za-z_][\u4e00-\u9fa5A-Za-z0-9_（）()\.\- ]{2,40}/g;
const found = new Map();
for (const m of T.match(RE) || []) {
  const s = m.trim();
  if (s.length < 3) continue;
  found.set(s, (found.get(s) || 0) + 1);
}
console.log('\n# 参数库.mdb 按 UTF-16LE 对齐挖掘: 去重词条=' + found.size);
const zh = [...found.entries()].filter(([s]) => /[\u4e00-\u9fa5]/.test(s)).sort((a, b) => b[1] - a[1]);
console.log('  中文词条=' + zh.length + '，Top 26:');
for (const [s, c] of zh.slice(0, 26)) console.log('    ' + String(c).padStart(4) + '  ' + s.slice(0, 42));
const en = [...found.entries()].filter(([s]) => /^[A-Za-z_][A-Za-z0-9_ ]{2,}$/.test(s)).sort((a, b) => b[1] - a[1]);
console.log('  英文/字段名 Top 18:');
for (const [s, c] of en.slice(0, 18)) console.log('    ' + String(c).padStart(4) + '  ' + s.slice(0, 42));
for (const key of ['型材', '玻璃', '螺栓', '混凝土', '强度设计值', '6063', 'Q235']) {
  const i = T.indexOf(key);
  if (i < 0) { console.log('  「' + key + '」: 未出现'); continue; }
  console.log('  「' + key + '」@' + (i * 2) + ' 邻近: ' + T.slice(Math.max(0, i - 60), i + 110).replace(/[^\u4e00-\u9fa5A-Za-z0-9\u3001\uff0c()\.\-_,;:%\s]/g, '').replace(/\s+/g, ' ').slice(0, 170));
}
