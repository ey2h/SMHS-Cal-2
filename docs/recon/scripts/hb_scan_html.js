// Scan each help page: decode gb18030, count image refs, detect base64 data URIs, list .files dirs.
const fs = require('fs');
const path = require('path');

const HTML_DIR = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\html';

function decode(buf) {
  try { return new TextDecoder('gb18030', { fatal: false }).decode(buf); }
  catch (e) { return buf.toString('utf8'); }
}

const entries = fs.readdirSync(HTML_DIR, { withFileTypes: true });
const htm = entries.filter(e => e.isFile() && /\.htm$/i.test(e.name)).map(e => e.name);
const dirs = entries.filter(e => e.isDirectory()).map(e => e.name);

console.log('pages:', htm.length, 'dirs:', dirs.length);

const out = {};
for (const name of htm) {
  const buf = fs.readFileSync(path.join(HTML_DIR, name));
  const text = decode(buf);
  const refs = [...text.matchAll(/<img[^>]*src\s*=\s*["']?([^"'\s>]+)["']?/gi)].map(m => m[1]);
  const dataUri = (text.match(/data:image/gi) || []).length;
  const objLen = (text.match(/objectlength|v:imagedata|o:href/gi) || []).length;
  out[name] = { bytes: buf.length, imgRefs: refs.length, dataUri, oleRefs: objLen, uniq: [...new Set(refs)].length };
  console.log(name.padEnd(10), 'bytes=' + String(buf.length).padStart(9), 'img=' + String(refs.length).padStart(4), 'uniq=' + String(out[name].uniq).padStart(4), 'datauri=' + dataUri, 'ole=' + objLen);
}
fs.writeFileSync('C:\\code\\Glass\\docs\\recon\\work\\hb_scan_html.json', JSON.stringify(out, null, 2));

// list image files on disk per dir
console.log('\n=== dirs ===');
for (const d of dirs) {
  const files = fs.readdirSync(path.join(HTML_DIR, d));
  console.log(d, '(' + files.length + '):', files.join(', '));
}
