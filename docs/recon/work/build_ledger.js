'use strict';
// Build calcbook_formula_ledger.csv from the per-line JSONL dumps produced by extract_calcbooks.ps1
// Rules: no invented content. Every emitted field is a literal substring of the exported original line.

const fs = require('fs');
const path = require('path');

const LINES_DIR = 'C:\\code\\Glass\\docs\\recon\\work\\lines';
const OUT_CSV   = 'C:\\code\\Glass\\docs\\recon\\reports\\calcbook_formula_ledger.csv';
const STATS_OUT = 'C:\\code\\Glass\\docs\\recon\\work\\ledger_stats.json';

const CLAUSE_RES = [
  // 4.6.1[GB55001-2021]  / 5.1.2 [GB 50009-2012]
  /\d+(?:\.\d+){1,5}\s*\[[^\[\]\n]{1,80}\]/g,
  // ……4.6.1  (dots filler then clause number, no bracket)
  /…{1,}[ ]*\d+(?:\.\d+){1,5}/g,
  // [GB 50017-2017] [JGJ 102-2003] [...] as standalone bracket
  /\[[^\[\]\n]{1,80}\]/g,
  // 《建筑结构荷载规范》
  /《[^》\n]{2,80}》/g,
  // 第4.6.1条 / 第 5.1.2 款
  /第\s*\d+(?:\.\d+){1,5}\s*[条款项]/g,
  // bare standard codes
  /(?:GB|GB\s*\/\s*T|JGJ|JGJ\s*\/\s*T|DB\d{2}|YB|CECS|EN|ISO|AS|AISC|BS|DIN|ASTM|ASCE)\s*\/?\s*\d{4,5}(?:[-—]\d{2,4})?/g,
  // 附录A / Appendix C
  /附录\s*[A-Z一二三四五六七八九十]/g,
];

const EQ_SPLIT = /(?<![<>≤≥≈≠<>+\-\/])(?<!<>)(?:=)(?![=<>])/;
const PURE_NUMERIC = /^[\d\s.,。+\-*/×·÷()（）\[\]{}%²³~]+$/;

function findClauses(line) {
  const spans = [];
  for (const re of CLAUSE_RES) {
    re.lastIndex = 0;
    let m;
    while ((m = re.exec(line)) !== null) {
      if (m[0].length === 0) { re.lastIndex++; continue; }
      spans.push({ start: m.index, end: m.index + m[0].length, text: m[0].trim() });
    }
  }
  // keep outermost / longest, drop overlaps (a bracket already covered by "4.6.1[GB...]")
  spans.sort((a, b) => a.start - b.start || (b.end - b.start) - (a.end - a.start));
  const kept = [];
  let lastEnd = -1;
  for (const s of spans) {
    if (s.start >= lastEnd) { kept.push(s); lastEnd = s.end; }
  }
  // trailing filler text after the final "……" that is short and holds a digit
  const dotsIdx = line.lastIndexOf('……');
  if (dotsIdx >= 0) {
    const tail = line.slice(dotsIdx).replace(/^…+/, '').trim();
    if (tail && tail.length <= 45 && /\d/.test(tail)) {
      const already = kept.some(k => k.text && (tail.includes(k.text) || k.text.includes(tail)));
      if (!already) {
        const overlap = kept.some(k => k.start <= dotsIdx && k.end > dotsIdx);
        if (!overlap) kept.push({ start: dotsIdx, end: line.length, text: tail });
      }
    }
  }
  kept.sort((a, b) => a.start - b.start);
  return kept;
}

function stripClauses(line, clauses) {
  if (!clauses.length) return line;
  let out = '';
  let pos = 0;
  for (const c of clauses) { out += line.slice(pos, c.start); pos = c.end; }
  out += line.slice(pos);
  out = out.replace(/…{1,}/g, ' ');
  return out.replace(/\s{2,}/g, ' ').trim();
}

function csvCell(v) {
  const s = (v === null || v === undefined) ? '' : String(v);
  return '"' + s.replace(/"/g, '""') + '"';
}

function isCandidate(text) {
  if (!text) return false;
  if (text.trim().length < 3) return false;
  if (EQ_SPLIT.test(text)) return true;
  if (/…{1,}/.test(text)) return true;
  for (const re of CLAUSE_RES) { re.lastIndex = 0; if (re.test(text)) return true; }
  // inequality-only judgement lines: sigma <= 84.0 MPa  etc.
  if (/[≤≥]/.test(text) && /\d/.test(text) && /(N\/mm|MPa|kN|m\/s°|°|mm|N\/m)/.test(text)) return true;
  return false;
}

const files = fs.readdirSync(LINES_DIR).filter(f => f.endsWith('.lines.jsonl')).sort();
const rows = [];
const bookStats = [];

for (const f of files) {
  const book = f.replace(/\.lines\.jsonl$/, '');
  const txt = fs.readFileSync(path.join(LINES_DIR, f), 'utf8');
  let n = 0, cand = 0, noClause = 0, missingFormula = 0, missingResult = 0, tableRows = 0, inequalities = 0;
  for (const line of txt.split(/\r?\n/)) {
    if (!line.trim()) continue;
    let rec;
    try { rec = JSON.parse(line); } catch (e) { continue; }
    n++;
    const text = rec.text || '';
    if (!isCandidate(text)) continue;
    cand++;
    const clauses = findClauses(text);
    const math = stripClauses(text, clauses);
    const flags = [];
    if (rec.table && rec.table > 0) { flags.push('table_cell'); tableRows++; }

    const hasEq = EQ_SPLIT.test(math);
    const hasIneq = /[≤≥]/.test(math) && !hasEq;
    let formula = '', subst = '', result = '';

    if (hasEq) {
      const parts = math.split(EQ_SPLIT).map(s => s.trim());
      if (parts.length === 1) {
        result = math;
        flags.push('missing_formula'); missingFormula++;
      } else if (parts.length === 2) {
        if (PURE_NUMERIC.test(parts[0])) {
          formula = ''; subst = parts[0]; result = parts[1];
          flags.push('missing_formula'); missingFormula++;
        } else {
          formula = parts[0]; subst = ''; result = parts[1];
        }
      } else {
        const last = parts[parts.length - 1];
        const mid = parts.slice(1, parts.length - 1);
        if (PURE_NUMERIC.test(parts[0])) {
          formula = ''; subst = [parts[0], ...mid].join(' = '); result = last;
          flags.push('missing_formula'); missingFormula++;
        } else {
          formula = parts[0]; subst = mid.join(' = '); result = last;
        }
      }
      if (!result) { flags.push('missing_result'); missingResult++; }
    } else if (hasIneq) {
      inequalities++;
      const m = math.split(/([≤≥])/);
      formula = (m[0] || '').trim();
      result = ((m[1] || '') + ' ' + (m[2] || '')).trim();
      subst = '';
      flags.push('inequality_only');
      if (!formula) { flags.push('missing_formula'); missingFormula++; }
    } else {
      // clause line with no expression at all
      result = math; formula = ''; subst = '';
      flags.push('missing_formula'); missingFormula++;
    }

    if (!clauses.length) { flags.push('no_clause_reference'); noClause++; }
    if (clauses.length > 1) flags.push('multi_clause');

    rows.push([
      book,
      rec.section || '',
      formula,
      clauses.map(c => c.text).join(' | '),
      subst,
      result,
      text,
      flags.join(';')
    ]);
  }
  bookStats.push({ book, jsonlLines: n, ledgerRows: cand, noClauseReference: noClause, missingFormula, missingResult, tableCellRows: tableRows, inequalityRows: inequalities });
}

const header = ['book', 'section', 'formula_or_symbol', 'source_clause', 'substitution', 'result', 'raw_line', 'flags'];
const csv = [header.join(',')].concat(rows.map(r => r.map(csvCell).join(','))).join('\n') + '\n';
fs.mkdirSync(path.dirname(OUT_CSV), { recursive: true });
fs.writeFileSync(OUT_CSV, csv, 'utf8');
fs.writeFileSync(STATS_OUT, JSON.stringify({ totalRows: rows.length, perBook: bookStats }, null, 2), 'utf8');
console.log(JSON.stringify({ totalRows: rows.length, perBook: bookStats }, null, 2));
