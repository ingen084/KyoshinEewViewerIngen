# Anti-Patterns to Avoid

## Vague Descriptions

❌ **Bad:**
```yaml
description: Helps with data
```

✅ **Good:**
```yaml
description: Analyze Excel spreadsheets, create pivot tables, generate charts. Use when working with Excel files or .xlsx format.
```

## Over-Explaining

❌ **Bad:** (Claude already knows this)
```markdown
PDF (Portable Document Format) files are a common file format...
To extract text from a PDF, you'll need to use a library...
```

✅ **Good:**
```markdown
## Extract PDF text
Use pdfplumber:
```python
import pdfplumber
with pdfplumber.open("file.pdf") as pdf:
    text = pdf.pages[0].extract_text()
```
```

## Too Many Options

❌ **Bad:**
```markdown
You can use pypdf, or pdfplumber, or PyMuPDF, or pdf2image...
```

✅ **Good:**
```markdown
Use pdfplumber for text extraction.
For scanned PDFs requiring OCR, use pdf2image with pytesseract.
```

## Windows-Style Paths

❌ **Bad:** `scripts\helper.py`
✅ **Good:** `scripts/helper.py`

## Deeply Nested References

❌ **Bad:**
```
SKILL.md → advanced.md → details.md → actual info
```

✅ **Good:**
```
SKILL.md → advanced.md (direct link)
SKILL.md → reference.md (direct link)
```

## Time-Sensitive Information

❌ **Bad:**
```markdown
If you're doing this before August 2025, use the old API.
```

✅ **Good:**
```markdown
## Current method
Use v2 API: `api.example.com/v2/messages`

## Old patterns (deprecated)
<details><summary>Legacy v1 API</summary>
The v1 endpoint is no longer supported.
</details>
```

## Inconsistent Terminology

❌ **Bad:** Mix "API endpoint", "URL", "API route", "path"
✅ **Good:** Always use "API endpoint"

## Magic Numbers

❌ **Bad:**
```python
TIMEOUT = 47  # Why 47?
```

✅ **Good:**
```python
# HTTP requests typically complete within 30 seconds
REQUEST_TIMEOUT = 30
```

## Assuming Packages Are Installed

❌ **Bad:**
```markdown
Use the pdf library to process the file.
```

✅ **Good:**
```markdown
Install required package: `pip install pypdf`
```

## Wrong Point of View

❌ **Bad:**
```yaml
description: I can help you process Excel files
```

✅ **Good:**
```yaml
description: Processes Excel files and generates reports
```

## Unnecessary Files

Don't create:
- README.md
- INSTALLATION_GUIDE.md
- CHANGELOG.md

The skill should only contain what Claude needs to do the job.
