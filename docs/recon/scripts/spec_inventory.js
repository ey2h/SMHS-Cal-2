'use strict';
/*
 * spec_inventory.js
 * Recursively inventory the 汇宝 spec/attachment/thermal source directories (READ ONLY).
 * Output: docs/recon/reports/specs_inventory.json
 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const ROOTS = [
  {
    label: 'guifan',
    label_cn: '规范',
    path: 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\规范',
  },
  {
    label: 'fujian',
    label_cn: '附件',
    path: 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\附件',
  },
  {
    label: 'regong',
    label_cn: 'pic\\热工',
    path: 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\pic\\热工',
  },
];

const OUT = 'C:\\code\\Glass\\docs\\recon\\reports\\specs_inventory.json';

function walk(dir, rootDir, outDirs, outFiles) {
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch (e) {
    outFiles.push({ error: 'readdir_failed', path: dir, message: e.message });
    return;
  }
  entries.sort((a, b) => a.name.localeCompare(b.name, 'zh-Hans-CN'));
  for (const ent of entries) {
    const full = path.join(dir, ent.name);
    const rel = path.relative(rootDir, full);
    if (ent.isDirectory()) {
      outDirs.push({ path: full, relPath: rel, name: ent.name });
      walk(full, rootDir, outDirs, outFiles);
    } else if (ent.isFile()) {
      let st;
      try {
        st = fs.statSync(full);
      } catch (e) {
        outFiles.push({ error: 'stat_failed', path: full, message: e.message });
        continue;
      }
      let sha256 = null;
      try {
        sha256 = crypto.createHash('sha256').update(fs.readFileSync(full)).digest('hex');
      } catch (e) {
        sha256 = null;
      }
      outFiles.push({
        path: full,
        relPath: rel,
        name: ent.name,
        ext: path.extname(ent.name).toLowerCase(),
        sizeBytes: st.size,
        mtime: st.mtime.toISOString(),
        sha256,
      });
    } else {
      outFiles.push({ error: 'not_a_regular_file', path: full, relPath: rel });
    }
  }
}

const result = {
  generatedAt: new Date().toISOString(),
  note: '源目录只读；本脚本仅读取，不写入源目录。sha256 为源文件内容哈希。',
  roots: [],
  allFiles: [],
  byExtension: {},
};

for (const root of ROOTS) {
  const dirs = [];
  const files = [];
  const exists = fs.existsSync(root.path);
  if (exists) walk(root.path, root.path, dirs, files);
  const realFiles = files.filter((f) => !f.error);
  const totalBytes = realFiles.reduce((s, f) => s + f.sizeBytes, 0);
  const extAgg = {};
  for (const f of realFiles) {
    const k = f.ext || '(no-ext)';
    extAgg[k] = extAgg[k] || { count: 0, bytes: 0 };
    extAgg[k].count += 1;
    extAgg[k].bytes += f.sizeBytes;
  }
  result.roots.push({
    label: root.label,
    label_cn: root.label_cn,
    path: root.path,
    exists,
    dirCount: dirs.length,
    fileCount: realFiles.length,
    errorCount: files.length - realFiles.length,
    totalBytes,
    byExtension: extAgg,
    dirs,
    files,
  });
  for (const f of files) {
    result.allFiles.push(Object.assign({ root: root.label, root_cn: root.label_cn }, f));
  }
}

for (const f of result.allFiles) {
  if (f.error) continue;
  const k = f.ext || '(no-ext)';
  result.byExtension[k] = result.byExtension[k] || { count: 0, bytes: 0 };
  result.byExtension[k].count += 1;
  result.byExtension[k].bytes += f.sizeBytes;
}
result.totals = {
  files: result.allFiles.filter((f) => !f.error).length,
  errors: result.allFiles.filter((f) => f.error).length,
  dirs: result.roots.reduce((s, r) => s + r.dirCount, 0),
  bytes: result.roots.reduce((s, r) => s + r.totalBytes, 0),
};

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, JSON.stringify(result, null, 2), 'utf8');

console.log('wrote ' + OUT);
console.log('totals: ' + JSON.stringify(result.totals));
console.log('byExt: ' + JSON.stringify(result.byExtension));
for (const r of result.roots) {
  console.log(`root ${r.label_cn} exists=${r.exists} dirs=${r.dirCount} files=${r.fileCount} bytes=${r.totalBytes}`);
}
