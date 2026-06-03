import sys
import xml.etree.ElementTree as ET

NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}

tree = ET.parse('.tmp_thesis_v11/word/document.xml')
root = tree.getroot()
body = root.find('w:body', NS)

# Map styleId -> outline level. Read styles.xml
styles_tree = ET.parse('.tmp_thesis_v11/word/styles.xml')
styles_root = styles_tree.getroot()
style_to_outline = {}
style_to_name = {}
for s in styles_root.findall('w:style', NS):
    sid = s.get('{http://schemas.openxmlformats.org/wordprocessingml/2006/main}styleId')
    name_el = s.find('w:name', NS)
    name = name_el.get('{http://schemas.openxmlformats.org/wordprocessingml/2006/main}val') if name_el is not None else None
    style_to_name[sid] = name
    ppr = s.find('w:pPr', NS)
    if ppr is not None:
        ol = ppr.find('w:outlineLvl', NS)
        if ol is not None:
            style_to_outline[sid] = int(ol.get('{http://schemas.openxmlformats.org/wordprocessingml/2006/main}val'))

for p in body.findall('w:p', NS):
    ppr = p.find('w:pPr', NS)
    style_id = None
    if ppr is not None:
        ps = ppr.find('w:pStyle', NS)
        if ps is not None:
            style_id = ps.get('{http://schemas.openxmlformats.org/wordprocessingml/2006/main}val')
    text_parts = []
    for t in p.iter('{http://schemas.openxmlformats.org/wordprocessingml/2006/main}t'):
        if t.text:
            text_parts.append(t.text)
    txt = ''.join(text_parts).strip()
    if not txt:
        continue
    outline = style_to_outline.get(style_id)
    if outline is not None:
        hashes = '#' * (outline + 1)
        print(f"\n{hashes} {txt}\n")
    else:
        name = style_to_name.get(style_id, '')
        if name and ('Heading' in name or 'Заголовок' in name or 'Title' in name):
            print(f"\n## [STYLE:{name}] {txt}\n")
        else:
            print(txt)
