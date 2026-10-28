// Dump the project's SQLite schemas and sample rows so column names can be mapped semantically.
const fs = require('fs');
const path = require('path');
const { DatabaseSync } = require('node:sqlite');

const D = 'D:/Code/Glass/src/GlassManagement.Api/Data';
for (const f of fs.readdirSync(D).filter((x) => x.toLowerCase().endsWith('.db'))) {
  let db;
  try { db = new DatabaseSync(path.join(D, f), { readOnly: true }); } catch (e) { console.log(f + ': ' + e.message); continue; }
  console.log('\n### ' + f);
  const ts = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").all();
  for (const t of ts) {
    const cols = db.prepare(`PRAGMA table_info([${t.name}])`).all().map((c) => c.name + ':' + c.type);
    let rows = 0; let sample = null;
    try { rows = db.prepare(`SELECT COUNT(*) c FROM [${t.name}]`).get().c; } catch { }
    try { sample = db.prepare(`SELECT * FROM [${t.name}] LIMIT 2`).all(); } catch { }
    console.log('  ' + t.name + '  rows=' + rows);
    console.log('    cols: ' + cols.join(', '));
    if (sample && sample.length) console.log('    样本1: ' + JSON.stringify(sample[0]).slice(0, 300));
  }
  db.close();
}
