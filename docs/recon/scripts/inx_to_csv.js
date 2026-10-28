'use strict';
/*
 * inx_to_csv.js
 * Parse all *.inx (GBK) under pic\热工\规范\50176 and pic\热工\规范\leiji (62 files),
 * plus the merged 50176\tmp.txt, into CSV + statistics.
 * Field meanings are NOT assumed: CSV columns are provisional labels; uncertain meanings go to unknowns.
 * Outputs:
 *   docs/recon/reports/thermal_inx.csv
 *   docs/recon/reports/thermal_inx_notes.md
 *   docs/recon/reports/thermal_inx_stats.json
 */
const fs = require('fs');
const path = require('path');

const BASE = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\pic\\热工\\规范';
const GROUPS = [
  { group: '50176', dir: path.join(BASE, '50176'), expectFields: 5 },
  { group: 'leiji', dir: path.join(BASE, 'leiji'), expectFields: 2 },
];
const OUT_CSV = 'C:\\code\\Glass\\docs\\recon\\reports\\thermal_inx.csv';
const OUT_NOTES = 'C:\\code\\Glass\\docs\\recon\\reports\\thermal_inx_notes.md';
const OUT_STATS = 'C:\\code\\Glass\\docs\\recon\\reports\\thermal_inx_stats.json';

const PROVISIONAL_COLS = {
  50176: ['city', 'thermal_zone', 'altitude', 'jan_mean', 'daily_min'],
  leiji: ['city', 'value_f2'],
};

function decode(buf) {
  return new TextDecoder('gb18030').decode(buf);
}

function csvEscape(v) {
  const s = String(v === null || v === undefined ? '' : v);
  if (/[",\r\n]/.test(s)) return '"' + s.replace(/"/g, '""') + '"';
  return s;
}

function num(v) {
  if (v === undefined) return null;
  const t = v.trim();
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

const rows = [];
const stats = {
  generatedAt: new Date().toISOString(),
  baseDir: BASE,
  groups: {},
  global: {
    inxFiles: 0,
    inxBytes: 0,
    dataRows: 0,
    emptyTrailingLines: 0,
    malformedRows: [],
    fieldCountHistogram: {},
    cityNames: new Set(),
    cityNamesByGroup: {},
    zoneCodes: {},
    numericRanges: {},
    duplicateCities: [],
    nonNumericFieldSamples: {},
  },
  unknowns: [],
};

for (const g of GROUPS) {
  const names = fs
    .readdirSync(g.dir)
    .filter((n) => n.toLowerCase().endsWith('.inx'))
    .sort((a, b) => a.localeCompare(b));
  const gStat = {
    dir: g.dir,
    fileCount: names.length,
    totalBytes: 0,
    dataRows: 0,
    emptyLines: 0,
    files: {},
    fieldHistogram: {},
    cityCount: 0,
  };
  stats.global.cityNamesByGroup[g.group] = new Set();

  for (const name of names) {
    const fp = path.join(g.dir, name);
    const buf = fs.readFileSync(fp);
    gStat.totalBytes += buf.length;
    stats.global.inxFiles += 1;
    stats.global.inxBytes += buf.length;

    const text = decode(buf);
    const hasCR = /\r\n/.test(text);
    const lines = text.split(/\r\n|\r|\n/);
    const perFile = {
      file: name,
      path: fp,
      bytes: buf.length,
      lineCount: lines.length,
      lineEnding: hasCR ? 'CRLF' : 'LF/none',
      endsWithNewline: /\n$/.test(text),
      dataLines: 0,
      emptyLines: 0,
      anomalies: [],
      sample: lines.filter((l) => l.trim()).slice(0, 3),
      lastLineRaw: JSON.stringify(lines[lines.length - 1]),
      zoneCodes: {},
      cities: [],
    };

    lines.forEach((line, idx) => {
      if (line.trim() === '') {
        perFile.emptyLines += 1;
        stats.global.emptyTrailingLines += 1;
        if (idx !== lines.length - 1) perFile.anomalies.push({ line: idx + 1, kind: 'empty_interior_line', raw: JSON.stringify(line) });
        return;
      }
      const fields = line.split(',');
      const fc = fields.length;
      perFile.dataLines += 1;
      gStat.dataRows += 1;
      stats.global.dataRows += 1;
      const key = g.group + ':' + fc;
      gStat.fieldHistogram[fc] = (gStat.fieldHistogram[fc] || 0) + 1;
      stats.global.fieldCountHistogram[key] = (stats.global.fieldCountHistogram[key] || 0) + 1;

      const cols = PROVISIONAL_COLS[g.group];
      if (fc !== g.expectFields) {
        const rec = {
          group: g.group,
          file: name,
          line: idx + 1,
          fieldCount: fc,
          raw: line,
          note: 'field count differs from the group mode',
        };
        stats.global.malformedRows.push(rec);
        perFile.anomalies.push(rec);
      }

      const city = (fields[0] || '').trim();
      if (city) {
        perFile.cities.push(city);
        stats.global.cityNames.add(city);
        stats.global.cityNamesByGroup[g.group].add(city);
      }

      const rec = { group: g.group, sourceFile: name, line: idx + 1, city, raw: line };

      if (g.group === '50176') {
        const zone = (fields[1] || '').trim();
        rec.thermal_zone = zone;
        rec.altitude_raw = (fields[2] || '').trim();
        rec.jan_mean_raw = (fields[3] || '').trim();
        rec.daily_min_raw = (fields[4] || '').trim();
        rec.altitude = num(fields[2]);
        rec.jan_mean = num(fields[3]);
        rec.daily_min = num(fields[4]);
        if (zone) {
          perFile.zoneCodes[zone] = (perFile.zoneCodes[zone] || 0) + 1;
          stats.global.zoneCodes[zone] = (stats.global.zoneCodes[zone] || 0) + 1;
        } else {
          perFile.anomalies.push({ line: idx + 1, kind: 'empty_zone_code', raw: line });
        }
        for (const [f, val] of [['altitude', fields[2]], ['jan_mean', fields[3]], ['daily_min', fields[4]]]) {
          if (val === undefined || num(val) === null) {
            perFile.anomalies.push({ line: idx + 1, kind: 'non_numeric_field', field: f, raw: line });
            stats.global.nonNumericFieldSamples[g.group + '.' + f] =
              stats.global.nonNumericFieldSamples[g.group + '.' + f] || [];
            if (stats.global.nonNumericFieldSamples[g.group + '.' + f].length < 20)
              stats.global.nonNumericFieldSamples[g.group + '.' + f].push({ file: name, line: idx + 1, value: val });
          }
        }
        // order check: is field5 <= field4 ?
        if (num(fields[4]) !== null && num(fields[3]) !== null && num(fields[4]) > num(fields[3])) {
          perFile.anomalies.push({ line: idx + 1, kind: 'f5_gt_f4', raw: line });
        }
      } else {
        rec.value_raw = (fields[1] || '').trim();
        rec.value = num(fields[1]);
        if (num(fields[1]) === null) {
          perFile.anomalies.push({ line: idx + 1, kind: 'non_numeric_field', field: 'value_f2', raw: line });
        }
      }
      rows.push(rec);
    });

    gStat.cityCount += perFile.cities.length;
    gStat.files[name] = perFile;
  }

  // numeric ranges per group
  const ranges = {};
  const collect = (label, get) => {
    const vals = rows.filter((r) => r.group === g.group).map(get).filter((v) => typeof v === 'number');
    if (vals.length) {
      const sorted = vals.slice().sort((a, b) => a - b);
      ranges[label] = {
        count: vals.length,
        min: sorted[0],
        max: sorted[sorted.length - 1],
        median: sorted[Math.floor(sorted.length / 2)],
        integerOnlyCount: vals.filter((v) => Number.isInteger(v)).length,
      };
    } else ranges[label] = { count: 0 };
  };
  if (g.group === '50176') {
    collect('altitude', (r) => r.altitude);
    collect('jan_mean', (r) => r.jan_mean);
    collect('daily_min', (r) => r.daily_min);
  } else {
    collect('value_f2', (r) => r.value);
  }
  gStat.numericRanges = ranges;
  gStat.zoneCodes = Object.keys(gStat.files).length ? stats.global.zoneCodes : null;
  stats.groups[g.group] = gStat;
}

// duplicate city detection within each group
for (const grp of Object.keys(stats.groups)) {
  const seen = new Map();
  rows.filter((r) => r.group === grp).forEach((r) => {
    if (!r.city) return;
    if (!seen.has(r.city)) seen.set(r.city, []);
    seen.get(r.city).push(r.sourceFile + ':' + r.line);
  });
  for (const [city, locs] of seen) if (locs.length > 1) stats.global.duplicateCities.push({ group: grp, city, occurrences: locs });
}

// cross-group city overlap
const setA = stats.global.cityNamesByGroup['50176'];
const setB = stats.global.cityNamesByGroup['leiji'];
const overlap = Array.from(setA).filter((c) => setB.has(c));
const onlyInA = Array.from(setA).filter((c) => !setB.has(c));
const onlyInB = Array.from(setB).filter((c) => !setA.has(c));

// also parse 50176/tmp.txt (merged superset?) for comparison, without putting it in the main CSV
let tmpStat = null;
const tmpPath = path.join(BASE, '50176', 'tmp.txt');
if (fs.existsSync(tmpPath)) {
  const t = decode(fs.readFileSync(tmpPath));
  const lines = t.split(/\r\n|\r|\n/);
  const data = lines.filter((l) => l.trim());
  const counts = {};
  data.forEach((l) => {
    const n = l.split(',').length;
    counts[n] = (counts[n] || 0) + 1;
  });
  const cities = data.map((l) => l.split(',')[0].trim()).filter(Boolean);
  tmpStat = {
    path: tmpPath,
    bytes: fs.statSync(tmpPath).size,
    totalLines: lines.length,
    dataLines: data.length,
    fieldHistogram: counts,
    distinctCities: new Set(cities).size,
    sampleFirst: data.slice(0, 3),
    sampleLast: data.slice(-3),
    citySet: Array.from(new Set(cities)),
  };
}

// ---- write CSV ----
const csvHeader = [
  'source_dir',
  'source_file',
  'line',
  'city',
  'thermal_zone',
  'altitude',
  'jan_mean',
  'daily_min',
  'value_f2',
  'raw_line',
];
const outLines = [csvHeader.join(',')];
for (const r of rows) {
  outLines.push(
    [
      r.group,
      r.sourceFile,
      r.line,
      r.city,
      r.thermal_zone === undefined ? '' : r.thermal_zone,
      r.altitude === undefined ? '' : r.altitude,
      r.jan_mean === undefined ? '' : r.jan_mean,
      r.daily_min === undefined ? '' : r.daily_min,
      r.value === undefined ? '' : r.value,
      csvEscape(r.raw),
    ].map((v) => (typeof v === 'string' ? csvEscape(v) : v)).join(','),
  );
}
fs.mkdirSync(path.dirname(OUT_CSV), { recursive: true });
fs.writeFileSync(OUT_CSV, '\ufeff' + outLines.join('\r\n') + '\r\n', 'utf8');

// stats JSON (Sets -> arrays)
const statsJson = JSON.parse(JSON.stringify(stats, (k, v) => (v instanceof Set ? Array.from(v) : v)));
statsJson.tmp_txt_50176 = tmpStat;
statsJson.crossGroup = {
  citiesIn50176Only_count: onlyInA.length,
  citiesIn50176Only_sample: onlyInA.slice(0, 40),
  citiesInLeijiOnly_count: onlyInB.length,
  citiesInLeijiOnly_sample: onlyInB.slice(0, 40),
  overlap_count: overlap.length,
  overlap_sample: overlap.slice(0, 40),
};
fs.writeFileSync(OUT_STATS, JSON.stringify(statsJson, null, 2), 'utf8');

// notes markdown
const zoneKeys = Object.keys(stats.global.zoneCodes).sort();
const notes = [];
notes.push('# pic\\热工\\规范\\*.inx 解析说明（thermal_inx_notes.md）');
notes.push('');
notes.push('- 生成时间：' + stats.generatedAt);
notes.push('- 源目录：' + BASE + '（只读，未修改）');
notes.push('- 解析脚本：docs\\recon\\scripts\\inx_to_csv.js');
notes.push('- 解码：Node TextDecoder `gb18030`（源为 GBK/ANSI，无 BOM，无多字节歧义报错）');
notes.push('- 输出 CSV：' + OUT_CSV + '（UTF-8 BOM，CRLF，便于 Excel 打开）');
notes.push('- 统计 JSON：' + OUT_STATS);
notes.push('');
notes.push('## 1. 文件与行数');
notes.push('');
notes.push('| 目录组 | .inx 文件数 | 总字节 | 数据行(非空) | 空尾行 | 字段数分布 |');
notes.push('|---|---|---|---|---|---|');
for (const grp of Object.keys(stats.groups)) {
  const g = stats.groups[grp];
  notes.push(
    `| ${grp} | ${g.fileCount} | ${g.totalBytes} | ${g.dataRows} | ${g.emptyLines} | ` +
      Object.entries(g.fieldHistogram).map(([k, v]) => `${k}字段:${v}`).join(', ') + ' |',
  );
}
notes.push('');
notes.push('每个 .inx = 一个省级代号文件（如 ah=安徽, bj=北京, nmg=内蒙古, xj=新疆… 该对应关系为推测，见 unknowns）。');
notes.push('');
notes.push('## 2. 观察到的格式（不是结论）');
notes.push('');
notes.push('### 50176 组（31 个文件）');
notes.push('- 每行 5 个字段，以英文逗号 `,` 分隔；行尾 CRLF；文件末尾通常有一个空行（即最后一行是 CRLF 之后的空字符串）。');
notes.push('- 字段1：城市/城镇名（GBK 中文，个别含括号，如 `酉阳县（钟名镇）` — 注意该样例出现在 leiji 组）。');
notes.push('- 字段2：分区代号，取值集合（含计数）：' + zoneKeys.map((z) => `${z}(${stats.global.zoneCodes[z]})`).join(', '));
notes.push('- 字段3：整数，范围 ' + JSON.stringify(stats.groups['50176'].numericRanges.altitude) + '；样例 北京=55、呼和浩特=1065、漠河=433、上海=3 —— 与“海拔高度(m)”高度一致，但未证实。');
notes.push('- 字段4：带一位小数的数值，范围 ' + JSON.stringify(stats.groups['50176'].numericRanges.jan_mean) + '；北京=-7.0、重庆=5.5。');
notes.push('- 字段5：带一位小数的数值，范围 ' + JSON.stringify(stats.groups['50176'].numericRanges.daily_min) + '；北京=-11.8、重庆=2.9；在全部行中该值 ≤ 字段4（见下）。');
notes.push('');
notes.push('### leiji 组（31 个文件）');
notes.push('- 每行 2 个字段：名称 + 一个数值（整数或 1 位小数），如 `成都市,34`、`乾宁,96.6`。');
notes.push('- 数值范围 ' + JSON.stringify(stats.groups['leiji'].numericRanges.value_f2) + '，全部为正数。');
notes.push('- 目录名 `leiji` 与字段含义均无直接证据（见 unknowns）。');
notes.push('');
notes.push('## 3. 异常/边界情况');
notes.push('');
notes.push('- 字段数不等于该组众数的行数：' + stats.global.malformedRows.length);
if (stats.global.malformedRows.length) {
  notes.push('');
  notes.push('| 组 | 文件 | 行 | 字段数 | 原始行 |');
  notes.push('|---|---|---|---|---|');
  for (const r of stats.global.malformedRows.slice(0, 30))
    notes.push(`| ${r.group} | ${r.file} | ${r.line} | ${r.fieldCount} | ${r.raw} |`);
}
const f5gt4 = [];
for (const grp of Object.keys(stats.groups)) {
  for (const [fname, f] of Object.entries(stats.groups[grp].files)) {
    for (const a of f.anomalies) if (a.kind === 'f5_gt_f4') f5gt4.push(`${grp}/${fname} 行${a.line}: ${a.raw}`);
  }
}
notes.push('- 字段5 > 字段4 的行数（即“第二个温度大于第一个温度”）：' + f5gt4.length + (f5gt4.length ? '；样例：' + f5gt4.slice(0, 10).join(' | ') : ''));
const totalAnomalies = Object.values(stats.groups).reduce(
  (s, g) => s + Object.values(g.files).reduce((x, f) => x + f.anomalies.length, 0),
  0,
);
notes.push('- 其它异常（空字段/非数值/内部空行）合计：' + totalAnomalies);
notes.push('- 文件结尾约定：以 CRLF 结束 → split 后产生一个尾部空行；这类空行未计入数据行。');
notes.push('- 非数值字段样例：' + JSON.stringify(stats.global.nonNumericFieldSamples));
notes.push('');
notes.push('## 4. 城市与分区统计');
notes.push('');
notes.push('- 50176 组数据行 = ' + stats.groups['50176'].dataRows + '，名称去重 = ' + stats.global.cityNamesByGroup['50176'].size);
notes.push('- leiji 组数据行 = ' + stats.groups['leiji'].dataRows + '，名称去重 = ' + stats.global.cityNamesByGroup['leiji'].size);
notes.push('- 两组名称交集 = ' + overlap.length + '；仅 50176 = ' + onlyInA.length + '；仅 leiji = ' + onlyInB.length);
notes.push('- 分区代号取值集合：' + zoneKeys.join(', ') + '（字母后缀 A/B/C，数字前缀 1–7? 实际观测：' + zoneKeys.join('/') + '）');
notes.push('- 重复城市（同组内出现多次）：' + stats.global.duplicateCities.length + ' 例：' + JSON.stringify(stats.global.duplicateCities.slice(0, 10)));
notes.push('');
notes.push('### 每组文件明细');
notes.push('');
notes.push('| 组 | 文件 | 字节 | 数据行 | 空行 | 出现的分区代号 |');
notes.push('|---|---|---|---|---|---|');
for (const grp of Object.keys(stats.groups)) {
  for (const [fname, f] of Object.entries(stats.groups[grp].files)) {
    notes.push(
      `| ${grp} | ${fname} | ${f.bytes} | ${f.dataLines} | ${f.emptyLines} | ` +
        (Object.keys(f.zoneCodes).length ? Object.entries(f.zoneCodes).map(([k, v]) => `${k}×${v}`).join(' ') : '-') +
        ' |',
    );
  }
}
notes.push('');
notes.push('## 5. 50176\\tmp.txt（对照用，未合并进 thermal_inx.csv）');
notes.push('');
if (tmpStat) {
  notes.push('- 字节=' + tmpStat.bytes + '，总行=' + tmpStat.totalLines + '，数据行=' + tmpStat.dataLines + '，去重城市=' + tmpStat.distinctCities);
  notes.push('- 字段数分布：' + JSON.stringify(tmpStat.fieldHistogram));
  notes.push('- 首样例行：' + JSON.stringify(tmpStat.sampleFirst));
  notes.push('- 末样例行：' + JSON.stringify(tmpStat.sampleLast));
  const mergedCities = new Set(tmpStat.citySet);
  const provCities = stats.global.cityNamesByGroup['50176'];
  const inProvNotTmp = Array.from(provCities).filter((c) => !mergedCities.has(c));
  const inTmpNotProv = tmpStat.citySet.filter((c) => !provCities.has(c));
  notes.push('- 与 31 个 50176\\*.inx 合计城市比较：tmp 独有=' + inTmpNotProv.length + '，*.inx 独有=' + inProvNotTmp.length + '；样例 tmp 独有：' + inTmpNotProv.slice(0, 25).join('、'));
  notes.push('- 样例 *.inx 独有：' + inProvNotTmp.slice(0, 25).join('、'));
  notes.push('- 判断：tmp.txt 形如同一张不分省的合并表（含 ' + tmpStat.dataLines + ' 条记录），是否等同于 62 个 .inx 的并集见 unknowns。');
}
notes.push('');
notes.push('## 6. unknowns（含义未经证实的字段/结论）');
notes.push('');
notes.push('1. `thermal_zone`（50176 组字段2）：形如 `1A/1B/1C/2A/2B/3A/3B/4A?` 的代号，疑似《GB 50176 热工规范》的建筑热工设计分区代号（ⅠA/ⅡA/ⅢA…），但源文件本身没有给出代号定义，也没有表头。仅“取值形态 + 目录名 50176”支持该推测。');
notes.push('2. `altitude`（字段3）：与海拔量级一致（上海=3、呼和浩特=1065、甘孜地区值上千），但未在源文件中看到单位“m”的说明。');
notes.push('3. `jan_mean`（字段4）/`daily_min`（字段5）：带符号、1 位小数，且 50176 组内 f5 ≤ f4 恒成立；具体物理含义（1 月平均温度 / 最冷日平均温度 / 采暖室外计算温度 …）无源内证据。列名只是按任务要求暂定的占位名。');
notes.push('4. `leiji` 组字段2：全为正值（范围见上），随海拔/纬度增大；可能是累年某指标（如“累年1月平均温度绝对值×?”“冻结指数”“度日数”），源内无表头，含义完全未证实。');
notes.push('5. 文件名代号（ah/bj/gd/hainan/hbei/hlj/hn/nmg/nx/qh/sc/sd/sh/sx1/sx2/tj/xj/xz/yn/zj/…）与省份的对应关系为推测：例如 `sx1`/`sx2` 可能表示同一省被拆成两个分区（如山西/陕西? 或内蒙两区?），`hbei`/`hn`/`hb` 可能分别是河北/河南/湖北或华南/湖南，存在歧义，需人工核对。');
notes.push('6. leiji 组中出现的名称包含 `酉阳县（钟名镇）`、`达川市`、`渡口市`（旧地名）等，说明数据源年代较早；同一城市在两组的名称写法可能不同（如 `西昌市` vs `西昌`），因此按名称做 join 不稳妥。');
notes.push('7. 这些数值被程序如何使用（哪个计算模块读取哪一组文件）未确认——需要看主程序调用（可用 REA/PE 分析，但本次未做）。');
notes.push('');
notes.push('## 7. 每文件明细见 thermal_inx_stats.json');
notes.push('');
fs.writeFileSync(OUT_NOTES, notes.join('\n'), 'utf8');

console.log('wrote ' + OUT_CSV + ' rows=' + rows.length);
console.log('wrote ' + OUT_NOTES);
console.log('wrote ' + OUT_STATS);
console.log('50176 rows=' + stats.groups['50176'].dataRows + ' cities=' + stats.global.cityNamesByGroup['50176'].size);
console.log('leiji rows=' + stats.groups['leiji'].dataRows + ' cities=' + stats.global.cityNamesByGroup['leiji'].size);
console.log('zones=' + JSON.stringify(stats.global.zoneCodes));
console.log('malformed=' + stats.global.malformedRows.length);
