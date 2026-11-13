# Builds docs/proto/skill-card-workshop.html from the rules, the levels, the page template and the solver's results.
import json, os
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'docs', 'proto', 'skill-card-workshop.html'))
read = lambda name: open(os.path.join(HERE, name), encoding='utf8').read()
core = read('core.js')
core = core[core.index('// CORE-START'):core.index('// CORE-END') + len('// CORE-END')]
levels = read('levels.js')
levels = levels[levels.index('// LEVELS-START'):levels.index('// LEVELS-END') + len('// LEVELS-END')]
survey = json.loads(read('survey.json'))
routes = 'const KNOWN_ROUTES = ' + json.dumps({k: v['routes'] for k, v in survey.items()}) + ';'
def rank(p):
    return (p['cost'], len(p['plan']['portals']), len(p['plan']['cards']))   # A tie goes to the plan that plays the level instead of hopping over it.
best = 'const BEST = ' + json.dumps({k: min(v['three'], key=rank) for k, v in survey.items()}, ensure_ascii=False) + ';'
page = read('page.html')
assert page.count('/*CORE*/') == 1 and page.count('/*LEVELS*/') == 1 and page.count('/*ROUTES*/') == 1 and page.count('/*BEST*/') == 1
page = page.replace('/*CORE*/', core).replace('/*LEVELS*/', levels).replace('/*ROUTES*/', routes).replace('/*BEST*/', best)
open(OUT, 'w', encoding='utf8').write(page)
print('built', os.path.relpath(OUT), len(page))
