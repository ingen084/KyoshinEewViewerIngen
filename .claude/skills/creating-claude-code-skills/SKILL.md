---
name: creating-claude-code-skills
description: Guide for creating effective Claude Code Agent Skills. Use when users want to create, improve, or troubleshoot Skills that extend Claude's capabilities with specialized workflows, domain expertise, or tool integrations.
---

# Creating Claude Code Skills

Skills are modular packages that extend Claude's capabilities with specialized knowledge, workflows, and tools.

## Quick Start

```
my-skill/
├── SKILL.md              # Required: metadata + instructions
├── reference.md          # Optional: detailed docs
└── scripts/              # Optional: utility scripts
    └── helper.py
```

## SKILL.md Structure

```yaml
---
name: skill-name          # lowercase, hyphens, max 64 chars
description: What it does and when to use it. Max 1024 chars.
---

# Skill Name

## Instructions
Clear, step-by-step guidance.

## Examples
Concrete input/output examples.
```

## Core Principles

1. **Be Concise**: Claude is smart. Only add context Claude doesn't already have.
2. **Progressive Disclosure**: SKILL.md is overview. Details go in separate files.
3. **Match Freedom to Risk**: Fragile operations need specific scripts; flexible tasks need general guidance.

## Writing Effective Descriptions

The `description` field is critical for discovery. Include:
- What the skill does
- When to use it (triggers/contexts)
- Key terms users would mention

**Good**:
```yaml
description: Extract text from PDF files, fill forms, merge documents. Use when working with PDF files, forms, or document extraction.
```

**Bad**:
```yaml
description: Helps with documents
```

## File Organization Patterns

**Simple skill** (single file):
```
commit-helper/
└── SKILL.md
```

**Skill with references**:
```
pdf-processing/
├── SKILL.md           # Quick start + navigation
├── FORMS.md           # Form-filling details
└── REFERENCE.md       # API reference
```

**Skill with scripts**:
```
data-analysis/
├── SKILL.md
├── scripts/
│   └── validate.py
└── references/
    └── schema.md
```

## Key Guidelines

- Keep SKILL.md under 500 lines
- Use forward slashes in paths: `scripts/helper.py`
- Keep references one level deep from SKILL.md
- Use third person in descriptions
- Test with all models you plan to use

## Skill Locations

- **Personal**: `~/.claude/skills/skill-name/`
- **Project**: `.claude/skills/skill-name/`

For detailed patterns, see [references/patterns.md](references/patterns.md).
For anti-patterns to avoid, see [references/anti-patterns.md](references/anti-patterns.md).
