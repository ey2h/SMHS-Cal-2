// Tag/structure census for dumped UTF-8 htm text files.
const fs = require('fs');
const path = require('path');
const dir = 'C:\\code\\Glass\\docs\\recon\\help\\_inspect';
const counts = ['<style', '<table', '<tr', '<td', '<th', 'colspan', 'rowspan', '<img', '<v:imagedata', 'o:title', 'alt=', '<o:OLEObject', 'Equation.DSMT4', '<span', '<p ', '<p>', '<h1', '<h2', '<h3', '<h4', '<h5', '<h6', '<ul', '<ol', '<li', '<b>', '<i>', '<em', '<font', '<o:p>', '<span lang=EN-US', 'Wingdings', '<script', '<frameset', 'background', '<map', '<area', '<a href', 'src="', '<object', '<embed', 'class=Section', '<xml', '<![endif]', 'MsoHeader', 'MsoFooter', 'style='];
for (const f of fs.readdirSync(dir)) {
  const t = fs.readFileSync(path.join(dir, f), 'utf8');
  const row = [f, 'chars=' + [...t].length];
  for (const c of counts) {
    let n = 0, i = -1;
    const lower = t.toLowerCase();
    const cl = c.toLowerCase();
    while ((i = lower.indexOf(cl, i + 1)) !== -1) n++;
    row.push(c + '=' + n);
  }
  console.log(row.join(' '));
}
