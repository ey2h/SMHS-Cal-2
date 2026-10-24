/*
 * hb_extract_help.js
 * Extract plain text + ordered image inventory from the 汇宝 V35.0 help pages (GBK/GB18030).
 * - plain text  -> C:\code\Glass\docs\recon\help\plain\<PAGE>.txt  (UTF-8, structure kept)
 * - inventory   -> C:\code\Glass\docs\recon\work\hb_page_images.json
 * Excludes B01/B02/B03 (owned by another agent).
 */
const fs = require('fs');
const path = require('path');

const HTML_DIR = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\html';
const OUT_PLAIN = 'C:\\code\\Glass\\docs\\recon\\help\\plain';
const OUT_WORK = 'C:\\code\\Glass\\docs\\recon\\work';
const EXCLUDE = new Set(['B01.htm', 'B02.htm', 'B03.htm']);
const IMG_EXT = new Set(['gif', 'jpg', 'jpeg', 'png', 'bmp', 'wmf', 'wmz', 'emf', 'tif', 'tiff', 'svg']);

const decode = b => new TextDecoder('gb18030', { fatal: false }).decode(b);

function decodeEntities(s) {
  return s.replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(parseInt(d, 10)))
    .replace(/&#x([0-9a-fA-F]+);/g, (_, h) => String.fromCodePoint(parseInt(h, 16)))
    .replace(/&nbsp;/gi, ' ')
    .replace(/&lt;/gi, '<').replace(/&gt;/gi, '>')
    .replace(/&quot;/gi, '"').replace(/&apos;|&#39;/gi, "'")
    .replace(/&mdash;/gi, '—').replace(/&ndash;/gi, '–')
    .replace(/&hellip;/gi, '…').replace(/&times;/gi, '×')
    .replace(/&divide;/gi, '÷').replace(/&plusmn;/gi, '±')
    .replace(/&le;|&leq;/gi, '≤').replace(/&ge;|&geq;/gi, '≥')
    .replace(/&deg;/gi, '°').replace(/&sup2;/g, '²').replace(/&sup3;/g, '³')
    .replace(/&amp;/gi, '&');
}

function attr(body, name) {
  const re = new RegExp('\\b' + name + '\\s*=\\s*(?:"([^"]*)"|\'([^\']*)\'|([^\\s"\'>]+))', 'i');
  const m = body.match(re);
  if (!m) return null;
  return m[1] !== undefined ? m[1] : (m[2] !== undefined ? m[2] : m[3]);
}

// ---- disk index for resolving image references (case-insensitive) ----
function buildDiskIndex() {
  const idx = new Map(); // lowercased relative path -> {abs, bytes, ext}
  const walk = (rel) => {
    const abs = rel ? path.join(HTML_DIR, rel) : HTML_DIR;
    for (const e of fs.readdirSync(abs, { withFileTypes: true })) {
      const childRel = rel ? rel + '\\' + e.name : e.name;
      const childAbs = path.join(abs, e.name);
      if (e.isDirectory()) walk(childRel);
      else if (e.isFile()) {
        const ext = path.extname(e.name).slice(1).toLowerCase();
        if (IMG_EXT.has(ext)) {
          idx.set(childRel.toLowerCase(), { abs: childAbs, rel: childRel, bytes: fs.statSync(childAbs).size, ext });
        }
      }
    }
  };
  walk('');
  return idx;
}

function resolveRef(raw, diskIdx) {
  let s = String(raw).trim().replace(/\\/g, '\\').replace(/\//g, '\\');
  try { s = decodeURIComponent(s); } catch (e) { /* keep raw */ }
  s = s.replace(/^\.\\/, '').replace(/^\.\/?/, '');
  // absolute path inside the same html tree -> make relative to html dir
  const lowAll = s.toLowerCase();
  const dirLow = HTML_DIR.toLowerCase();
  let key = lowAll;
  if (lowAll.startsWith(dirLow)) key = lowAll.slice(dirLow.length).replace(/^[\\]+/, '');
  else if (/^[a-z]:\\|^\\\\/.test(s)) {
    // absolute path outside our dir: try basename fallback match below
    key = null;
  }
  let hit = key ? diskIdx.get(key) : null;
  if (!hit) {
    const base = path.basename(s).toLowerCase();
    for (const [k, v] of diskIdx) if (path.basename(k) === base) { hit = v; break; }
  }
  return hit || { abs: null, rel: key || s, bytes: null, ext: (path.extname(s).slice(1) || '').toLowerCase(), missing: true };
}

// ---- HTML -> linear text with inline image markers ----
function unwrapConditional(html) {
  // Word downlevel-revealed/hidden conditional comments:<!--[if gte vml 1]> ... <![endif]-->
  // Remove only the wrappers so the VML markup (and its <v:imagedata>) becomes ordinary tags.
  return html
    .replace(/<!--\[if[^\]]*\]>/gi, '')
    .replace(/<!\[if[^\]]*\]>/gi, '')
    .replace(/<!--\[endif\]-->/gi, '')
    .replace(/<!\[endif\]>/gi, '');
}

function htmlToText(htmlRaw) {
  html = unwrapConditional(htmlRaw);
  // drop Word/VML definition blocks that would otherwise leak metadata text
  html = html.replace(/<xml[\s\S]*?<\/xml>/gi, '');
  html = html.replace(/<v:shapetype[\s\S]*?<\/v:shapetype>/gi, '');
  html = html.replace(/<v:formulas[\s\S]*?<\/v:formulas>/gi, '');
  html = html.replace(/<o:SmartTagType[^>]*>/gi, '');
  html = html.replace(/<w:[A-Za-z][^>]*>[\s\S]*?<\/w:[A-Za-z][^>]*>/gi, '');
  // drop script/style/xml-map blocks
  html = html.replace(/<script[\s\S]*?<\/script>/gi, '\n');
  html = html.replace(/<style[\s\S]*?<\/style>/gi, '\n');
  html = html.replace(/<link[\s\S]*?\/?>/gi, '\n');
  html = html.replace(/<meta[\s\S]*?\/?>/gi, '');

  const titleM = html.match(/<title[^>]*>([\s\S]*?)<\/title>/i);
  const title = titleM ? decodeEntities(titleM[1]).replace(/\s+/g, ' ').trim() : '';

  // remove head section (already mostly neutralized) but keep body text
  html = html.replace(/<head[\s\S]*?<\/head>/gi, '');

  const out = [];
  const events = [];
  let markerSeq = 0;
  let cellDepth = 0;
  let inObject = 0;

  const push = s => { if (s) out.push(s); };
  const pushText = raw => {
    if (!raw) return;
    let t = decodeEntities(raw);
    t = t.replace(/\s+/g, ' ');
    if (t === ' ') { if (out.length && /[\s]$/.test(out[out.length - 1] || '')) return; push(' '); return; }
    push(t);
  };

  let i = 0;
  while (i < html.length) {
    const lt = html.indexOf('<', i);
    if (lt < 0) { pushText(html.slice(i)); break; }
    pushText(html.slice(i, lt));
    // comments
    if (html.startsWith('<!--', lt)) {
      const end = html.indexOf('-->', lt + 4);
      i = end < 0 ? html.length : end + 3;
      continue;
    }
    // conditional CDATA comments <!--[ ... ]> handled above by <!--
    const gt = html.indexOf('>', lt + 1);
    if (gt < 0) { pushText(html.slice(lt)); break; }
    let body = html.slice(lt + 1, gt);
    i = gt + 1;
    if (/^!/.test(body)) continue; // doctype
    const closing = body.startsWith('/');
    body = body.replace(/^\/+/, '');
    const nameM = body.match(/^([A-Za-z][A-Za-z0-9:_-]*)/);
    if (!nameM) continue;
    const name = nameM[1].toLowerCase();

    if (inObject) {
      if (name === 'object' && !closing) inObject++;
      else if (name === 'object' && closing) inObject--;
      continue;
    }

    if (name === 'object' || name === 'embed') { if (!closing) inObject = 1; continue; }

    const h = name.match(/^h([1-6])$/);
    if (h) {
      if (!closing) push('\n\n' + '#'.repeat(+h[1]) + ' '); else push('\n');
      continue;
    }
    switch (name) {
      case 'img': {
        const src = attr(body, 'src');
        const m = '<<IMG:' + (++markerSeq) + '>>';
        events.push({ n: markerSeq, via: 'img', src, marker: m });
        push(' ' + m + ' ');
        continue;
      }
      case 'v:imagedata': case 'imagedata': {
        const src = attr(body, 'href') || attr(body, 'o:href');
        const m = '<<IMG:' + (++markerSeq) + '>>';
        events.push({ n: markerSeq, via: 'imagedata', src, marker: m });
        push(' ' + m + ' ');
        continue;
      }
      case 'br': push(cellDepth ? ' ' : '\n'); continue;
      case 'p': case 'div': case 'section': case 'article': case 'blockquote': case 'tr':
        if (name === 'tr') { push(cellDepth ? ' ‖ ' : '\n'); continue; }
        push(cellDepth ? ' ' : '\n'); continue;
      case 'table': push(cellDepth ? ' ' : '\n\n'); continue;
      case 'caption': push(cellDepth ? ' ' : '\n'); push('【表】'); push(' '); continue;
      case 'td': case 'th': if (closing) cellDepth--; else cellDepth++; push(' | '); continue;
      case 'li': push(' ' + (cellDepth ? '' : '\n') + '- '); continue;
      case 'ul': case 'ol': push(' '); continue;
      case 'sup': push(closing ? '}' : '^{'); continue;
      case 'sub': push(closing ? '}' : '_{'); continue;
      case 'o:p': continue;
      default:
        // ignore other tags (span/font/b/i/a/vml formatting tags...) but keep their text
        continue;
    }
  }

  let marked = out.join('');
  // normalise whitespace / blank lines
  marked = marked.replace(/[ \t]{2,}/g, ' ');
  marked = marked.replace(/( \|( \|)+)/g, ' |');
  marked = marked.replace(/\|( \|\s*)+$/gm, '');
  marked = marked.replace(/^( \|\s*)+/gm, '');
  marked = marked.replace(/[ \t]+$/gm, '');
  marked = marked.replace(/\n{3,}/g, '\n\n');
  marked = marked.split('\n').map(l => l.trim()).join('\n');
  marked = marked.replace(/\n{3,}/g, '\n\n').trim();
  return { marked, events, title };
}

function contextAt(marked, marker, span) {
  const pos = marked.indexOf(marker);
  if (pos < 0) return { before: null, after: null };
  const before = marked.slice(Math.max(0, pos - span), pos).replace(/\s+/g, ' ').trim();
  const after = marked.slice(pos + marker.length, pos + marker.length + span).replace(/\s+/g, ' ').trim();
  return { before, after };
}

function main() {
  const diskIdx = buildDiskIndex();
  fs.mkdirSync(OUT_PLAIN, { recursive: true });
  fs.mkdirSync(OUT_WORK, { recursive: true });

  const pages = fs.readdirSync(HTML_DIR).filter(n => /\.htm$/i.test(n) && !EXCLUDE.has(n));
  const report = [];

  for (const page of pages) {
    const buf = fs.readFileSync(path.join(HTML_DIR, page));
    const text = decode(buf);
    const { marked, events, title } = htmlToText(text);
    const clean = marked.replace(/<<IMG:\d+>>/g, '').replace(/[ \t]{2,}/g, ' ')
      .split('\n').map(l => l.trim()).join('\n').replace(/\n{3,}/g, '\n\n').trim();

    const header = '# ' + (title || page.replace(/\.htm$/i, '')) + '\n\n';
    const outFile = path.join(OUT_PLAIN, page.replace(/\.htm$/i, '') + '.txt');
    fs.writeFileSync(outFile, header + clean + '\n', 'utf8');

    const items = events.map(ev => {
      const resolved = resolveRef(ev.src, diskIdx);
      const ctx = contextAt(marked, ev.marker, 260);
      return {
        order: ev.n, via: ev.via, src_raw: ev.src,
        file: resolved.abs, rel: resolved.rel, ext: resolved.ext,
        bytes: resolved.bytes, missing: !!resolved.missing,
        context_before: ctx.before, context_after: ctx.after,
      };
    });

    report.push({
      page: page.replace(/\.htm$/i, ''), htm: page, title, htm_bytes: buf.length,
      plain_file: outFile, plain_chars: clean.length,
      img_events: items.length,
      unique_files: [...new Set(items.filter(x => !x.missing).map(x => x.file))].length,
      missing_files: items.filter(x => x.missing).length,
      images: items,
    });
    console.log(page.padEnd(9), 'chars=' + String(clean.length).padStart(7), 'imgEvents=' + items.length,
      'uniq=' + report[report.length - 1].unique_files, 'missing=' + report[report.length - 1].missing_files);
  }

  fs.writeFileSync(path.join(OUT_WORK, 'hb_page_images.json'), JSON.stringify(report, null, 2), 'utf8');

  // flat inventory of every distinct image file used by the non-B01/02/03 pages
  const used = new Map();
  for (const p of report) for (const im of p.images) {
    const k = (im.file || 'MISSING:' + im.rel).toLowerCase();
    if (!used.has(k)) used.set(k, { file: im.file, rel: im.rel, ext: im.ext, bytes: im.bytes, pages: new Set(), missing: im.missing });
    used.get(k).pages.add(p.page);
  }
  const flat = [...used.values()].map(v => ({ ...v, pages: [...v.pages] }));
  fs.writeFileSync(path.join(OUT_WORK, 'hb_image_inventory.json'), JSON.stringify(flat.sort((a, b) => (a.file || '').localeCompare(b.file || '')), null, 2), 'utf8');
  console.log('\ndistinct image files used:', flat.length, ' missing:', flat.filter(f => f.missing).length);
  const byExt = {};
  for (const f of flat) byExt[f.ext] = (byExt[f.ext] || 0) + 1;
  console.log('by ext:', JSON.stringify(byExt));
}
main();
