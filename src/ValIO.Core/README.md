# ValIO.Core

The foundation library shared by every other ValIO project. It is **language-agnostic** and has no
generation logic of its own.

## Contains

- **IR** (`IR/`) — the Intermediate Representation (`IrDocument`, `IrModel`, `IrEnum`, `IrOperation`,
  the `IrType` hierarchy, `IrPropertyConstraints`) plus `IrBuilder`, which turns a parsed OpenAPI
  document into IR.
- **AST** (`Ast/`) — base, language-agnostic AST nodes (`AstNode`, `DeclarationNode`, `CommentNode`,
  `InterfaceDeclarationNode`, …) and the visitor contracts.
- **Plugins** (`Plugins/`) — the generator contracts: `IGeneratorPlugin`, `ICodeGenerationStep`,
  `ICodeGenerationPipeline`, `GenerationContext`, `GeneratedFile`, `IGeneratorOptionsDescriptor`,
  `OptionDescription`.
- **Interfaces / Models / Utilities** — `INamingConverter`, `FileOrganization`, `CommentGenerator`,
  and other shared helpers.

See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md) for how the IR and AST are used.
