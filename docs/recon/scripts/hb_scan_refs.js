// Find every reference to an image file in every htm page (any attribute, any markup), case-insensitive.
const fs = require('fs');
const path = require('path');
const HTML_DIR = 'C:\\Program Files (x86)\\汇宝幕墙门窗结构计算软件V35.0\\html';

const IMG_EXT = /\.(gif|jpe?g|png|bmp|wmf|wmz|emf|tif|tiff|svg)\b/i;

function decode(buf) { return new TextDecoder('gb18030', { fatal: false }).decode(buf); }

const pages = fs.readdirSync(HTML_DIR).filter(n => /\.htm$/i.test(n));
for (const name of pages) {
  const text = decode(fs.readFileSync(path.join(HTML_DIR, name)));
  // any token ending in image extension
  const tokens = [...text.matchAll(/[A-Za-z0-9_\-\.\/\\]+\.(?:gif|jpe?g|png|bmp|wmf|wmz|emf|tif|svg)/gi)].map(m => m[0]);
  const uniq = [...new Set(tokens)];
  if (uniq.length) {
    console.log('### ' + name + ' (' + uniq.length + ' unique tokens)');
    for (const u of uniq) {
      const cnt = tokens.filter(t => t.toLowerCase() === u.toLowerCase()).length;
      console.log('   ' + u + '  x' + cnt);
    }
  } else {
    console.log('### ' + name + ' : none');
  }
  // also dump all tags that contain 'img' or 'imagedata' or 'v:shape' occurrences count
  const tags = { img: (text.match(/<img[\s>]/gi) || []).length, imagedata: (text.match(/imagedata/gi) || []).length, vml: (text.match(/<[a-z]*:v[ >]/gi) || []).length, object: (text.match(/<object[\s>]/gi) || []).length, embed: (text.match(/<embed[\s>]/gi) || []).length };
  console.log('    tags:', JSON.stringify(tags));
}
