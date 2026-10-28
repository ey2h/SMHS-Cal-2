// Extract user-role text messages from a DSH session.v4.jsonl backup.
// Usage: node mine_session_user_msgs.js <session.v4.jsonl> [out.txt]
const fs = require('fs');
const path = require('path');

const src = process.argv[2];
const out = process.argv[3] || null;
if (!src) { console.error('need session jsonl path'); process.exit(2); }

const lines = fs.readFileSync(src, 'utf8').split('\n').filter(Boolean);
const msgs = [];
let counts = {};
for (const line of lines) {
  let o;
  try { o = JSON.parse(line); } catch (e) { continue; }
  counts[o.type] = (counts[o.type] || 0) + 1;
  const d = o.data || {};
  const recs = [];
  const push = (t, meta) => {
    if (typeof t === 'string' && t.trim()) recs.push({ text: t.trim(), meta });
  };
  if (o.type === 'agent/inbox/spliced' && Array.isArray(d.inserted)) {
    for (const it of d.inserted) {
      if (it && it.role === 'user' && Array.isArray(it.content)) {
        for (const c of it.content) if (c && c.type === 'text') push(c.text, it.source && it.source.kind);
      }
    }
  }
  if (o.type === 'message' && d.role === 'user' && Array.isArray(d.content)) {
    for (const c of d.content) if (c && c.type === 'text') push(c.text, d.role);
  }
  if (o.type === 'user/message' || o.type === 'message/user') {
    if (typeof d.text === 'string') push(d.text, 'direct');
    if (Array.isArray(d.content)) for (const c of d.content) if (c && c.type === 'text') push(c.text, 'direct');
  }
  for (const r of recs) msgs.push({ seq: o.seq, type: o.type, kind: r.meta, text: r.text });
}

let buf = '=== entry type counts ===\n' + Object.entries(counts).sort((a,b)=>b[1]-a[1]).map(([k,v])=>`${k}\t${v}`).join('\n') + '\n\n';
buf += '=== user messages (' + msgs.length + ') ===\n';
for (const m of msgs) {
  const t = m.text.length > 1200 ? m.text.slice(0, 1200) + ' ...[truncated]' : m.text;
  buf += `--- seq ${m.seq} [${m.type}${m.kind ? '/' + m.kind : ''}] ---\n${t}\n`;
}
if (out) { fs.mkdirSync(path.dirname(out), { recursive: true }); fs.writeFileSync(out, buf, 'utf8'); console.log('wrote ' + out); }
else console.log(buf);
