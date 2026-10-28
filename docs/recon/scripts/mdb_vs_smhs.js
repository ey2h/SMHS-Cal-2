// Compare Huibao's parameter library (59 tables) against the project's SMHS SQLite databases.
const fs = require('fs');
const path = require('path');
const { DatabaseSync } = require('node:sqlite');

const HB = 'D:/Code/333/hb_mdb/_index.json';
const DATA_DIR = 'D:/Code/Glass/src/GlassManagement.Api/Data';
const OUT = 'D:/Code/333/recon-out';
fs.mkdirSync(OUT, { recursive: true });

const hb = JSON.parse(fs.readFileSync(HB, 'utf8')).filter((t) => t && t.table);
console.log('汇宝表数=' + hb.length);

// --- inventory the project's SQLite databases
const dbs = fs.readdirSync(DATA_DIR).filter((f) => f.toLowerCase().endsWith('.db'));
const mine = [];
for (const f of dbs) {
  let db;
  try { db = new DatabaseSync(path.join(DATA_DIR, f), { readOnly: true }); } catch (e) { console.log('跳过 ' + f + ': ' + e.message); continue; }
  const ts = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").all();
  for (const t of ts) {
    let cols = [], rows = 0;
    try { cols = db.prepare(`PRAGMA table_info([${t.name}])`).all().map((c) => c.name); } catch { }
    try { rows = db.prepare(`SELECT COUNT(*) c FROM [${t.name}]`).get().c; } catch { }
    mine.push({ db: f, table: t.name, columns: cols, rows });
  }
  db.close();
}
console.log('本项目表数=' + mine.length + '  库=' + dbs.join(','));

const norm = (s) => String(s).replace(/[\s（）()·，,、．.]/g, '').toLowerCase();
const overlap = (a, b) => {
  const A = new Set(a.map(norm)); const B = new Set(b.map(norm));
  if (!A.size || !B.size) return 0;
  let hit = 0;
  for (const x of A) if (B.has(x)) hit++;
  return hit / Math.max(A.size, B.size);
};

// --- match each Huibao table to the best project table
const rows = [];
for (const h of hb) {
  let best = null;
  for (const m of mine) {
    const sc = overlap(h.columns, m.columns);
    const nameBonus = norm(m.table).includes(norm(h.table).slice(0, 3)) ? 0.15 : 0;
    const s = sc + nameBonus;
    if (!best || s > best.score) best = { score: s, m };
  }
  rows.push({ h, best });
}

const covered = rows.filter((r) => r.best && r.best.score >= 0.6);
const partial = rows.filter((r) => r.best && r.best.score >= 0.25 && r.best.score < 0.6);
const missing = rows.filter((r) => !r.best || r.best.score < 0.25);

const lines = [];
lines.push('# 汇宝参数库(59表) ↔ SMHS 数据库 对表结果\n');
lines.push(`- 本项目库：${dbs.join(', ')}`);
lines.push(`- 本项目表数：${mine.length}`);
lines.push(`- 判定：完全覆盖(列重叠≥60%) **${covered.length}** ／ 部分覆盖(25–60%) **${partial.length}** ／ 缺失(<25%) **${missing.length}**\n`);

lines.push('## 缺失（汇宝有、你没有或几乎不对应）\n');
lines.push('| 汇宝表 | 行数 | 关键列 | 最佳猜测 | 列重叠 |');
lines.push('|---|---|---|---|---|');
for (const r of missing.sort((a, b) => b.h.rows - a.h.rows)) {
  lines.push(`| ${r.h.table} | ${r.h.rows} | ${r.h.columns.slice(0, 6).join('、')} | ${(r.best && r.best.m.db + '/' + r.best.m.table) || '无'} | ${((r.best && r.best.score) || 0).toFixed(2)} |`);
}
lines.push('\n## 部分覆盖（列不一致，缺哪几列）\n');
lines.push('| 汇宝表 | 行数 | 汇宝列 | 对应表 | 你缺的列 |');
lines.push('|---|---|---|---|---|');
for (const r of partial.sort((a, b) => b.h.rows - a.h.rows)) {
  const hc = new Set(r.h.columns.map(norm));
  const mc = new Set(r.best.m.columns.map(norm));
  const lack = r.h.columns.filter((c) => !mc.has(norm(c))).slice(0, 10);
  lines.push(`| ${r.h.table} | ${r.h.rows} | ${r.h.columns.slice(0, 8).join('、')} | ${r.best.m.db}/${r.best.m.table} (${r.best.m.rows}行) | ${lack.join('、') || '—'} |`);
}
lines.push('\n## 已覆盖\n');
lines.push('| 汇宝表 | 汇宝行 | 对应表 | 你的行 | 列重叠 |');
lines.push('|---|---|---|---|---|');
for (const r of covered.sort((a, b) => b.h.rows - a.h.rows)) {
  lines.push(`| ${r.h.table} | ${r.h.rows} | ${r.best.m.db}/${r.best.m.table} | ${r.best.m.rows} | ${r.best.score.toFixed(2)} |`);
}
lines.push('\n## 你独有（汇宝没有对应）\n');
const used = new Set(rows.filter((r) => r.best && r.best.score >= 0.25).map((r) => r.best.m.db + '/' + r.best.m.table));
for (const m of mine) if (!used.has(m.db + '/' + m.table)) lines.push(`- ${m.db} / ${m.table} (${m.rows} 行, ${m.columns.length} 列)`);

const report = lines.join('\n');
fs.writeFileSync(path.join(OUT, 'mdb_vs_smhs.md'), report, 'utf8');
console.log('\n缺失=' + missing.length + ' 部分=' + partial.length + ' 覆盖=' + covered.length + ' 你独有=' + (mine.length - used.size));
console.log('报告: ' + path.join(OUT, 'mdb_vs_smhs.md'));
console.log('\n=== 缺失表清单（按行数）');
for (const r of missing.sort((a, b) => b.h.rows - a.h.rows).slice(0, 26)) console.log('  ' + r.h.table.padEnd(22) + ' 行=' + String(r.h.rows).padStart(5) + '  ' + r.h.columns.slice(0, 5).join(','));
