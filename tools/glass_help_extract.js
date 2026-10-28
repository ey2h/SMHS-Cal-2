'use strict';
// Extract plain text + image occurrences (with context) from GBK/GB18030 Word-exported HTML help pages.
// Usage: node glass_help_extract.js
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const SRC = 'C:/Program Files (x86)/\u6c47\u5b9d\u5e55\u5899\u95e8\u7a97\u7ed3\u6784\u8ba1\u7b97\u8f6f\u4ef6V35.0/html';
const OUTPLAIN = 'C:/code/Glass/docs/recon/help/plain';
const OUTMETA = 'C:/code/Glass/docs/recon/help/meta';

const PAGES = ['B01', 'B02', 'B03'];

function decodeGBK(buf) {
  return new TextDecoder('gb18030').decode(buf);
}

const NAMED = {
  nbsp: ' ', amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", ldquo: '\u201C', rdquo: '\u201D',
  lsquo: '\u2018', rsquo: '\u2019', ndash: '\u2013', mdash: '\u2014', hellip: '\u2026',
  times: '\u00D7', divide: '\u00F7', deg: '\u00B0', plusmn: '\u00B1', sup2: '\u00B2', sup3: '\u00B3',
  middot: '\u00B7', bull: '\u2022', frac12: '\u00BD', copy: '\u00A9', reg: '\u00AE', trade: '\u2122',
  ne: '\u2260', le: '\u2264', ge: '\u2265', infin: '\u221E', micro: '\u00B5', sup1: '\u00B9',
};
function decodeEntities(s) {
  return s.replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(parseInt(d, 10)))
          .replace(/&#x([0-9a-fA-F]+);/g, (_, d) => String.fromCodePoint(parseInt(d, 16)))
          .replace(/&([a-zA-Z]+);/g, (m, n) => NAMED[n] !== undefined ? NAMED[n] : m);
}

// Tags that end a block (emit newline), table cell separators, list bullets.
const BLOCK_END = new Set(['p','div','br','li','ul','ol','dl','dt','dd','tr','table','h1','h2','h3','h4','h5','h6','hr','body','html','section','blockquote','pre','address','fieldset','form','center','object','applet','iframe','o:p','v:line','v:group','v:shape','v:roundrect','v:rect','v:oval','v:line3d','table:table','table:table-row','table:table-cell','w:p','w:tbl','font2']);
const CELL_END = new Set(['td','th','table:table-cell']);
const HEADINGS = new Set(['h1','h2','h3','h4','h5','h6','h7','h8','h9']);

function tagInfo(tag) {
  // tag like '<img src="...">' or '</p>' or '<p class=x>'
  const m = /^<\/?\s*([a-zA-Z][a-zA-Z0-9:_\-]*)/.exec(tag);
  return m ? m[1].toLowerCase() : null;
}

function extractPage(page) {
  const file = path.join(SRC, page + '.htm');
  const buf = fs.readFileSync(file);
  const html = decodeGBK(buf);

  const images = [];          // occurrences in document order
  const out = [];             // text chunks
  let i = 0;
  const len = html.length;
  let pendingNewlines = 0;
  let pendingSpace = false;

  function pushText(t) {
    if (!t) return;
    if (pendingNewlines) { out.push('\n'.repeat(Math.min(pendingNewlines, 2))); pendingNewlines = 0; pendingSpace = false; }
    out.push(t);
  }

  while (i < len) {
    const lt = html.indexOf('<', i);
    if (lt < 0) {
      pushText(decodeEntities(html.slice(i)));
      break;
    }
    const rawText = html.slice(i, lt);
    if (rawText) {
      let t = decodeEntities(rawText);
      t = t.replace(/\s+/g, ' ');
      if (t) {
        if (pendingNewlines) { out.push('\n'.repeat(Math.min(pendingNewlines, 2))); pendingNewlines = 0; pendingSpace = false; }
        if (pendingSpace && !/\s$/.test(out.length ? out[out.length - 1] : '')) out.push(' ');
        out.push(t);
        pendingSpace = true;
      }
    }
    i = lt;
    if (html.startsWith('<!--', i)) {
      const end = html.indexOf('-->', i);
      const stop = end < 0 ? len : end + 3;
      const comment = html.slice(i, stop);
      // image refs may live inside Word conditional comments -> record them, do not emit text
      const re = /<(img|v:imagedata|v:image|imagedata)\b([^>]*)\/?>/gi;
      let mm;
      while ((mm = re.exec(comment)) !== null) {
        const srcm = /src\s*=\s*("([^"]*)"|'([^']*)'|([^\s>]+))/i.exec(mm[2]);
        if (srcm) images.push({ raw: srcm[2] || srcm[3] || srcm[4] || '', inComment: true, offset: i + mm.index });
      }
      i = stop;
      continue;
    }
    // find end of tag
    let gt = html.indexOf('>', i);
    if (gt < 0) { i = len; continue; }
    let tag = html.slice(i, gt + 1);
    const name = tagInfo(tag);
    if (name === 'script' || name === 'style') {
      const close = new RegExp('</\\s*' + name + '\\s*>', 'i');
      const m = close.exec(html.slice(gt + 1));
      i = m ? gt + 1 + m.index + m[0].length : len;
      continue;
    }
    if (name === 'img' || name === 'v:imagedata' || name === 'v:image') {
      const srcm = /src\s*=\s*("([^"]*)"|'([^']*)'|([^\s>]+))/i.exec(tag);
      const raw = srcm ? (srcm[2] || srcm[3] || srcm[4] || '') : '';
      images.push({ raw, inComment: false, offset: i, tag });
      pushText(' \u27e6IMG:' + raw + '\u27e7 ');
      pendingSpace = false;
      i = gt + 1;
      continue;
    }
    if (name && CELL_END.has(name)) { out.push(' | '); pendingSpace = false; i = gt + 1; continue; }
    if (name && BLOCK_END.has(name)) { pendingNewlines += (tag.startsWith('</') ? 1 : (name === 'br' ? 1 : 2)); pendingSpace = false; i = gt + 1; continue; }
    if (name && HEADINGS.has(name) && !tag.startsWith('</')) { pendingNewlines += 2; pendingSpace = false; i = gt + 1; continue; }
    if (name === 'li' && !tag.startsWith('</')) { pushText('\n\u2022 '); pendingSpace = false; i = gt + 1; continue; }
    i = gt + 1;
  }

  let text = out.join('');
  text = text.replace(/\u00a0/g, ' ');
  text = text.replace(/[ \t]+/g, ' ');
  text = text.replace(/ ?\u27e6IMG:([^\u27e7]+)\u27e7 ?/g, '\u27e6IMG:$1\u27e7');
  text = text.replace(/\| \| \|/g, '|');
  text = text.replace(/\n{3,}/g, '\n\n');
  text = text.split('\n').map(l => l.replace(/\s+$/,'').replace(/^\s+/,'')).join('\n');
  text = text.replace(/\n{3,}/g, '\n\n');
  text = text.replace(/^\n+/, '');

  // Context around each image occurrence in the produced text
  const markerRe = /\u27e6IMG:([^\u27e7]*)\u27e7/g;
  let occIndex = 0;
  const occurrences = [];
  let m;
  while ((m = markerRe.exec(text)) !== null) {
    const before = text.slice(Math.max(0, m.index - 600), m.index);
    const after = text.slice(m.index + m[0].length, m.index + m[0].length + 600);
    const bQuote = tailSentence(before);
    const aQuote = headSentence(after);
    occurrences.push({
      occurrence: occIndex++,
      raw: m[1],
      quote_before: bQuote,
      quote_after: aQuote,
      heading_context: nearestHeading(text.slice(0, m.index)),
    });
  }

  // Resolve to disk files
  const imgDir = path.join(SRC, page + '.files');
  for (const o of occurrences) {
    o.files_candidates = resolveRaw(o.raw, SRC, page);
  }

  // all files physically present in the .files dir
  const diskFiles = [];
  const dirs = [path.join(SRC, page + '.files'), path.join(SRC, page)];
  for (const d of dirs) {
    if (fs.existsSync(d) && fs.statSync(d).isDirectory()) {
      for (const f of fs.readdirSync(d)) {
        const full = path.join(d, f);
        let st;
        try { st = fs.statSync(full); } catch (e) { continue; }
        if (!st.isFile()) continue;
        const ext = path.extname(f).slice(1).toLowerCase();
        const isImage = ['jpg','jpeg','gif','png','bmp','wmf','wmz','emf','tif','tiff','svg'].includes(ext);
        if (!isImage && f !== 'filelist.xml' && !isImage) continue;
        if (!isImage) continue;
        const buf2 = fs.readFileSync(full);
        diskFiles.push({
          name: f, path: full, ext, bytes: st.size,
          sha256: crypto.createHash('sha256').update(buf2).digest('hex'),
          referenced: occurrences.some(o => o.files_candidates.some(c => c.path === full)),
          referenced_count: occurrences.filter(o => o.files_candidates.some(c => c.path === full)).length,
        });
      }
    }
  }

  return { page, sourceBytes: buf.length, htmlChars: html.length, text, occurrences, diskFiles, imgDir };
}

function cleanStream(s) {
  return s.replace(/\u27e6IMG:[^\u27e7]+\u27e7/g, ' ')
          .replace(/\|/g, ' ')
          .replace(/\s+/g, ' ')
          .trim();
}
function tailSentence(s) {
  // strip image markers and table pipes FIRST, then take the trailing sentence(s)
  let t = cleanStream(s);
  const bounds = ['\u3002', '\uFF1B', '\uFF1A', '\uFF01', '\uFF1F', '\u201D'];
  let cut = -1;
  for (const b of bounds) { const k = t.lastIndexOf(b); if (k > cut) cut = k; }
  if (cut >= 0 && t.length - cut - 1 < 8) {
    const sub = t.slice(0, cut);
    let prev = -1;
    for (const b of bounds) { const k = sub.lastIndexOf(b); if (k > prev) prev = k; }
    if (prev >= 0) t = t.slice(prev + 1);
  } else if (cut >= 0) {
    t = t.slice(cut + 1);
  }
  if (t.length > 260) t = t.slice(-260);
  return t.trim();
}
function headSentence(s) {
  let t = cleanStream(s);
  const bounds = ['\u3002', '\uFF1B', '\uFF1A', '\uFF01', '\uFF1F'];
  let cut = -1;
  for (const b of bounds) { const k = t.indexOf(b); if (k >= 0 && (cut < 0 || k < cut)) cut = k; }
  if (cut >= 0 && cut < 12) {
    const rest = t.slice(cut + 1);
    let next = -1;
    for (const b of bounds) { const k = rest.indexOf(b); if (k >= 0 && (next < 0 || k < next)) next = k; }
    if (next >= 0) t = t.slice(0, cut + 1 + next + 1);
  } else if (cut >= 0) {
    t = t.slice(0, cut + 1);
  }
  if (t.length > 260) t = t.slice(0, 260);
  return t.trim();
}
function nearestHeading(s) {
  const lines = s.split('\n');
  for (let k = lines.length - 1; k >= 0; k--) {
    const l = lines[k].trim();
    if (l && l.length <= 80 && /IMG:/.test(l) === false) {
      if (/^\d+(\.\d+)*\s/.test(l) || /^[\u4e00-\u9fff]{2,20}$/.test(l) || /^[A-Z]\d?[\d.]*\s/.test(l)) return l;
    }
  }
  return null;
}
function resolveRaw(raw, src, page) {
  if (!raw) return [];
  const cands = [];
  const clean = raw.replace(/\\/g, '/').replace(/^\.\//, '');
  const tries = [];
  if (/^[a-zA-Z]:[\/\\]/.test(raw)) tries.push(raw);
  tries.push(path.join(src, clean));
  tries.push(path.join(src, page + '.files', path.basename(clean)));
  const seen = new Set();
  for (const t of tries) {
    const norm = path.normalize(t);
    if (seen.has(norm.toLowerCase())) continue;
    seen.add(norm.toLowerCase());
    try {
      const st = fs.statSync(norm);
      if (st.isFile()) cands.push({ path: norm, bytes: st.size });
    } catch (e) { /* not found */ }
  }
  return cands;
}

fs.mkdirSync(OUTPLAIN, { recursive: true });
fs.mkdirSync(OUTMETA, { recursive: true });

const summary = [];
for (const p of PAGES) {
  const r = extractPage(p);
  fs.writeFileSync(path.join(OUTPLAIN, p + '.txt'), r.text, 'utf8');
  fs.writeFileSync(path.join(OUTMETA, p + '.images.json'), JSON.stringify({
    page: p, sourceHtml: path.join(SRC, p + '.htm'), sourceBytes: r.sourceBytes,
    textFile: path.join(OUTPLAIN, p + '.txt'), textChars: r.text.length,
    occurrences: r.occurrences, diskFiles: r.diskFiles,
  }, null, 2), 'utf8');
  const uniqFiles = new Set(r.occurrences.flatMap(o => o.files_candidates.map(c => c.path.toLowerCase())));
  const uniqNames = new Set(r.occurrences.map(o => o.raw));
  summary.push({
    page: p, sourceBytes: r.sourceBytes, textChars: r.text.length, textLines: r.text.split('\n').length,
    occurrences: r.occurrences.length, uniqueReferencedFiles: uniqFiles.size, uniqueRawRefs: uniqNames.size,
    diskImageFiles: r.diskFiles.length,
    unreferencedDiskImages: r.diskFiles.filter(d => !d.referenced).map(d => d.name),
  });
  console.log(JSON.stringify(summary[summary.length - 1], null, 2));
}
console.log('DONE');
