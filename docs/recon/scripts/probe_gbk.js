// Quick probe: does Node's TextDecoder support GBK in this runtime?
const { TextDecoder } = require('node:util');
for (const label of ['gbk', 'gb18030', 'windows-936', 'cp936']) {
  try {
    const td = new TextDecoder(label);
    // 中 = 0xD6 0xD0 in GBK
    const s = td.decode(Buffer.from([0xD6, 0xD0, 0xC4, 0xEA]));
    console.log(label, '=> OK', JSON.stringify(s), 'decoded-name=', td.encoding);
  } catch (e) {
    console.log(label, '=> FAIL', e.message);
  }
}
console.log('node', process.version);
