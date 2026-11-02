"""แปลง Summary-Week13-for-Exam.md -> .docx (รองรับหัวข้อ, ตาราง, bullet, code block, ตัวหนา/inline code)"""
import re
import sys
from docx import Document
from docx.shared import Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

SRC = "Summary-Week13-for-Exam.md"
DST = "Summary-Week13-for-Exam.docx"
FONT = "TH Sarabun New"

doc = Document()
for s in doc.sections:
    s.left_margin = s.right_margin = Pt(54)
    s.top_margin = s.bottom_margin = Pt(54)

st = doc.styles["Normal"]
st.font.name = FONT
st.font.size = Pt(14)
st.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
for name, size in (("Heading 1", 24), ("Heading 2", 20), ("Heading 3", 17)):
    h = doc.styles[name]
    h.font.name = FONT
    h.font.size = Pt(size)
    h.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)


def shade(cell, color):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), color)
    tcPr.append(shd)


TOKEN = re.compile(r"(\*\*.+?\*\*|`.+?`)")


def add_runs(par, text, bold=False, size=None):
    text = text.replace("<br/>", "\n").replace("<br>", "\n")
    for part in TOKEN.split(text):
        if not part:
            continue
        if part.startswith("**") and part.endswith("**"):
            r = par.add_run(part[2:-2])
            r.bold = True
        elif part.startswith("`") and part.endswith("`"):
            r = par.add_run(part[1:-1])
            r.font.name = "Consolas"
            r._element.rPr.rFonts.set(qn("w:eastAsia"), "Consolas")
            r.font.color.rgb = RGBColor(0xC7, 0x25, 0x4E)
        else:
            r = par.add_run(part)
            r.bold = bold
        if bold:
            r.bold = True
        if size:
            r.font.size = Pt(size)


def add_table(rows):
    header = rows[0]
    body = rows[1:]
    t = doc.add_table(rows=1, cols=len(header))
    t.style = "Table Grid"
    for i, c in enumerate(header):
        cell = t.rows[0].cells[i]
        cell.text = ""
        add_runs(cell.paragraphs[0], c, bold=True, size=12)
        shade(cell, "D9E2F3")
    for row in body:
        cells = t.add_row().cells
        for i in range(len(header)):
            val = row[i] if i < len(row) else ""
            cells[i].text = ""
            add_runs(cells[i].paragraphs[0], val, size=12)
    doc.add_paragraph()


def split_row(line):
    line = line.strip()
    if line.startswith("|"):
        line = line[1:]
    if line.endswith("|"):
        line = line[:-1]
    return [c.strip() for c in re.split(r"(?<!\\)\|", line)]


with open(SRC, encoding="utf-8") as f:
    lines = f.read().split("\n")

i = 0
while i < len(lines):
    ln = lines[i].rstrip("\r")

    if ln.strip().startswith("```"):
        i += 1
        code = []
        while i < len(lines) and not lines[i].strip().startswith("```"):
            code.append(lines[i].rstrip("\r"))
            i += 1
        i += 1
        p = doc.add_paragraph()
        r = p.add_run("\n".join(code))
        r.font.name = "Consolas"
        r._element.rPr.rFonts.set(qn("w:eastAsia"), "Consolas")
        r.font.size = Pt(10)
        pPr = p._p.get_or_add_pPr()
        shd = OxmlElement("w:shd")
        shd.set(qn("w:val"), "clear")
        shd.set(qn("w:fill"), "F2F2F2")
        pPr.append(shd)
        continue

    if ln.strip().startswith("|"):
        rows = []
        while i < len(lines) and lines[i].strip().startswith("|"):
            cells = split_row(lines[i].rstrip("\r"))
            if not all(re.fullmatch(r":?-{2,}:?", c) for c in cells):
                rows.append(cells)
            i += 1
        add_table(rows)
        continue

    m = re.match(r"^(#{1,3})\s+(.*)", ln)
    if m:
        h = doc.add_heading(level=len(m.group(1)))
        add_runs(h, m.group(2))
        i += 1
        continue

    if ln.strip() == "---":
        i += 1
        continue

    if ln.startswith(">"):
        p = doc.add_paragraph()
        p.paragraph_format.left_indent = Pt(18)
        add_runs(p, ln.lstrip("> ").strip())
        for r in p.runs:
            r.italic = True
        i += 1
        continue

    m = re.match(r"^(\s*)[-*]\s+(.*)", ln)
    if m:
        level = len(m.group(1)) // 2
        p = doc.add_paragraph(style="List Bullet" if level == 0 else "List Bullet 2")
        add_runs(p, m.group(2))
        i += 1
        continue

    m = re.match(r"^(\s*)\d+\.\s+(.*)", ln)
    if m:
        level = len(m.group(1)) // 2
        p = doc.add_paragraph(style="List Number" if level == 0 else "List Number 2")
        add_runs(p, m.group(2))
        i += 1
        continue

    if ln.strip():
        p = doc.add_paragraph()
        add_runs(p, ln.strip())
    i += 1

doc.save(DST)
print("saved", DST)
