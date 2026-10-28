// Strip a GBK HTML page to readable text + link map. Node >= 20, stdlib only.
// Usage: node html_strip.js <file.htm> [<file2.htm> ...] [--links-only]
const fs = require('fs');

const dec = new TextDecoder('gb18030', { fatal: false });
const args = process.argv.slice(2);
const linksOnly = args.includes('--links-only');
const files = args.filter(a => !a.startsWith('--'));

function strip(html) {
  // drop scripts/styles/comments
  html = html.replace(/<!--[\s\S]*?-->/g, ' ')
             .replace(/<style[\s\S]*?<\/style>/gi, ' ')
             .replace(/<script[\s\S]*?<\/script>/gi, ' ')
             .replace(/<xml[\s\S]*?<\/xml>/gi, ' ')
             .replace(/<head[\s\S]*?<\/head>/gi, ' ');
  const links = [];
  // anchors
  html = html.replace(/<a\b[^>]*href\s*=\s*"?([^"\s>]+)"?[^>]*>([\s\S]*?)<\/a>/gi, (m, href, inner) => {
    const text = inner.replace(/<[^>]+>/g, '').replace(/&nbsp;/g, ' ').replace(/\s+/g, ' ').trim();
    links.push({ href, text });
    return ' ' + (text || href) + ' ';
  });
  // images -> [IMG:path]
  html = html.replace(/<img\b[^>]*src\s*=\s*"?([^"\s>]+)"?[^>]*>/gi, (m, src) => ' [IMG:' + src + '] ');
  // headings -> markdown
  html = html.replace(/<h([1-6])\b[^>]*>([\s\S]*?)<\/h\1>/gi, (m, lvl, inner) => '\n' + '#'.repeat(Number(lvl)) + ' ' + inner.replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim() + '\n');
  // table cells -> pipe rows
  html = html.replace(/<\/t[dh]>/gi, ' | ');
  html = html.replace(/<tr\b[^>]*>/gi, '\n');
  html = html.replace(/<\/tr>/gi, '');
  // block breaks
  html = html.replace(/<\/(p|div|li|br|table|tbody|thead|tr|h[1-6])>/gi, '\n');
  html = html.replace(/<br\s*\/?>/gi, '\n');
  html = html.replace(/<[^>]+>/g, ' ');
  // entities
  html = html.replace(/&nbsp;/g, ' ').replace(/&lt;/g, '<').replace(/&gt;/g, '>')
             .replace(/&quot;/g, '"').replace(/&#(\d+);/g, (m, d) => String.fromCharCode(Number(d)))
             .replace(/&amp;/g, '&');
  const lines = html.split(/\r?\n/).map(l => l.replace(/[ \t]+/g, ' ').trim()).filter(Boolean);
  return { lines, links };
}

for (const f of files) {
  const buf = fs.readFileSync(f);
  const html = dec.decode(buf);
  const title = (html.match(/<title[^>]*>([\s\S]*?)<\/title>/i) || [, ''])[1].replace(/<[^>]+>/g, '').trim();
  const { lines, links } = strip(html);
  console.log('=== ' + f + ' ===');
  console.log('title: ' + title);
  console.log('bytes: ' + buf.length + '  textLines: ' + lines.length + '  links: ' + links.length + '  images: ' + (lines.join('\n').match(/\[IMG:/g) || []).length);
  if (links.length) {
    console.log('--- links (href => text) ---');
    for (const l of links) console.log(l.href + '  =>  ' + l.text);
  }
  if (!linksOnly) {
    console.log('--- text ---');
    for (const l of lines) console.log(l);
  }
  console.log('');
}
