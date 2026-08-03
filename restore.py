"""One-off: place decompiled types back into the repository, tidied.

ILSpy re-attaches the XML documentation but writes tabs and fully-qualified cref attributes
(cref="F:Rustaveli.Pdf.Layout.SpacePlanType.Wrap"). Both are normalised back to house style here.
"""
import io
import os
import re
import shutil

DECOMP = os.path.join(os.environ['LOCALAPPDATA'], 'Temp', 'decomp')

# repo path <- (decompiled assembly, type name)
PLAN = [
    ('src/Rustaveli.Pdf/Documents/Document.cs', 'Rustaveli.Pdf', 'Document'),
    ('src/Rustaveli.Pdf/Drawing/IDocumentCanvas.cs', 'Rustaveli.Pdf', 'IDocumentCanvas'),
    ('src/Rustaveli.Pdf/Drawing/IImage.cs', 'Rustaveli.Pdf', 'IImage'),
    ('src/Rustaveli.Pdf/Elements/LayersElement.cs', 'Rustaveli.Pdf', 'LayersElement'),
    ('src/Rustaveli.Pdf/Elements/ListElement.cs', 'Rustaveli.Pdf', 'ListElement'),
    ('src/Rustaveli.Pdf/Elements/RowElement.cs', 'Rustaveli.Pdf', 'RowElement'),
    ('src/Rustaveli.Pdf/Elements/TableElement.cs', 'Rustaveli.Pdf', 'TableElement'),
    ('src/Rustaveli.Pdf/Fluent/ColumnDescriptor.cs', 'Rustaveli.Pdf', 'ColumnDescriptor'),
    ('src/Rustaveli.Pdf/Fluent/ContentExtensions.cs', 'Rustaveli.Pdf', 'ContentExtensions'),
    ('src/Rustaveli.Pdf/Fluent/TableDescriptor.cs', 'Rustaveli.Pdf', 'TableDescriptor'),
    ('src/Rustaveli.Pdf/Fluent/TextDescriptor.cs', 'Rustaveli.Pdf', 'TextDescriptor'),
    ('src/Rustaveli.Pdf/Layout/Element.cs', 'Rustaveli.Pdf', 'Element'),
    ('src/Rustaveli.Pdf/Layout/LayoutContext.cs', 'Rustaveli.Pdf', 'LayoutContext'),
    ('src/Rustaveli.Pdf/Layout/SpacePlan.cs', 'Rustaveli.Pdf', 'SpacePlan'),
    ('src/Rustaveli.Pdf/Primitives/Colors.cs', 'Rustaveli.Pdf', 'Colors'),
    ('src/Rustaveli.Pdf/Primitives/Unit.cs', 'Rustaveli.Pdf', 'Unit'),
    ('src/Rustaveli.Pdf/Text/ITextMeasurer.cs', 'Rustaveli.Pdf', 'ITextMeasurer'),
    ('src/Rustaveli.Pdf/Text/TextStyle.cs', 'Rustaveli.Pdf', 'TextStyle'),
    ('src/Rustaveli.Pdf.Skia/PdfGenerationExtensions.cs', 'Rustaveli.Pdf.Skia', 'PdfGenerationExtensions'),
    ('tests/Rustaveli.Pdf.IntegrationTests/Comparison/PdfSnapshot.cs',
     'Rustaveli.Pdf.IntegrationTests', 'PdfSnapshot'),
    ('tests/Rustaveli.Pdf.IntegrationTests/Comparison/Recipes.cs',
     'Rustaveli.Pdf.IntegrationTests', 'Recipes'),
    ('tests/Rustaveli.Pdf.UnitTests/DocumentGeneratorTests.cs',
     'Rustaveli.Pdf.UnitTests', 'DocumentGeneratorTests'),
    ('tests/Rustaveli.Pdf.UnitTests/TestDoubles/RecordingCanvas.cs',
     'Rustaveli.Pdf.UnitTests', 'RecordingCanvas'),
]

CREF = re.compile(r'cref="[TFPME]:([^"]+)"')


def tidy_cref(match):
    """cref="F:Rustaveli.Pdf.Layout.SpacePlanType.Wrap" -> cref="SpacePlanType.Wrap"."""
    full = match.group(1)
    full = re.sub(r'\(.*\)$', '', full)             # drop method parameter lists
    parts = full.split('.')
    keep = [p for p in parts if p and (p[0].isupper() or p[0] == '_')]
    # Namespace segments are capitalised too, so keep only the trailing type/member pair.
    tail = keep[-2:] if len(keep) >= 2 and not keep[-1][0].islower() else keep[-1:]
    if len(tail) == 2 and tail[0] in ('Rustaveli', 'Pdf'):
        tail = tail[-1:]
    return 'cref="' + '.'.join(tail) + '"'


def find_source(assembly, type_name):
    root = os.path.join(DECOMP, assembly)
    for dirpath, _dirnames, filenames in os.walk(root):
        if type_name + '.cs' in filenames:
            return os.path.join(dirpath, type_name + '.cs')
    return None


placed = missing = 0
for dest, assembly, type_name in PLAN:
    src = find_source(assembly, type_name)
    if not src:
        print(f'MISSING  {type_name}')
        missing += 1
        continue
    text = io.open(src, encoding='utf-8-sig').read()
    text = text.replace('\t', '    ')
    text = CREF.sub(tidy_cref, text)
    text = re.sub(r'\n{3,}', '\n\n', text)
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    io.open(dest, 'w', encoding='utf-8', newline='').write(
        text if text.endswith('\n') else text + '\n')
    placed += 1
    print(f'placed   {dest}  ({len(text)} chars)')

print(f'\n{placed} placed, {missing} missing')
