# Task Completion Guidelines

## When Task is Completed

### Build and Validation
1. **Build the project**: Run `dotnet build` to ensure no compilation errors
2. **Run tests**: Execute `dotnet test` to verify all tests pass
3. **Check for specific test projects**: Only run tests for projects existing in the `tests/` directory:
   - `tests/KyoshinEewViewer.Tests/`
   - `tests/KyoshinEewViewer.JmaXmlParser.Tests/`
   - `tests/KyoshinEewViewer.DCReportParser.Tests/`

### Code Quality
- **No linting/formatting commands found**: The project uses .editorconfig for style enforcement
- **Follow .editorconfig rules**: Tab indentation for C#, proper naming conventions
- **Japanese content**: Ensure all user-facing content and logs are in Japanese

### Implementation Policies
- **DRY Principle**: However, prioritize readability for short code
- **No TODO Left Behind**: Except when instructed otherwise
- **Avoid Unnecessary Abstraction**: Don't create excessive abstraction layers
- **Early Return**: Use guard clauses to improve readability

### Important Notes
- **Never commit changes unless explicitly asked**
- **Test modifications**: Carefully judge the validity of implementation changes
- **File format**: Ensure all files end with a newline character
- **Requirements**: Always confirm with users when specifications are unclear