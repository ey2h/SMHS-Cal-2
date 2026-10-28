'use strict';
/*
 * wmf_probe.js — 对少量 WMF/WMZ 样本做详细转储，用于验证 wmf_lib.js 的解析假设
 * 用法: node wmf_probe.js <file1> [file2 ...]
 */
const fs = require('fs');
const path = require('path');
const L = require('./wmf_lib.js');

function dumpFile(file) {
  console.log('='.repeat(90));
  console.log('FILE:', file, 'bytes=', fs.statSync(file).size);
  const buf = fs.readFileSync(file);
  if (buf.length >= 2 && buf[0] === 0x50 && buf[1] === 0x4b) {
    const z = L.readWmz(buf);
    console.log('WMZ entries:', (z.entries || []).map((e) => `${e.name} method=${e.method} ${e.compSize}->${e.uncompSize}`));
    for (const e of z.entries || []) {
      if (e.data) return dumpInner(file + '!' + e.name, e.data);
    }
    console.log('WMZ error:', z.error);
    return;
  }
  if (L.isEmf(buf)) {
    console.log('EMF detected');
    const r = L.walkEmf(buf);
    for (const rec of r.recs) {
      if (rec.text) console.log(' TEXT', rec.name, JSON.stringify(rec.text.candidates && rec.text.candidates[0]));
    }
    console.log(' EMF recs=', r.recs.length, 'errors=', JSON.stringify(r.errors));
    return;
  }
  const h = L.parseHeaders(buf);
  console.log('header:', JSON.stringify(h, null, 1).slice(0, 1200));
  if (h.kind === 'not-wmf' || h.kind === 'too-small') {
    console.log('head hex:', buf.subarray(0, 48).toString('hex'));
    return;
  }
  const r = L.walkWmf(buf, h.recordStart, h);
  const hist = {};
  for (const rec of r.recs) hist[rec.name] = (hist[rec.name] || 0) + 1;
  console.log('records=', r.recs.length, 'hist=', JSON.stringify(hist));
  console.log('errors=', JSON.stringify(r.errors));
  let ti = 0;
  for (const rec of r.recs) {
    if (rec.text) {
      ti++;
      if (ti <= 30) {
        console.log(` TEXT#${ti} @${rec.at} ${rec.name} x=${rec.text.x} y=${rec.text.y} cch=${rec.text.cch} cb=${rec.text.cbString} region=${rec.text.regionLen}`);
        for (const c of rec.text.candidates.slice(0, 2)) console.log('    cand[' + c.enc + '] score=' + c.score + ' => ' + JSON.stringify(c.text));
      }
    }
    if (rec.font) {
      console.log(` FONT @${rec.at} skip=${rec.font.skip} h=${rec.font.height} w=${rec.font.width} wt=${rec.font.weight} charset=${rec.font.charsetName} face=${JSON.stringify(rec.font.faceName)}${rec.font.error ? ' ERR=' + rec.font.error : ''}`);
    }
    if (rec.escape) {
      console.log(` ESCAPE @${rec.at} len=${rec.escape.len} head=${rec.escape.headHex} print=${JSON.stringify(rec.escape.printable)}`);
    }
  }
  const last = r.recs[r.recs.length - 1];
  console.log('last rec:', JSON.stringify(last && { name: last.name, at: last.at }));
}

function dumpInner(name, buf) {
  console.log('INNER:', name, 'bytes=', buf.length);
  const h = L.parseHeaders(buf);
  console.log('inner header:', JSON.stringify(h).slice(0, 800));
  const r = L.walkWmf(buf, h.recordStart, h);
  const hist = {};
  for (const rec of r.recs) hist[rec.name] = (hist[rec.name] || 0) + 1;
  console.log('records=', r.recs.length, 'hist=', JSON.stringify(hist), 'errors=', JSON.stringify(r.errors));
  for (const rec of r.recs) {
    if (rec.text) {
      console.log(` TEXT @${rec.at} x=${rec.text.x} y=${rec.text.y} cch=${rec.text.cch} cb=${rec.text.cbString}`);
      for (const c of rec.text.candidates.slice(0, 2)) console.log('    cand[' + c.enc + '] => ' + JSON.stringify(c.text));
    }
    if (rec.font) console.log(` FONT skip=${rec.font.skip} h=${rec.font.height} charset=${rec.font.charsetName} face=${JSON.stringify(rec.font.faceName)}${rec.font.error ? ' ERR' : ''}`);
    if (rec.escape) console.log(' ESCAPE', JSON.stringify(rec.escape.printable));
  }
}

for (const f of process.argv.slice(2)) dumpFile(f);
