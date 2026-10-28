'use strict';
/*
 * scan_htm_images.js
 * 扫描汇宝帮助目录 html\ 下所有 .htm/.html/.xml（GBK/GB18030 编码），
 * 抽取其中引用到的图片路径（wmf/emf/wmz/gif/png/jpg/jpeg/bmp），
 * 统计数量、去重，并标记文件是否真实存在（可解析）。
 *
 * 只读。不写安装目录。
 * 用法: node scan_htm_images.js [htmlDir] [outJson]
 */
const fs = require('fs');
const path = require('path');

const DEFAULT_HTML = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\html';
const DEFAULT_OUT = 'C:\\code\\Glass\\docs\\recon\\reports\\htm_image_refs.json';

const IMG_EXT = new Set(['wmf', 'emf', 'wmz', 'gif', 'png', 'jpg', 'jpeg', 'bmp', 'tif', 'tiff']);
const IMG_EXTS_STR = 'wmf|emf|wmz|gif|png|jpe?g|bmp|tiff?';

function decodeGb18030(buf) {
  // Node 24 的 TextDecoder 支持 gbk / gb18030
  try {
    return new TextDecoder('gb18030').decode(buf);
  } catch (e) {
    return new TextDecoder('utf-8', { fatal: false }).decode(buf);
  }
}

function walk(dir, out = []) {
  let ents;
  try {
    ents = fs.readdirSync(dir, { withFileTypes: true });
  } catch (e) {
    return out;
  }
  for (const ent of ents) {
    const full = path.join(dir, ent.name);
    if (ent.isDirectory()) walk(full, out);
    else if (ent.isFile()) out.push(full);
  }
  return out;
}

function pctDecodeGb(s, decoder) {
  // 还原 %XX%XX 形式的 GBK 字节
  if (!/%[0-9A-Fa-f]{2}/.test(s)) return s;
  const bytes = [];
  for (let i = 0; i < s.length; i++) {
    if (s[i] === '%' && /^[0-9A-Fa-f]{2}$/.test(s.substr(i + 1, 2))) {
      bytes.push(parseInt(s.substr(i + 1, 2), 16));
      i += 2;
    } else {
      if (bytes.length) {
        // 提交当前 GBK 字节串
        s = decoder.decode(Buffer.from(bytes)) + s.slice(i);
        return s;
      }
    }
  }
  if (bytes.length) return decoder.decode(Buffer.from(bytes));
  return s;
}

/** 从 HTML 文本里抽出所有候选图片引用（含属性与裸文件名） */
function extractRefs(text) {
  const refs = [];
  const push = (raw, how, idx) => {
    if (!raw) return;
    refs.push({ raw: raw.trim(), via: how, pos: idx });
  };

  // 1) 属性型引用: src / background / href / data / longdesc / content(=url)
  const attrRe = new RegExp(
    `(?:src|background|href|data|longdesc|dynsrc|lowsrc)\\s*=\\s*(?:"([^"]*)"|'([^']*)'|([^"'\\s>]+))`,
    'gi'
  );
  let m;
  while ((m = attrRe.exec(text))) {
    const v = m[1] || m[2] || m[3] || '';
    if (new RegExp(`\\.(${IMG_EXTS_STR})`, 'i').test(v)) push(v, 'attr:' + m[0].split('=')[0].trim(), m.index);
  }

  // 2) CSS url(...)
  const cssRe = /url\(\s*(?:"([^"]*)"|'([^']*)'|([^)]*))\s*\)/gi;
  while ((m = cssRe.exec(text))) {
    const v = m[1] || m[2] || m[3] || '';
    if (new RegExp(`\\.(${IMG_EXTS_STR})`, 'i').test(v)) push(v, 'css:url', m.index);
  }

  // 3) VML: <v:imagedata src=...> 已在属性里；OLE/ActiveX 的 classid/object codebase
  const objRe = new RegExp(`[A-Za-z0-9_.\\\\/%:-]+\\.(?:${IMG_EXTS_STR})`, 'gi');
  while ((m = objRe.exec(text))) {
    push(m[0], 'bare-token', m.index);
  }

  return refs;
}

function normalizeRef(raw) {
  let s = raw.replace(/&amp;/gi, '&').replace(/&#38;/gi, '&');
  // 去掉 query / fragment
  s = s.split('#')[0].split('?')[0];
  // percent-decoding (GBK)
  let dec;
  try {
    dec = new TextDecoder('gb18030');
  } catch (e) {
    dec = null;
  }
  if (dec) s = pctDecodeGb(s, dec);
  s = s.replace(/\\/g, '/');
  // 去掉常见前缀 noise
  s = s.replace(/^[./]+/, '');
  return s.trim();
}

function main() {
  const htmlDir = process.argv[2] || DEFAULT_HTML;
  const outJson = process.argv[3] || DEFAULT_OUT;
  const rootDir = path.dirname(htmlDir);

  const allFiles = walk(htmlDir);
  const htmFiles = allFiles.filter((f) => /\.(htm|html|xml|htc|txt)$/i.test(f));

  const perPage = [];
  const globalMap = new Map(); // normalizedRelKey -> info

  const utf8Strict = new TextDecoder('utf-8', { fatal: true });
  for (const file of htmFiles) {
    const buf = fs.readFileSync(file);
    let text, encodingUsed;
    // 尝试判定编码：若严格 UTF-8 能解，则视为 UTF-8（部分新页面可能已转换）
    try {
      text = utf8Strict.decode(buf);
      encodingUsed = 'utf-8(detected)';
    } catch (e) {
      text = decodeGb18030(buf);
      encodingUsed = 'gb18030';
    }
    const refs = extractRefs(text);
    const pageRel = path.relative(htmlDir, file).replace(/\\/g, '/');
    const pageDir = path.dirname(file);
    const items = [];
    for (const r of refs) {
      const norm = normalizeRef(r.raw);
      if (!norm) continue;
      const ext = (path.extname(norm).slice(1) || '').toLowerCase();
      if (!IMG_EXT.has(ext)) continue;
      // 解析候选绝对路径
      const candidates = [];
      candidates.push(path.resolve(pageDir, norm)); // 相对引用页所在目录
      candidates.push(path.resolve(htmlDir, norm)); // 相对 html 根
      candidates.push(path.resolve(rootDir, norm)); // 相对安装根
      // 仅按文件名在 html / pic 下搜索
      let resolved = null;
      for (const c of candidates) {
        if (fs.existsSync(c) && fs.statSync(c).isFile()) {
          resolved = c;
          break;
        }
      }
      let resolvedKind = resolved ? 'direct' : null;
      if (!resolved) {
        const base = path.basename(norm);
        const hit = allFiles.find((f) => path.basename(f).toLowerCase() === base.toLowerCase());
        if (hit) {
          resolved = hit;
          resolvedKind = 'by-basename';
        }
      }
      const key = resolved
        ? path.relative(rootDir, resolved).replace(/\\/g, '\\').toLowerCase()
        : 'MISSING:' + norm.toLowerCase();
      const rec = {
        page: pageRel,
        raw: r.raw,
        norm,
        ext,
        via: r.via,
        resolved: resolved,
        resolvedKind,
        key,
        bytes: resolved ? fs.statSync(resolved).size : null,
      };
      items.push(rec);
      if (!globalMap.has(key)) {
        globalMap.set(key, {
          key,
          ext,
          resolved: resolved,
          bytes: rec.bytes,
          normForms: new Set(),
          pages: new Set(),
        });
      }
      const g = globalMap.get(key);
      g.normForms.add(norm);
      g.pages.add(pageRel);
    }
    perPage.push({ page: pageRel, encoding: encodingUsed, bytes: buf.length, refCount: items.length, refs: items });
  }

  const uniq = [...globalMap.values()].map((g) => ({
    key: g.key,
    ext: g.ext,
    resolved: g.resolved || null,
    bytes: g.bytes,
    normForms: [...g.normForms],
    pages: [...g.pages].sort(),
  }));
  uniq.sort((a, b) => a.key.localeCompare(b.key, 'en'));

  const byExt = {};
  const byExtMissing = {};
  for (const u of uniq) {
    byExt[u.ext] = (byExt[u.ext] || 0) + 1;
    if (!u.resolved) byExtMissing[u.ext] = (byExtMissing[u.ext] || 0) + 1;
  }
  const byPage = {};
  const byPageExt = {};
  for (const p of perPage) {
    const c = {};
    for (const r of p.refs) {
      c[r.ext] = (c[r.ext] || 0) + 1;
      byPageExt[p.page] = byPageExt[p.page] || {};
      byPageExt[p.page][r.ext] = (byPageExt[p.page][r.ext] || 0) + 1;
    }
    byPage[p.page] = c;
  }

  const out = {
    generatedAt: new Date().toISOString(),
    htmlDir,
    rootDir,
    htmFileCount: htmFiles.length,
    totalRefs: perPage.reduce((a, p) => a + p.refCount, 0),
    uniqueRefs: uniq.length,
    byExt,
    byExtMissing,
    missingCount: uniq.filter((u) => !u.resolved).length,
    pages: perPage,
    unique: uniq,
  };
  fs.mkdirSync(path.dirname(outJson), { recursive: true });
  fs.writeFileSync(outJson, JSON.stringify(out, null, 2), 'utf8');

  // 控制台摘要
  console.log('htm files scanned:', htmFiles.length);
  console.log('total image refs:', out.totalRefs);
  console.log('unique image refs:', uniq.length);
  console.log('by ext:', JSON.stringify(byExt));
  console.log('missing (未解析):', out.missingCount, JSON.stringify(byExtMissing));
  console.log('--- 每页图片引用计数 ---');
  for (const p of perPage) {
    if (p.refCount) console.log(String(p.refCount).padStart(5), p.page, JSON.stringify(byPage[p.page]), p.encoding);
  }
  console.log('--- 去重后的 wmf/emf/wmz 引用 ---');
  for (const u of uniq) {
    if (['wmf', 'emf', 'wmz'].includes(u.ext)) {
      console.log(u.ext, '|', u.resolved || 'MISSING', '|', u.bytes || '-', '|', u.pages.join(','), '|', u.normForms.join(' ; '));
    }
  }
  console.log('wrote:', outJson);
}

main();
