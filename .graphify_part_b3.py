import json
from graphify.cache import save_semantic_cache
from pathlib import Path

# Validate chunks on disk
new = {'nodes': [], 'edges': [], 'hyperedges': []}
for n in ('01', '02'):
    p = Path('graphify-out/.graphify_chunk_%s.json' % n)
    j = json.loads(p.read_text())
    assert 'nodes' in j and 'edges' in j, 'chunk %s invalid' % n
    new['nodes'].extend(j['nodes'])
    new['edges'].extend(j['edges'])
    new['hyperedges'].extend(j.get('hyperedges', []))

saved = save_semantic_cache(new['nodes'], new['edges'], new['hyperedges'])
print('Cached %d files' % saved)

# Merge with cache (nothing cached before this run, but follow the flow)
cached = json.loads(Path('.graphify_cached.json').read_text()) if Path('.graphify_cached.json').exists() else {'nodes': [], 'edges': [], 'hyperedges': []}

all_nodes = cached['nodes'] + new['nodes']
all_edges = cached['edges'] + new['edges']
all_hyperedges = cached.get('hyperedges', []) + new['hyperedges']
seen = set()
deduped = []
for n in all_nodes:
    if n['id'] not in seen:
        seen.add(n['id'])
        deduped.append(n)

merged = {
    'nodes': deduped,
    'edges': all_edges,
    'hyperedges': all_hyperedges,
    'input_tokens': 0,
    'output_tokens': 0,
}
Path('.graphify_semantic.json').write_text(json.dumps(merged, indent=2))
print('Extraction complete - %d nodes, %d edges (%d from cache, %d new)' % (
    len(deduped), len(all_edges), len(cached['nodes']), len(new['nodes'])))
