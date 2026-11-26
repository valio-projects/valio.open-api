# AST generation inventory

The registered language set comes from [PluginsBootstrapExtensions.AddAllGeneratorPlugins](../../src/ValIO.Plugins.Bootstrap/PluginsBootstrapExtensions.cs). Each plug-in's `CreatePipeline` determines the executable steps. Common model, enum and client switches gate their respective steps. Profile and library switches gate the additional outputs below.

| Target | Pipeline and output | Syntax producer | Verification entry point |
| --- | --- | --- | --- |
| C# | `CSharpModelsStep`, `CSharpEnumsStep`, `CSharpClientStep` for models, enums, interfaces, HTTP clients, telemetry and API support. `CSharpSignalRStep` for hub bases and clients | `CSharpAstBuilder` and `CSharpSignalRAstBuilder` produce compilation units. `CSharpRuntimeSource` attaches the configured namespace as a Roslyn syntax node to complete checked-in runtime sources | `ValIO.Generators.CSharp.Tests`, `ValIO.Application.Tests/SignalRGenerationTests.cs`, [live probe](../../samples/signalr-pipeline/README.md) |
| TypeScript | `TypeScriptModelsStep`, `TypeScriptEnumsStep`, `TypeScriptParamsStep`, `TypeScriptResultsStep`, `TypeScriptUuidFormatsStep`, `TypeScriptQueryKeysStep`, `TypeScriptSignalRStep`, `TypeScriptIndexStep` | Source, declaration, type, expression and module-export nodes. The barrel uses AST export metadata registered by output producers | `ValIO.Generators.TypeScript.Tests` with the installed Node runtime dependencies |
| TypeScript validators | `ZodSchemasStep`, `ValibotSchemasStep`, `TypeBoxSchemasStep`, `TypeScriptParamsSchemasStep` and `TypeScriptResultsSchemasStep` | Schema source builders produce calls, constraints, declarations, type aliases and exports | TypeScript validators and strict compilation suites, including Zod 3 and 4 |
| TypeScript transports | Fetch, Axios, Ky, Got, OFetch, Undici, Angular RxJS and Promise, SolidStart and Next.js steps | Transport builders produce HTTP request statements, typed calls and return expressions | TypeScript native transport and runtime tests |
| TypeScript integrations | TanStack React, Vue, Angular, Svelte and Solid steps, plus MSW | Hook and handler builders produce declared functions, types and call expressions | TypeScript integration and generated compiler suites |
| C++ standalone | `CppStdModelsStep`, `CppStdEnumsStep`, `CppStdSerializersStep`, `CppStdClientStep`, `CppStdRuntimeStep`, `CppStdCmakeStep` | Standard builders produce C++ headers, sources, typed calls and definitions. A CMake tree represents commands and conditions. Runtime headers use typed imports and alias declarations with fixed implementation resources | `ValIO.Generators.Cpp.Tests`, `ValIO.Generators.Cpp.CompileTests` |
| C++ Unreal | `CppUeModelsStep`, `CppUeEnumsStep`, `CppUeEndpointsStep` for models, enums, parameter and result types, Blueprint wrappers and native endpoint sources | Unreal model and endpoint AST builders with structured engine declarations and function bodies | `ValIO.Generators.Cpp.Tests`, Unreal SDK build when available |
| Dart | `DartModelsStep`, `DartEnumsStep`, `DioClientStep` | Dart class, field, parameter, enum and Dio call trees | `ValIO.Generators.Dart.Tests` |
| F# | `FSharpModelsStep`, `FSharpEnumsStep`, `RestSharpClientStep` | F# record, enum and computation-expression trees | `ValIO.Generators.FSharp.Tests` with `VALIO_FSHARP_FSI` and `VALIO_RESTSHARP_ASSEMBLY` |
| Java | `JavaModelsStep`, `JavaEnumsStep`, `OkHttpClientStep` | Java records, enums, annotations and OkHttp call trees. Gradle files are complete fixed resources | `ValIO.Generators.Java.Tests` with `VALIO_JAVAC` and `VALIO_JAVA_CLASSPATH` |
| Kotlin | `KotlinModelsStep`, `KotlinEnumsStep`, `OkHttpClientStep` | Kotlin declarations, coroutine, callback, error and serialization trees | `ValIO.Generators.Kotlin.Tests`, Kotlin compiler and OkHttp fixture |
| PHP | `PhpModelsStep`, `PhpEnumsStep`, `GuzzleClientStep` | PHP declarations, route interpolation and Guzzle call nodes. Composer manifest is a complete fixed resource | `ValIO.Generators.Php.Tests`, `php -l` |
| Python | `PythonModelsStep`, `PythonEnumsStep`, `HttpxClientStep` | Python Pydantic models, enums, package exports and HTTPX request trees | `ValIO.Generators.Python.Tests` with a Python environment containing HTTPX and Pydantic |
| Ruby | `RubyModelsStep`, `RubyEnumsStep`, `FaradayClientStep` | Ruby model attributes, constructor and enum declarations, Faraday block and request expressions | `ValIO.Generators.Ruby.Tests`, `ruby -c` |
| Rust | `RustModelsStep`, `RustEnumsStep`, `ReqwestClientStep` | Rust structs, enums and Reqwest statements, including fallible operations | `ValIO.Generators.Rust.Tests` |
| Swift | `SwiftModelsStep`, `SwiftEnumsStep`, `URLSessionClientStep` | Swift declarations, URL construction, async calls and Codable response handling | `ValIO.Generators.Swift.Tests` |

## Compare a mixed contract

The mixed fixture combines [HTTP requests and models](../../tests/Fixtures/OpenApi/NativeClients/requests-3.1.json) with [two SignalR hubs and their schemas](../../tests/Fixtures/OpenApi/SignalR/support.json). Generate the two C++ profiles and the remaining eleven languages with the same document and configuration. Compare normalized output paths with the baseline at `75ddf87` and review wire and public types with the suites above.

The observed file sets matched for twelve of the thirteen profiles. Standalone C++ added `Runtime/ValIOHttpBackend.h`, `Runtime/ValIOJsonBackend.h` and `Runtime/ValIOJsonTypesBase.h` so the fixed implementation can include the configured backend. Native UUID and temporal configurations also emit the headers for their selected provider. The application DTO headers retain their independence from the HTTP and JSON implementations.

## Verification

- `ValIO.sln` builds with .NET SDK 10.0.400. The build includes CLI, Web and Desktop.
- The SignalR sample compiles both SDKs and checks the live loopback exchange, nullable positional arguments, typed results and individual handler removal. The application suite also compares generation from equivalent JSON and YAML.
- The TypeScript suite runs 984 tests with the pinned Node dependencies. It exercises strict compilation and validator execution.
- Java and F# native suites run generated clients through OkHttp and RestSharp. Python tests run generated HTTPX and Pydantic code. Generated Dart, Kotlin and Rust request fixtures compile and exercise local HTTP success and a 404. The Dart probe uses a caller-owned Dio interceptor to deserialize the model.
- CMake and MSVC build and run the four standalone HTTP variants and seven scalar provider variants. The generated Unreal source is covered by the generator suite. The Unreal SDK is absent from the local test environment.
- PHP and Ruby generated sources pass their respective native syntax checks. Request and response assertions for these targets use the generator suites. Swift has generator suite coverage, with the native Swift build pending.
- The fixed runtime files exercised by the mixed C++ profile and the Java/PHP build manifests match their embedded sources byte for byte.
