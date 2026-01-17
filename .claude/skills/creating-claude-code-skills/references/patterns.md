# Skill Design Patterns

## Progressive Disclosure

SKILL.md serves as table of contents. Claude loads additional files only when needed.

**Pattern 1: High-level guide with references**
```markdown
# PDF Processing

## Quick start
[basic example]

## Advanced features
- **Form filling**: See [FORMS.md](FORMS.md)
- **API reference**: See [REFERENCE.md](REFERENCE.md)
```

**Pattern 2: Domain-specific organization**
```
bigquery-skill/
├── SKILL.md
└── reference/
    ├── finance.md
    ├── sales.md
    └── product.md
```

## Template Pattern

Provide templates for consistent output:
```markdown
## Report structure

Use this template:
```markdown
# [Title]
## Executive summary
[One paragraph]
## Key findings
- Finding 1
- Finding 2
```
```

## Examples Pattern

Show input/output pairs:
```markdown
## Commit messages

**Example 1:**
Input: Added JWT authentication
Output:
```
feat(auth): implement JWT-based authentication
```
```

## Workflow Pattern

For complex tasks, provide checklists:
```markdown
## PDF form filling

Task Progress:
- [ ] Step 1: Analyze form
- [ ] Step 2: Create mapping
- [ ] Step 3: Validate
- [ ] Step 4: Fill form
- [ ] Step 5: Verify output
```

## Feedback Loop Pattern

Run validator → fix errors → repeat:
```markdown
1. Make edits
2. Run: `python scripts/validate.py`
3. If errors, fix and re-validate
4. Only proceed when validation passes
```

## Tool Permissions (allowed-tools)

Restrict available tools for focused skills:
```yaml
---
name: code-reviewer
description: Review code for issues. Use when reviewing PRs or code quality.
allowed-tools: Read, Grep, Glob
---
```

## MCP Tool References

Use fully qualified names: `ServerName:tool_name`
```markdown
Use BigQuery:bigquery_schema to retrieve table schemas.
```

## Script Guidelines

**Handle errors explicitly:**
```python
def process_file(path):
    try:
        with open(path) as f:
            return f.read()
    except FileNotFoundError:
        print(f"Creating {path}")
        with open(path, 'w') as f:
            f.write('')
        return ''
```

**Document constants:**
```python
# 30 seconds accounts for slow connections
REQUEST_TIMEOUT = 30
```
