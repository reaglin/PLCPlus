"""Writes the PLC instructors' review pages for softwareplus.ai: https://softwareplus.ai/plcplus/review/

    python tools/make-review-site.py            (from the PLCPlus folder; SoftwarePlus beside it)

The pages are made from this repository, never edited on the site:
  - index.html      the guide (docs/INSTRUCTOR-REVIEW.md), with the download and a link to each module
  - module-NN.html  each module as a learner meets it: outcomes, topics, and every item's Learn page,
                    with what its Example and Exercise tabs hold and a comment box per item
  - reference.html  the Reference pages
  - images/ladders  every program drawn by PLC+ itself (PLCPlus.exe --export-ladders)
  - images/screens  pictures of the window (tools/capture-window.ps1; copied from docs/images/review/)
  - PLCPlus-preview.zip  the review build (review/PLCPlus-preview-2026-11-04.zip), if it has been made

Like the other review pages on the site they are not linked from anywhere and carry noindex. Comments go
through the site's comment service (js/review-comments.js): a reviewer's link ends ?user=yes.
Then, in SoftwarePlus: .\\deploy.ps1 -Status, then .\\deploy.ps1 -Only plcplus -Reason "PLC+: review pages".
"""
import html
import json
import os
import re
import shutil
import subprocess
import sys

import markdown

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(REPO, 'content', 'plcplus-foundations')
OUT = os.path.join(os.path.dirname(REPO), 'SoftwarePlus', 'site', 'plcplus', 'review')
EXE = os.path.join(REPO, 'src', 'Learning.Shell', 'bin', 'Debug', 'net10.0-windows10.0.19041.0', 'PLCPlus.exe')
ZIP = os.path.join(REPO, 'review', 'PLCPlus-preview-2026-11-04.zip')
SCREENS = os.path.join(REPO, 'docs', 'images', 'review')
EMAIL = 'ron.eaglin@gmail.com'
COLOUR = '#1E7B34'  # PLC+'s power-on green


def front_matter(text):
    """The settings at the top of a content file (the same small YAML subset Learning.Core reads)."""
    lines = text.replace('\r\n', '\n').split('\n')
    if not lines or lines[0].strip() != '---':
        return {}, text
    values, key = {}, None
    for i, line in enumerate(lines[1:], 1):
        if line.strip() == '---':
            return values, '\n'.join(lines[i + 1:]).strip('\n')
        if not line.strip():
            continue
        if line.strip().startswith('- ') and key:
            values[key].append(line.strip()[2:])
            continue
        k, _, v = line.partition(':')
        key, v = k.strip(), v.strip()
        if not v:
            values[key] = []
        elif v.startswith('[') and v.endswith(']'):
            values[key] = [x.strip() for x in v[1:-1].split(',') if x.strip()]
            key = None
        else:
            values[key] = v
            key = None
    raise SystemExit('unclosed front-matter')


def truth_table(expr):
    """The truth-table widget as an HTML table (AND, OR, XOR, NOT, brackets)."""
    names = []
    for t in re.findall(r'[A-Za-z_][A-Za-z0-9_]*', expr):
        if t.upper() not in ('AND', 'OR', 'XOR', 'NOT') and t not in names:
            names.append(t)
    py = re.sub(r'\bXOR\b', '^', expr)
    py = re.sub(r'\bAND\b', ' and ', py)
    py = re.sub(r'\bOR\b', ' or ', py)
    py = re.sub(r'\bNOT\b', ' not ', py)
    rows = []
    for n in range(2 ** len(names)):
        vals = {name: bool(n >> (len(names) - 1 - i) & 1) for i, name in enumerate(names)}
        result = bool(eval(py, {}, vals))  # noqa: S307 - our own content, checked by the validator
        cells = ''.join(f'<td>{"TRUE" if vals[x] else "FALSE"}</td>' for x in names)
        rows.append(f'<tr>{cells}<td class="{"on" if result else "off"}">{"TRUE ✓" if result else "FALSE"}</td></tr>')
    head = ''.join(f'<th>{html.escape(x)}</th>' for x in names) + f'<th>{html.escape(expr)}</th>'
    return f'<p class="cap">Truth table: {html.escape(expr)}</p><table class="tt"><tr>{head}</tr>{"".join(rows)}</table>'


def widgets(md_text, up):
    """[[ladder: …]] and [[truth-table: …]] lines become a picture and a table."""
    out = []
    for line in md_text.split('\n'):
        m = re.match(r'^\s*\[\[\s*([a-z-]+)\s*:\s*(.*?)\s*\]\]\s*$', line)
        if not m:
            out.append(line)
        elif m.group(1) == 'ladder':
            name = os.path.splitext(os.path.basename(m.group(2)))[0]
            out.append(f'\n<figure><img src="{up}images/ladders/{name}.png" alt="Ladder diagram: {name}" loading="lazy">'
                       f'<figcaption>Drawn by PLC+ as on the Example tab (not running).</figcaption></figure>\n')
        elif m.group(1) == 'truth-table':
            out.append('\n' + truth_table(m.group(2)) + '\n')
    return '\n'.join(out)


def md(text, up='', shift=0):
    lines, prev = [], ''
    for line in widgets(text, up).splitlines():
        if re.match(r'^\s*(?:[-*]|\d+\.)\s', line) and prev.strip() and not re.match(r'^\s*(?:[-*]|\d+\.)\s', prev) \
                and not prev.startswith(' '):
            lines.append('')
        if shift and line.startswith('#'):
            line = '#' * shift + line
        lines.append(line)
        prev = line
    return markdown.markdown('\n'.join(lines), extensions=['tables', 'sane_lists'])


def thread(uri, title):
    return f'<div data-rc-thread="{html.escape(uri)}" data-rc-title="{html.escape(title)}"></div>'


def page(title, body):
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex, nofollow">
<title>{html.escape(title)} — PLC+ review</title>
<link rel="icon" href="../../images/favicon.svg" type="image/svg+xml">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Atkinson+Hyperlegible+Next:wght@400;700;800&display=swap">
<link rel="stylesheet" href="../../css/site.css">
<style>
  .review {{ padding: 28px 0 56px; }}
  .review h1 {{ font-size: clamp(1.5rem, 3vw, 2.1rem); margin-bottom: .3em; }}
  .review h2 {{ margin-top: 1.6em; }}
  .review table {{ border-collapse: collapse; margin: 12px 0 20px; font-size: .95rem; }}
  .review th, .review td {{ border: 1px solid #cfd6df; padding: 6px 9px; text-align: left; vertical-align: top; }}
  .review th {{ background: #eaf1f8; }}
  .review img {{ max-width: 100%; height: auto; border: 1px solid #cfd6df; }}
  .review code {{ font-family: Consolas, "Courier New", monospace; font-size: .9em; background: #eef1f4; padding: 0 3px; }}
  .review figure {{ margin: 12px 0 18px; }}
  .review figure img {{ max-width: min(100%, 720px); }}
  .review figcaption, .cap {{ font-size: .88rem; color: #4a5563; }}
  .cap {{ font-weight: 700; color: #1b2430; margin-bottom: 2px; }}
  .tt td.on {{ color: #1e7b34; font-weight: 700; }} .tt td.off {{ color: #6b7685; }}
  .note {{ background: #fff8e1; border-left: 4px solid #e0a800; padding: 10px 14px; margin: 14px 0; }}
  .item {{ border-top: 3px solid var(--c); margin-top: 34px; padding-top: 6px; }}
  .tabs {{ background: #f4f8fc; border: 1px solid #c9d8ea; padding: 10px 14px; margin: 10px 0; }}
  .tabs dt {{ font-weight: 700; }} .tabs dd {{ margin: 0 0 6px 0; }}
  .badge {{ display: inline-block; padding: 1px 9px; border-radius: 10px; background: #e8eef5; font-weight: 700; font-size: .9rem; }}
  .crumbs a {{ margin-right: 14px; }}
  .btn {{ display: inline-block; font-weight: 700; padding: 9px 16px; border-radius: 6px; background: var(--c); color: #fff; text-decoration: none; }}
  .modules td:first-child {{ white-space: nowrap; }}
</style>
</head>
<body style="--c: {COLOUR}">
<main id="main" class="review"><div class="wrap">
{body}
</div></main>
<script src="/js/review-comments.js" defer></script>
</body>
</html>
"""


KIND_WORDS = {'choice': 'Choose one', 'multiple': 'Choose every one that is right', 'numeric': 'A number', 'truthTable': 'Fill in a truth table',
              'match': 'Match each one', 'fault': 'Find the fault: a machine with a faulty wire, running the working program'}


def questions_html(quiz, challenges):
    """The item's questions as the instructor sees them: each with its answer marked, and why."""
    out = [f'<h3>The questions (on the Exercise tab)</h3><p>{len(quiz["questions"])} questions; the learner needs '
           f'{round(quiz.get("passThreshold", 0.8) * 100)} % to pass and can try again. '
           + ('Practice: a wrong answer shows the right one.' if quiz.get('practice') else
              'A wrong answer says “not quite, look again”; the reason is shown once the answer is right.')
           + ' <strong>The right answers are marked ✓ here only.</strong></p><ol>']
    for q in quiz['questions']:
        kind = q.get('kind', 'choice')
        bits = [f'<li><p><em>{KIND_WORDS.get(kind, kind)}.</em> {md(q["prompt"])[3:-4]}</p>']
        if kind == 'fault' and q.get('machine') in challenges:
            bits.append(f'<p>Machine: {html.escape(challenges[q["machine"]][0]["title"])}; the fault: <code>{html.escape(q.get("fault", ""))}</code></p>')
        if q.get('options'):
            bits.append('<ul>' + ''.join(f'<li>{"✓ <strong>" if i in q.get("correct", []) else ""}{html.escape(o)}{"</strong>" if i in q.get("correct", []) else ""}</li>'
                                         for i, o in enumerate(q['options'])) + '</ul>')
        if kind == 'numeric':
            bits.append(f'<p>✓ <strong>{q.get("answer")} {html.escape(q.get("unit") or "")}</strong> (within {q.get("tolerance", 0)})</p>')
        if kind == 'truthTable':
            bits.append(truth_table(q['expression']))
        if kind == 'match':
            bits.append('<ul>' + ''.join(f'<li><code>{html.escape(p["left"])}</code> → {html.escape(p["right"])}</li>' for p in q['pairs']) + '</ul>')
        if q.get('explanation'):
            bits.append(f'<p><small>Why: {html.escape(q["explanation"])}</small></p>')
        out.append(''.join(bits) + '</li>')
    return ''.join(out) + '</ol>'


STATUS_WORDS = {'draft': 'First draft, for your review', 'outline': 'Outline: titles and outcomes only',
                'coming-later': 'Coming later: outcomes and topics only', 'ready': 'Ready'}


def main():
    if os.path.exists(OUT):
        shutil.rmtree(OUT)
    os.makedirs(os.path.join(OUT, 'images', 'screens'))

    # the ladder pictures, drawn by PLC+ itself
    if not os.path.exists(EXE):
        sys.exit(f'Build PLC+ first: {EXE} is missing.')
    subprocess.run([EXE, '--export-ladders', os.path.join(OUT, 'images', 'ladders')], check=True)
    if os.path.isdir(SCREENS):
        for f in os.listdir(SCREENS):
            shutil.copy(os.path.join(SCREENS, f), os.path.join(OUT, 'images', 'screens', f))
    has_zip = os.path.exists(ZIP)
    if has_zip:
        shutil.copy(ZIP, os.path.join(OUT, 'PLCPlus-preview.zip'))

    manifest = json.load(open(os.path.join(CONTENT, 'manifest.json'), encoding='utf-8'))
    challenges = {}
    for c in manifest['challenges']:
        ch = json.load(open(os.path.join(CONTENT, c['file']), encoding='utf-8'))
        challenges[ch['id']] = (ch, c['programs'])
    refs = {}
    for f in sorted(os.listdir(os.path.join(CONTENT, 'reference'))):
        meta, body = front_matter(open(os.path.join(CONTENT, 'reference', f), encoding='utf-8').read())
        refs[f[:-3]] = (meta.get('title', f[:-3]), body)

    modules = []
    for folder in manifest['modules']:
        d = os.path.join(CONTENT, folder)
        meta, intro = front_matter(open(os.path.join(d, 'module.md'), encoding='utf-8').read())
        items = []
        for n, f in enumerate(sorted(x for x in os.listdir(d) if x.endswith('.md') and x != 'module.md'), 1):
            im, learn = front_matter(open(os.path.join(d, f), encoding='utf-8').read())
            items.append((f'{meta["number"]}.{n}', im, learn))
        modules.append((meta, intro, items))
    by_id = {im['id']: (num, im) for meta, _, items in modules for num, im, _ in items}
    titles = {meta['id']: f'Module {meta["number"]}, {meta["title"]}' for meta, _, _ in modules}

    # ---- each module
    for meta, intro, items in modules:
        num = int(meta['number'])
        b = [f'<p class="crumbs"><a href="index.html">← The review guide</a><a href="reference.html">Reference pages</a></p>',
             f'<h1>Module {num} · {html.escape(meta["title"])}</h1>',
             f'<p><span class="badge">{STATUS_WORDS.get(meta.get("status", "ready"))}</span>'
             + (f' · about {round(int(meta["minutes"]) / 60, 1):g} hours' if meta.get('minutes', '0') != '0' else '') + '</p>',
             '<h2>Recommended first</h2>',
             '<p>' + (', '.join(html.escape(titles[p]) for p in meta.get('prerequisites', [])) or 'Nothing: this module can be the first.')
             + ' <em>(recommended, not enforced)</em></p>',
             '<h2>What the learner will be able to do</h2><ul>'
             + ''.join(f'<li>{html.escape(o.partition(": ")[2])} <small>({html.escape(o.partition(": ")[0])})</small></li>' for o in meta.get('outcomes', []))
             + '</ul>',
             '<h2>Topics</h2><p>' + ' · '.join(html.escape(t) for t in meta.get('topics', [])) + '</p>']
        if intro.strip():
            b.append('<h2>About this module</h2>' + md(intro))
        if items:
            b.append('<h2>The items</h2><p>Each item has four tabs in PLC+: <strong>Learn</strong> (the lesson below), '
                     '<strong>Example</strong> (a machine running a working program), <strong>Exercise</strong> (the learner '
                     'writes the program; PLC+ checks it) and <strong>Reference</strong>. Every item has a comment box under it.</p><ol>'
                     + ''.join(f'<li><a href="#{html.escape(im["id"])}">{html.escape(n)} {html.escape(im["title"])}</a></li>' for n, im, _ in items) + '</ol>')
        for n, im, learn in items:
            ex, exr = im.get('example'), im.get('exercise')
            tabs = []
            if im.get('kind') == 'assess':
                tabs.append(('Module check', 'Complete when every item it covers is: '
                             + ', '.join(html.escape(by_id[c][0]) for c in im.get('covers', []) if c in by_id)
                             + ('. Its questions, on the Exercise tab, are below; it is complete when those items are and the questions are passed.'
                                if im.get('quiz') else '.')
                             + ' In a later version, finishing it makes the progress report to hand in.'))
            else:
                if ex in challenges:
                    ch, programs = challenges[ex]
                    names = [p['title'] for p in programs]
                    tabs.append(('Example', f'The machine for “{html.escape(ch["title"])}”, running the working program '
                                 f'(also: {html.escape(", ".join(names[1:]))}). <a href="#{html.escape(im["id"])}-pic">The working program</a>.'))
                else:
                    tabs.append(('Example', 'None: this item has no machine.'))
                if exr in challenges:
                    ch, _ = challenges[exr]
                    tabs.append(('Exercise', f'The learner writes the program for: <em>{html.escape(ch["brief"])}</em> '
                                 f'PLC+ checks it with {len(ch["scenarios"])} tests'
                                 + (f' ({sum(1 for s in ch["scenarios"] if s.get("hidden"))} hidden)' if any(s.get('hidden') for s in ch['scenarios']) else '')
                                 + '. Passing every test completes the item.'))
                else:
                    tabs.append(('Exercise', 'None: the item is complete when the learner has read it.'))
                if im.get('reference'):
                    tabs.append(('Reference', ', '.join(f'<a href="reference.html#{html.escape(r)}">{html.escape(refs[r][0])}</a>' for r in im['reference'] if r in refs)))
            b.append(f'<section class="item" id="{html.escape(im["id"])}"><h2>{html.escape(n)} {html.escape(im["title"])}</h2>')
            meta_bits = []
            if im.get('minutes'):
                meta_bits.append(f'about {im["minutes"]} minutes')
            if im.get('builds-on') in by_id:
                meta_bits.append(f'builds on {by_id[im["builds-on"]][0]}')
            if meta_bits:
                b.append(f'<p><em>{html.escape(" · ".join(meta_bits))}</em></p>')
            b.append('<dl class="tabs">' + ''.join(f'<dt>{t}</dt><dd>{d}</dd>' for t, d in tabs) + '</dl>')
            if learn.strip():
                b.append('<h3>The Learn tab</h3>' + md(learn, shift=2))
            else:
                b.append('<p class="note">The Learn page is still to be written.</p>')
            if ex in challenges:
                prog = challenges[ex][1][0]['file']
                name = os.path.splitext(os.path.basename(prog))[0]
                b.append(f'<figure id="{html.escape(im["id"])}-pic"><img src="images/ladders/{name}.png" alt="The working program" loading="lazy">'
                         f'<figcaption>The Example\'s working program. (In the finished modules the example will be a similar machine, not the exercise\'s own answer.)</figcaption></figure>')
            if im.get('quiz'):
                b.append(questions_html(json.load(open(os.path.join(CONTENT, im['quiz']), encoding='utf-8')), challenges))
            b.append('<h3>Your comments on this item</h3>' + thread(f'/plcplus/review/module-{num:02d}.html?q={im["id"]}', f'PLC+ {n} {im["title"]}') + '</section>')
        b.append('<section class="item"><h2>Your comments on the module as a whole</h2>'
                 + thread(f'/plcplus/review/module-{num:02d}.html?q=module', f'PLC+ module {num}: {meta["title"]}') + '</section>')
        open(os.path.join(OUT, f'module-{num:02d}.html'), 'w', encoding='utf-8').write(page(f'Module {num}: {meta["title"]}', '\n'.join(b)))

    # ---- the Reference pages
    b = ['<p class="crumbs"><a href="index.html">← The review guide</a></p><h1>Reference pages</h1>',
         '<p>The Reference tab of each item shows the pages it lists. These are all of them.</p>']
    for name, (title, body) in refs.items():
        b.append(f'<section class="item" id="{html.escape(name)}">' + md(body, shift=1)
                 + thread(f'/plcplus/review/reference.html?q={name}', f'PLC+ reference: {title}') + '</section>')
    open(os.path.join(OUT, 'reference.html'), 'w', encoding='utf-8').write(page('Reference pages', '\n'.join(b)))

    # ---- the guide
    guide = open(os.path.join(REPO, 'docs', 'INSTRUCTOR-REVIEW.md'), encoding='utf-8').read()
    rows = ''.join(f'<tr><td><a href="module-{int(meta["number"]):02d}.html">Module {meta["number"]}</a></td>'
                   f'<td>{html.escape(meta["title"])}</td><td>{STATUS_WORDS.get(meta.get("status", "ready"))}</td>'
                   f'<td>{len(items)}</td></tr>' for meta, _, items in modules)
    top = ['<h1>PLC+ — review for PLC instructors</h1>',
           '<p class="note">Thank you for reviewing PLC+. <strong>Read the modules on this site</strong>, and comment under each item. '
           + ('To try the program itself (Windows), <a href="PLCPlus-preview.zip">download the preview</a> (a zip, 63 MB; unzip it and open PLCPlus.exe). '
              if has_zip else '')
           + f'You can also email notes to Ron Eaglin at <a href="mailto:{EMAIL}">{EMAIL}</a>.</p>',
           '<h2>The modules</h2><table class="modules"><tr><th>Module</th><th>Title</th><th>State</th><th>Items</th></tr>' + rows + '</table>',
           '<p><a href="reference.html">All the Reference pages</a></p>']
    shots = sorted(os.listdir(os.path.join(OUT, 'images', 'screens')))
    if shots:
        top.append('<h2>What it looks like</h2>' + ''.join(
            f'<figure><img src="images/screens/{s}" alt="PLC+: {html.escape(os.path.splitext(s)[0][3:].replace("-", " "))}" loading="lazy">'
            f'<figcaption>{html.escape(os.path.splitext(s)[0][3:].replace("-", " ").capitalize())}</figcaption></figure>' for s in shots))
    guide_html = md(re.sub(r'^# .*\n', '', guide, count=1), shift=1)
    body = '\n'.join(top) + '<h2>The guide</h2>' + guide_html + '<section class="item"><h2>General comments</h2>' \
        + thread('/plcplus/review/index.html?q=general', 'PLC+ review: general comments') + '</section>'
    open(os.path.join(OUT, 'index.html'), 'w', encoding='utf-8').write(page('Review for PLC instructors', body))
    print(f'wrote {OUT}: {len(modules)} module pages, {len(refs)} reference pages, zip {"copied" if has_zip else "not made yet"}')


if __name__ == '__main__':
    main()
