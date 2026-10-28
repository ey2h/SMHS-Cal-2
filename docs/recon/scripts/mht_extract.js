'use strict';
/*
 * mht_extract.js
 * Parse .mht (MIME single-part / multipart-related) and exported .htm documents:
 *   - decode quoted-printable / base64 part bodies
 *   - charset: declared charset, else UTF-8 sniff, else GB18030
 *   - convert HTML to plain text (original Chinese preserved)
 *   - extract <table> structures to CSV (grid with colspan expansion, rowspan marked)
 * Read-only on the source tree. Outputs under docs\recon\specs and docs\recon\specs\tables.
 */
const fs = require('fs');
const path = require('path');

const BASE = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0';
const TARGETS = [
  { group: '节能', dir: BASE + '\\规范', filter: (n) => /^节能\d+\.mht$/i.test(n) },
  { group: '物理性能', dir: BASE + '\\规范\\物理性能', filter: (n) => /\.mht$/i.test(n) },
  { group: '热工规范表', dir: BASE + '\\pic\\热工\\规范', filter: (n) => /\.mht$/i.test(n) },
  { group: '荷载规范HTM', dir: BASE + '\\规范\\荷载规范', filter: (n) => /^00\d\.htm$/i.test(n) },
];

const SPEC_DIR = 'C:\\code\\Glass\\docs\\recon\\specs';
const TABLE_DIR = SPEC_DIR + '\\tables';
const OUT_STATS = 'C:\\code\\Glass\\docs\\recon\\reports\\mht_html_stats.json';

fs.mkdirSync(TABLE_DIR, { recursive: true });
fs.mkdirSync(SPEC_DIR, { recursive: true });

/* ---------------- MIME ---------------- */
function parseMime(buf) {
  const head = buf.subarray(0, 4096).toString('latin1');
  const m = /Content-Type:\s*multipart\/[a-z]+;\s*boundary="?([^"\r\n;]+)"?/i.exec(head);
  if (!m) {
    // single part: split headers/body at first blank line
    const idx = buf.indexOf('\r\n\r\n');
    const idx2 = buf.indexOf('\n\n');
    const cut = idx >= 0 ? idx : idx2;
    if (cut < 0) return { multipart: false, headers: '', body: buf };
    const nl = idx >= 0 ? 4 : 2;
    return { multipart: false, headers: buf.subarray(0, cut).toString('latin1'), body: buf.subarray(cut + nl) };
  }
  const boundary = m[1];
  const text = buf.toString('latin1');
  const delim = '--' + boundary;
  const esc = delim.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const pieces = text.split(new RegExp('\\r?\\n' + esc));
  const parts = [];
  // preamble before first boundary is the IE "Web archive" notice -> dropped
  for (let i = 1; i < pieces.length; i++) {
    let chunk = pieces[i];
    if (/^--\s*\r?\n?$/.test(chunk)) continue; // closing boundary
    chunk = chunk.replace(/^[^\S\r\n]+/, '');
    const hEnd = chunk.search(/\r\n\r|\n\n/);
    let headers = '';
    let bodyStart = -1;
    const sepCRLF = chunk.indexOf('\r\n\r\n');
    const sepLF = chunk.indexOf('\n\n');
    if (sepCRLF >= 0) {
      headers = chunk.slice(0, sepCRLF);
      bodyStart = sepCRLF + 4;
    } else if (sepLF >= 0) {
      headers = chunk.slice(0, sepLF);
      bodyStart = sepLF + 2;
    } else {
      headers = chunk.slice(0, 400);
    }
    if (bodyStart < 0) continue;
    let bodyStr = chunk.slice(bodyStart);
    bodyStr = bodyStr.replace(/(\r?\n)+--[^\r\n"]*--\s*$/, '');
    const body = Buffer.from(bodyStr, 'latin1');
    parts.push({ headers, body });
  }
  return { multipart: true, boundary, parts, preamble: pieces[0] };
}

function qpDecode(buf) {
  const out = [];
  let i = 0;
  while (i < buf.length) {
    const c = buf[i];
    if (c === 0x3d /* = */) {
      if (i + 1 < buf.length && (buf[i + 1] === 0x0a || (buf[i + 1] === 0x0d && buf[i + 2] === 0x0a))) {
        i += buf[i + 1] === 0x0d ? 3 : 2; // soft line break
        continue;
      }
      if (i + 2 < buf.length) {
        const hex = buf.subarray(i + 1, i + 3).toString('latin1');
        if (/^[0-9a-fA-F]{2}$/.test(hex)) {
          out.push(parseInt(hex, 16));
          i += 3;
          continue;
        }
      }
      out.push(c);
      i++;
      continue;
    }
    out.push(c);
    i++;
  }
  return Buffer.from(out);
}

function validUtf8(buf) {
  try {
    new TextDecoder('utf-8', { fatal: true }).decode(buf);
    return true;
  } catch (e) {
    return false;
  }
}

function decodePart(part) {
  const h = part.headers;
  const te = (/Content-Transfer-Encoding:\s*([a-z0-9-]+)/i.exec(h) || [])[1] || '7bit';
  const declared = (/Content-Type:\s*text\/[^;]*;\s*charset="?([^";\r\n]+)"?/i.exec(h) || [])[1] || '';
  const metaDeclared = (/charset=3D"?([a-z0-9-]+)"?/i.exec(h) || [])[1] || '';
  let bytes = part.body;
  let transfer = te.toLowerCase();
  if (transfer === 'quoted-printable') bytes = qpDecode(bytes);
  else if (transfer === 'base64') bytes = Buffer.from(bytes.toString('latin1').replace(/\s+/g, ''), 'base64');
  else bytes = Buffer.from(bytes.toString('latin1'), 'binary');

  let enc = null;
  const decl = (declared || metaDeclared || '').toLowerCase();
  if (decl && validUtf8(bytes) && /utf-?8/i.test(decl)) enc = 'utf-8';
  else if (validUtf8(bytes) && /[^\x00-\x7f]/.test(bytes.toString('latin1'))) enc = 'utf-8';
  else if (/gb|gb2312|gbk|gb18030/i.test(decl)) enc = 'gb18030';
  else if (validUtf8(bytes)) enc = 'utf-8';
  else enc = 'gb18030';

  const text = new TextDecoder(enc).decode(bytes);
  return { text, transfer, enc, declared };
}

/* ---------------- entities / text ---------------- */
const NAMED = {
  nbsp: ' ', amp: '&', lt: '<', gt: '>', quot: '"', apos: "'",
  ldquo: '\u201c', rdquo: '\u201d', lsquo: '\u2018', rsquo: '\u2019',
  mdash: '\u2014', ndash: '\u2013', hellip: '\u2026', deg: '\u00b0',
  times: '\u00d7', divide: '\u00f7', plusmn: '\u00b1', sup: '\u2081',
  ensp: '\u2002', emsp: '\u2003', middot: '\u00b7', bull: '\u2022',
  yen: '\u00a5', copy: '\u00a9', reg: '\u00ae', trade: '\u2122',
  laquo: '\u00ab', raquo: '\u00bb', sect: '\u00a7', para: '\u00b6',
  micro: '\u00b5', frac12: '\u00bd', frac14: '\u00bc', frac34: '\u00be',
  sup2: '\u00b2', sup3: '\u00b3', superscript: '\u2074', line: '\u2121',
};
function decodeEntities(s) {
  return s
    .replace(/&#x([0-9a-fA-F]+);?/g, (m, h) => {
      const n = parseInt(h, 16);
      return n > 0 && n < 0x110000 ? String.fromCodePoint(n) : m;
    })
    .replace(/&#(\d+);?/g, (m, d) => {
      const n = parseInt(d, 10);
      return n > 0 && n < 0x110000 ? String.fromCodePoint(n) : m;
    })
    .replace(/&([a-zA-Z][a-zA-Z0-9]*);?/g, (m, name) => {
      const k = name.toLowerCase();
      return Object.prototype.hasOwnProperty.call(NAMED, k) ? NAMED[k] : m;
    });
}

function cleanText(s) {
  return decodeEntities(s)
    .replace(/\u00a0/g, ' ')
    .replace(/[\t ]+/g, ' ')
    .replace(/\u200b|\ufeff/g, '');
}

function stripNonContent(html) {
  let h = html;
  h = h.replace(/<!\[[^\]]*\]>/g, ''); // Word conditional markup like <![if !supportLists]> / <![endif]>
  h = h.replace(/<!--[\s\S]*?-->/g, ''); // removes @page style comments and mso conditionals
  h = h.replace(/<style[\s\S]*?<\/style>/gi, '\n');
  h = h.replace(/<script[\s\S]*?<\/script>/gi, '\n');
  h = h.replace(/<xml[\s\S]*?<\/xml>/gi, '');
  h = h.replace(/<!\[CDATA\[[\s\S]*?\]\]>/g, '');
  h = h.replace(/<\/?[a-z:]+:?(xml|o:documentproperties|o:customdocumentproperties)[^>]*>/gi, '');
  return h;
}

/* ---------------- table structure ---------------- */
function tokenize(html) {
  const re = /<\/?([a-zA-Z][a-zA-Z0-9:-]*)\b([^>]*)>|([^<]+)/g;
  const out = [];
  let m;
  while ((m = re.exec(html))) {
    if (m[3] !== undefined) out.push({ t: 'text', v: m[3] });
    else {
      const full = m[0];
      out.push({
        t: 'tag',
        name: m[1].toLowerCase(),
        closing: full[1] === '/',
        selfClose: />\/$/.test(full),
        attrs: m[2] || '',
      });
    }
  }
  return out;
}

function flattenNested(tokens) {
  // collect text from nested-table tokens (excluding nested-nested table markup structure)
  const parts = [];
  let depth = 0;
  for (const tk of tokens) {
    if (tk.t === 'tag') {
      if (tk.name === 'table') depth += tk.closing ? -1 : 1;
      if (tk.name === 'br') parts.push('\n');
    } else parts.push(tk.v);
  }
  return cleanText(parts.join('')).replace(/\s*\n\s*/g, ' / ').trim();
}

function attrNum(attrs, name) {
  const m = new RegExp(name + '\\s*=\\s*"?(\\d+)"?', 'i').exec(attrs);
  return m ? parseInt(m[1], 10) : 1;
}

function parseTableFromTokens(tokens) {
  // tokens start right after <table ...> at depth 1
  const rows = [];
  let curRow = null;
  let cell = null;
  let tableDepth = 1; // tokens are the INSIDE of one <table>; its closing tag is included
  let i = 0;
  const flushCell = () => {
    if (cell) {
      const text = cleanText(cell.parts.join(''))
        .replace(/\s*\n\s*/g, '\u21b5')
        .replace(/[\u21b5]{2,}/g, '\u21b5')
        .replace(/^[\u21b5]+/, '')
        .replace(/[\u21b5]+$/, '')
        .trim();
      curRow && curRow.push({ text, colspan: cell.colspan, rowspan: cell.rowspan, header: cell.header, nested: cell.nestedTables });
      cell = null;
    }
  };
  const flushRow = () => {
    flushCell();
    if (curRow && curRow.length) rows.push(curRow);
    curRow = null;
  };
  for (; i < tokens.length; i++) {
    const tk = tokens[i];
    if (tk.t === 'text') {
      if (cell) cell.parts.push(tk.v);
      continue;
    }
    if (tk.name === 'table') {
      if (!tk.closing) {
        tableDepth++;
        if (tableDepth === 2) {
          // nested table: consume through its matching close, flatten its text into the current cell
          let d = 1;
          const nested = [];
          while (i + 1 < tokens.length && d > 0) {
            i++;
            const t2 = tokens[i];
            if (t2.t === 'tag' && t2.name === 'table') {
              d += t2.closing ? -1 : 1;
              if (d === 0) break;
            }
            nested.push(t2);
          }
          if (cell) {
            cell.parts.push(flattenNested(nested));
            cell.nestedTables++;
          }
          tableDepth = 1;
        }
        continue;
      } else {
        tableDepth--;
        if (tableDepth === 0) break; // closing tag of the table currently being parsed
        continue;
      }
    }
    if (tableDepth !== 1) continue;
    if (tk.name === 'tr') {
      if (!tk.closing) flushRow();
      else flushRow();
      if (!tk.closing) curRow = [];
      continue;
    }
    if (tk.name === 'td' || tk.name === 'th') {
      if (tk.closing) flushCell();
      else {
        flushCell();
        curRow = curRow || [];
        cell = {
          parts: [],
          colspan: attrNum(tk.attrs, 'colspan'),
          rowspan: attrNum(tk.attrs, 'rowspan'),
          header: tk.name === 'th',
          nestedTables: 0,
        };
        if (tk.selfClose) flushCell();
      }
      continue;
    }
    if (tk.name === 'br') {
      if (cell) cell.parts.push('\n');
      continue;
    }
    // other tags: ignore structure (text nodes already collected)
  }
  flushRow();
  return rows;
}

function findTopTables(html) {
  const tokens = tokenize(html);
  const tables = [];
  const kept = []; // tokens outside tables
  let i = 0;
  while (i < tokens.length) {
    const tk = tokens[i];
    if (tk.t === 'tag' && tk.name === 'table' && !tk.closing) {
      let d = 1;
      const inner = [];
      while (i + 1 < tokens.length && d > 0) {
        i++;
        const t2 = tokens[i];
        if (t2.t === 'tag' && t2.name === 'table') {
          d += t2.closing ? -1 : 1;
          if (d === 0) {
            inner.push(t2);
            break;
          }
        }
        inner.push(t2);
      }
      tables.push(parseTableFromTokens(inner));
    } else {
      kept.push(tk);
    }
    i++;
  }
  return { tables, kept };
}

function tokensToText(tokens) {
  const BLOCK = new Set(['p', 'div', 'br', 'li', 'tr', 'table', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'pre', 'hr', 'blockquote', 'dl', 'dt', 'dd', 'center', 'section', 'body', 'html', 'title', 'o:p']);
  const out = [];
  for (const tk of tokens) {
    if (tk.t === 'text') out.push(tk.v);
    else {
      if (tk.name === 'br') out.push('\n');
      else if (BLOCK.has(tk.name)) out.push('\n');
    }
  }
  return decodeEntities(out.join(''))
    .replace(/\u00a0/g, ' ')
    .replace(/[\t ]+\n/g, '\n')
    .replace(/\n[ \t]+/g, '\n')
    .replace(/[ \t]{2,}/g, ' ')
    .replace(/\n{3,}/g, '\n\n')
    .split('\n')
    .map((l) => l.trim())
    .join('\n');
}

function buildGrid(rows) {
  const grid = [];
  const pending = new Map(); // col -> {remaining, text}
  const notes = [];
  for (const row of rows) {
    let cells = [];
    let col = 0;
    const place = (text) => {
      cells[col] = text;
      col++;
    };
    // first consume any carried rowspan continuations
    for (const [c, info] of pending) {
      if (info.remaining > 0 && c < col) {
        /* handled below */
      }
    }
    // walk row cells, interleaving pending carries
    for (const cell of row) {
      while (true) {
        const carry = pending.get(col);
        if (carry && carry.remaining > 0) {
          place(carry.text + '\u21b7'); // continuation marker for merged cell from above
          carry.remaining -= 1;
          if (carry.remaining <= 0) pending.delete(col);
          continue;
        }
        break;
      }
      const cs = Math.max(1, cell.colspan || 1);
      for (let k = 0; k < cs; k++) {
        place(cell.text);
        if (cs > 1) notes.push('colspan:' + cs);
      }
      if ((cell.rowspan || 1) > 1) pending.set(col - cs, { remaining: cell.rowspan - 1, text: cell.text });
    }
    // trailing carries beyond the row's own cells
    for (const [c, info] of Array.from(pending.entries())) {
      if (info.remaining > 0 && c >= col) {
        while (col < c) place('');
        place(info.text + '\u21b7');
        info.remaining -= 1;
        if (info.remaining <= 0) pending.delete(c);
      }
    }
    grid.push(cells);
  }
  const width = Math.max(0, ...grid.map((r) => r.length));
  for (const r of grid) while (r.length < width) r.push('');
  return { grid, width, height: grid.length, mergeNotes: notes.length };
}

function csvWrite(file, grid) {
  const esc = (v) => {
    const s = String(v === undefined || v === null ? '' : v).replace(/\u21b5/g, ' / ');
    return /[",\r\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s;
  };
  const body = grid.map((r) => r.map(esc).join(',')).join('\r\n');
  fs.writeFileSync(file, '\ufeff' + body + '\r\n', 'utf8');
}

/* ---------------- main ---------------- */
const report = { generatedAt: new Date().toISOString(), files: [] };

for (const target of TARGETS) {
  if (!fs.existsSync(target.dir)) {
    report.files.push({ group: target.group, dir: target.dir, error: 'dir missing' });
    continue;
  }
  const names = fs.readdirSync(target.dir).filter(target.filter).sort((a, b) => a.localeCompare(b, 'zh-Hans-CN'));
  for (const name of names) {
    const full = path.join(target.dir, name);
    const buf = fs.readFileSync(full);
    const mime = parseMime(buf);
    const htmlParts = [];
    const otherParts = [];
    if (mime.multipart) {
      for (const p of mime.parts) {
        if (/Content-Type:\s*text\/html/i.test(p.headers)) htmlParts.push(decodePart(p));
        else {
          const ct = (p.headers.match(/Content-Type:\s*([^;\r\n]+)/i) || [])[1] || 'unknown';
          otherParts.push({ contentType: ct, bytes: p.body.length });
        }
      }
    } else {
      htmlParts.push(decodePart({ headers: mime.headers, body: mime.body }));
    }

    const base = path.basename(name).replace(/\.(mht|htm|html)$/i, '');
    const textOut = path.join(SPEC_DIR, name + '.md');
    const entry = {
      group: target.group,
      sourceFile: name,
      sourcePath: full,
      bytes: buf.length,
      multipart: mime.multipart,
      htmlParts: htmlParts.length,
      nonHtmlParts: otherParts,
      decode: htmlParts.map((h) => ({ transfer: h.transfer, usedEncoding: h.enc, declaredCharset: h.declared })),
      tables: [],
    };

    const textChunks = [];
    for (const hp of htmlParts) {
      let html = hp.text;
      const title = ((/<title[^>]*>([\s\S]*?)<\/title>/i.exec(stripNonContent(html)) || [])[1] || '').trim();
      entry.title = entry.title || (title ? cleanText(title) : null);
      html = stripNonContent(html);
      // drop <head>...</head> but keep title separately
      html = html.replace(/<head[\s\S]*?<\/head>/i, '\n');
      const { tables, kept } = findTopTables(html);
      const bodyText = tokensToText(kept);
      const tableRefs = [];
      tables.forEach((rows, ti) => {
        const { grid, width, height, mergeNotes } = buildGrid(rows);
        const csvName = `${base}__t${String(ti + 1).padStart(2, '0')}.csv`;
        const csvPath = path.join(TABLE_DIR, csvName);
        csvWrite(csvPath, grid);
        const nonEmpty = grid.filter((r) => r.some((c) => String(c).trim() !== '')).length;
        const firstNonEmptyRow = (grid.find((r) => r.some((c) => String(c).trim() !== '')) || []).map((c) => String(c));
        entry.tables.push({
          csv: csvPath,
          index: ti + 1,
          htmlRows: rows.length,
          gridRows: height,
          gridCols: width,
          nonEmptyGridRows: nonEmpty,
          mergeMarkers: mergeNotes,
          firstNonEmptyRowCells: firstNonEmptyRow.slice(0, 12),
        });
        tableRefs.push(`[[表格${ti + 1}: ${rows.length}行×${width}列 -> tables\\${csvName}]]`);
      });
      textChunks.push((title ? '# ' + cleanText(title) + '\n\n' : '') + bodyText + (tableRefs.length ? '\n\n' + tableRefs.join('\n') : ''));
    }

    const header = [
      '# ' + base + '（源：' + (target.group === '热工规范表' ? 'pic\\热工\\规范\\' : target.group === '物理性能' ? '规范\\物理性能\\' : target.group === '荷载规范HTM' ? '规范\\荷载规范\\' : '规范\\') + name + '）',
      '',
      '- 源路径：' + full,
      '- 源字节数：' + buf.length,
      '- MIME：' + (mime.multipart ? 'multipart/related（boundary 分块，正文前有一段 GBK 的 “Web 档案” 提示文字，已丢弃）' : 'single-part（头部直接跟 HTML）'),
      '- HTML 部件数：' + htmlParts.length + '；非 HTML 部件（图片等，未导出）：' + JSON.stringify(otherParts),
      '- 解码：' + JSON.stringify(entry.decode),
      '- 表格：' + entry.tables.length + ' 个，CSV 见 specs\\tables\\',
      '- 说明：以下正文由 HTML 去标记后逐行保留（原文未翻译）。',
      '',
      '---',
      '',
    ].join('\n');
    fs.writeFileSync(textOut, header + textChunks.join('\n\n---\n\n') + '\n', 'utf8');
    entry.textOut = textOut;
    entry.textLines = (textChunks.join('\n').split('\n').length);
    entry.textChars = textChunks.join('\n').length;
    report.files.push(entry);
    console.log(
      `${target.group}/${name}: bytes=${buf.length} multipart=${mime.multipart} tables=${entry.tables.length} textLines=${entry.textLines} enc=${entry.decode.map((d) => d.usedEncoding).join('/')}`,
    );
  }
}

fs.writeFileSync(OUT_STATS, JSON.stringify(report, null, 2), 'utf8');
console.log('wrote ' + OUT_STATS);
