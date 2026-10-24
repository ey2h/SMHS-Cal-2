// Build a verbatim SQLite copy of Huibao's 59-table parameter library + write the column-level gap report.
const fs = require('fs');
const path = require('path');
const { DatabaseSync } = require('node:sqlite');

const SRC = 'D:/Code/333/hb_mdb';
const OUT = 'D:/Code/333/recon-out';
fs.mkdirSync(OUT, { recursive: true });
const DBPATH = path.join(OUT, 'hb_library.sqlite');
if (fs.existsSync(DBPATH)) fs.unlinkSync(DBPATH);

const index = JSON.parse(fs.readFileSync(path.join(SRC, '_index.json'), 'utf8')).filter((t) => t && t.table);

// RFC4180-ish CSV parser (Export-Csv quotes fields that contain commas/quotes/newlines).
function parseCsv(text) {
  if (text.charCodeAt(0) === 0xfeff) text = text.slice(1);
  const rows = [];
  let field = '';
  let row = [];
  let inQ = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (inQ) {
      if (ch === '"') {
        if (text[i + 1] === '"') { field += '"'; i++; } else inQ = false;
      } else field += ch;
      continue;
    }
    if (ch === '"') { inQ = true; continue; }
    if (ch === ',') { row.push(field); field = ''; continue; }
    if (ch === '\r') continue;
    if (ch === '\n') { row.push(field); rows.push(row); row = []; field = ''; continue; }
    field += ch;
  }
  if (field.length || row.length) { row.push(field); rows.push(row); }
  if (!rows.length) return [];
  const head = rows[0];
  return rows.slice(1).filter((r) => r.length > 1 || (r[0] !== '' )).map((r) => {
    const o = {};
    head.forEach((h, i) => { o[h.replace(/"/g, '')] = r[i] === undefined ? '' : r[i]; });
    return o;
  });
}

const db = new DatabaseSync(DBPATH);
let totalRows = 0;
for (const t of index) {
  const cols = t.columns.map((c) => `"${String(c).replace(/"/g, '')}" TEXT`);
  db.exec(`CREATE TABLE [${t.table}] (${cols.join(', ')})`);
  const csvPath = path.join(SRC, t.table + '.csv');
  if (!fs.existsSync(csvPath)) { console.log('缺 CSV: ' + t.table); continue; }
  const rows = parseCsv(fs.readFileSync(csvPath, 'utf8'));
  const names = rows.length ? Object.keys(rows[0]) : t.columns;
  const order = t.columns.map((c) => names.find((n) => n === c) ?? c);
  const stmt = db.prepare(`INSERT INTO [${t.table}] (${t.columns.map((c) => `"${c}"`).join(', ')}) VALUES (${t.columns.map(() => '?').join(', ')})`);
  db.exec('BEGIN');
  for (const r of rows) for (const c of order) stmt.run(r[c] === undefined ? null : String(r[c]));
  db.exec('COMMIT');
  totalRows += rows.length;
}
db.exec('VACUUM');

// --- column-level mapping (curated by hand from both real schemas)
const MAP = [
  ['地区参数', 'SMHS_Region.db/Regions', ['地区名称|Name', '50年基本风压|WindPressure50', '100年基本风压|WindPressure100', '50年基本雪压|SnowPressure50', '100年基本雪压|SnowPressure100', 'αmax|AlphaMax', '基本地震动峰值加速度|PeakAcceleration', '极限温差|TemperatureDiff', '抗震烈度|SeismicIntensity']],
  ['立柱', 'SMHS_Profile.db/ProfileColumns', ['型材代号|ProfileCode', '截面积|SectionArea', 'Ix惯性矩|MomentOfInertiaX', 'Iy惯性矩|MomentOfInertiaY', 'Wx1抵抗矩|SectionModulusX', '备注|Remark']],
  ['横框', 'SMHS_Profile.db/ProfileBeams', ['型材代号|ProfileCode', '截面积|SectionArea', 'Ix惯性矩|MomentOfInertiaX', 'Iy惯性矩|MomentOfInertiaY', 'Wx1抵抗矩|SectionModulusX', 'Wy1抵抗矩|SectionModulusY', '备注|Remark']],
  ['螺栓1', 'SMHS_Bolt.db/Bolts', ['型号|Model', '材质|Material', '公称直径|Diameter', '有效直径|EffectiveDiameter', '拉应力|TensionStress', '剪应力|ShearStress', '备注|Remark']],
  ['后锚固材料参数', 'SMHS_Material.db/AnchorMaterials', ['材料|Material', '代号|Model', '螺杆直径d|Diameter', '有效截面积As|EffectiveArea', 'fyk，JGJ145规范用|Fyk', 'fstk，JGJ145规范用|Fstk', 'fud,t，GB50367规范用|FudT', 'fud,v，GB50367规范用|FudV', '备注|Remark']],
  ['幕墙自重参数', 'SMHS_Material.db/CurtainWallWeights', ['幕墙类别|Category', '立柱及主体自重|ColumnWeight', '计算横梁时自重|BeamWeight', '软件初始默认值适用范围|ApplicationRange', '序号|Id']],
];

const mine = { columns: new Map() };
for (const [hb, target, pairs] of MAP) {
  const [dbf, tbl] = target.split('/');
  if (!mine.columns.has(target)) {
    const d = new DatabaseSync(path.join('D:/Code/Glass/src/GlassManagement.Api/Data', dbf), { readOnly: true });
    mine.columns.set(target, d.prepare(`PRAGMA table_info([${tbl}])`).all().map((c) => c.name));
    d.close();
  }
}

const lines = ['# 汇宝参数库 ↔ SMHS 数据库：列级对照与缺口\n', `汇宝库：59 表 / ${totalRows} 行 → 已原样落盘 [hb_library.sqlite](${DBPATH.replace(/\\/g, '/')})（未改动你现有库）\n`];
lines.push('## 1) 已 1:1 对应的表（含被丢弃的列）\n');
lines.push('| 汇宝表 | 你的表 | 汇宝列数 | 已映射 | 你缺的列 |');
lines.push('|---|---|---|---|---|');
for (const [hb, target, pairs] of MAP) {
  const h = index.find((x) => x.table === hb);
  const mappedHb = new Set(pairs.map((p) => p.split('|')[0]));
  const lack = h.columns.filter((c) => !mappedHb.has(c));
  lines.push(`| ${hb} (${h.rows}行) | ${target} | ${h.columns.length} | ${pairs.length} | ${lack.join('、') || '—'} |`);
}

lines.push('\n## 2) 汇宝有、你完全没有对应表的库（按用途分组）\n');
const covered = new Set(MAP.map((m) => m[0]));
const rest = index.filter((t) => !covered.has(t.table));
const groups = {
  单元幕墙: /^单元|转角立柱/, 采光顶: /^采光顶/, 吊顶: /^吊顶/, 雨篷: /^雨篷|雨蓬/, 点式: /^钢管点玻/,
  门窗: /^立梃|^横梃/, 栏杆: /^栏杆/, 钢铝结合: /^钢铝结合/, 面板材料: /^(铝单板|复合搪瓷板|单体搪瓷板|钢板|不锈钢板|钛合金板|铜板|铝塑复合板|蜂窝铝板|铝镁硅合金板|百叶叶片|U型玻璃)$/,
  热工节能: /^热工/, 连续梁查表: /^多跨/, 下料优化: /^线材/, 静力查表: /^静力手册/,
};
for (const [g, re] of Object.entries(groups)) {
  const ts = rest.filter((t) => re.test(t.table));
  if (!ts.length) continue;
  lines.push(`### ${g}（${ts.length} 表 / ${ts.reduce((s, t) => s + t.rows, 0)} 行）`);
  for (const t of ts) lines.push(`- **${t.table}** ${t.rows} 行 — ${t.columns.slice(0, 7).join('、')}`);
  lines.push('');
}
const unclassified = rest.filter((t) => !Object.values(groups).some((re) => re.test(t.table)));
if (unclassified.length) { lines.push('### 其他'); for (const t of unclassified) lines.push(`- ${t.table} ${t.rows} 行 — ${t.columns.slice(0, 6).join('、')}`); }

lines.push('\n## 3) 你侧的数据异常（实测，需修）\n');
lines.push('- `SMHS_Glass.db / GlassTypes` **rows=0** —— 玻璃材料库是空表，玻璃强度只能靠硬编码常量。');
lines.push('- `ProfileColumns` 样本：`RadiusOfGyrationX=0, RadiusOfGyrationY=0`（回转半径全 0，未被填充）；`ShearArea=6` 对 60/80 系列型材明显不合理 —— 汇宝该列叫 **受剪腹板总宽度**，语义与"剪切面积"不同。');
lines.push('- 汇宝把抵抗矩分成 **Wx1 / Wx2（Wy1 / Wy2）** 两套值，你只有一套 `SectionModulusX/Y` —— 这正是连续梁/双跨模型在支座与跨中取不同截面模量的关键，缺这一列就无法复刻汇宝的立柱验算。');
lines.push('- 后锚固缺 **有效锚固深度 hef、钻孔直径、性能等级** 三列 —— JGJ 145-2004 的后锚固承载力计算必须有 hef 和孔径。');
lines.push('- 面板材料只收了铝单板 26 行，汇宝另有 11 类板材（搪瓷板/钢板/不锈钢/钛合金/铜/铝塑/蜂窝铝板/铝镁硅…）共 ' + rest.filter((t) => groups['面板材料'].test(t.table)).reduce((s, t) => s + t.rows, 0) + ' 行；其中铝镁硅/钛/铜表还带 **弹性模量、泊松比、密度、热线膨胀系数**，而你的 `PanelMaterials` 这些字段是 null。');

fs.writeFileSync(path.join(OUT, 'param_library_gap.md'), lines.join('\n'), 'utf8');
const info = db.prepare("SELECT name FROM sqlite_master WHERE type='table'").all();
console.log('SQLite 表数=' + info.length + ' 总行=' + totalRows + ' 文件=' + (fs.statSync(DBPATH).size / 1024).toFixed(0) + ' KB');
console.log('报告=' + path.join(OUT, 'param_library_gap.md'));
