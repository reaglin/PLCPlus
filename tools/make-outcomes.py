"""Writes docs/OUTCOMES.md: every module's outcomes with their stable IDs, the items that teach each one, and how
many check and practice questions give evidence for it (plan 9.6: the grant's outcomes, written as we go).

    python tools/make-outcomes.py

Run it after changing a module's outcomes, its items' objectives or its questions.
"""
import json
import os
import re

REPO = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
CONTENT = os.path.join(REPO, 'content', 'plcplus-foundations')


def front(path):
    text = open(path, encoding='utf-8').read()
    m = re.match(r'---\n(.*?)\n---\n', text, re.S)
    meta, outcomes, key = {}, [], None
    for line in (m.group(1) if m else '').splitlines():
        if re.match(r'^\s+- ', line) and key == 'outcomes':
            oid, words = line.strip()[2:].split(':', 1)
            outcomes.append((oid.strip(), words.strip()))
            continue
        if re.match(r'^\s+- ', line):
            continue
        k, _, v = line.partition(':')
        key = k.strip()
        v = v.strip()
        meta[key] = [x.strip() for x in v[1:-1].split(',') if x.strip()] if v.startswith('[') else v
    meta['outcomes'] = outcomes
    return meta


manifest = json.load(open(os.path.join(CONTENT, 'manifest.json'), encoding='utf-8'))
lines = ['# PLC+ — outcomes', '',
         'Every module\'s outcomes with their **stable IDs** (progress, mastery and reports key off them, so a course\'s or the grant\'s',
         'outcomes can be matched to them), the items that teach each one, and the questions that give evidence for it. The grant asks',
         'for faculty training on PLCs and student educational resources (Ron, question 12); the outcomes are written with each unit,',
         'and **Ron reviews them** (plan 9.6). Written by `tools/make-outcomes.py`; do not edit by hand.', '']
for folder in manifest['modules']:
    mdir = os.path.join(CONTENT, folder)
    mod = front(os.path.join(mdir, 'module.md'))
    items = []
    for f in sorted(os.listdir(mdir)):
        if f.endswith('.md') and f != 'module.md':
            items.append(front(os.path.join(mdir, f)))
    check_q, practice_q = {}, {}
    for it in items:
        if it.get('quiz'):
            quiz = json.load(open(os.path.join(CONTENT, it['quiz']), encoding='utf-8'))
            bucket = practice_q if quiz.get('practice') else check_q
            for q in quiz['questions']:
                if q.get('objective'):
                    bucket[q['objective']] = bucket.get(q['objective'], 0) + 1
    status = mod.get('status', '')
    lines.append(f'## Module {mod.get("number")}: {mod.get("title")} ({status})')
    lines.append('')
    if not mod['outcomes']:
        lines += ['No outcomes yet.', '']
        continue
    lines.append('| ID | The learner can | Taught in | Questions (check / practice) |')
    lines.append('|---|---|---|---|')
    for oid, words in mod['outcomes']:
        taught = [f'{mod.get("number")}.{n} {it.get("title")}' for n, it in enumerate(items, 1)
                  if oid in (it.get('objectives') or []) and it.get('kind') != 'assess']
        qs = f'{check_q.get(oid, 0)} / {practice_q.get(oid, 0)}' if (check_q.get(oid) or practice_q.get(oid)) else '—'
        lines.append(f'| `{oid}` | {words.replace("|", "/")} | {"; ".join(taught) or "—"} | {qs} |')
    lines.append('')
open(os.path.join(REPO, 'docs', 'OUTCOMES.md'), 'w', encoding='utf-8', newline='\n').write('\n'.join(lines))
print('wrote docs/OUTCOMES.md')
