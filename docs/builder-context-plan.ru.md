# Общий контекст IR для генераторов

Статус: проверенная по исходникам инвентаризация и план миграции. Общий контекст и базовый builder пока не реализованы. Документ не меняет текущий контракт генерации. Первая обязательная точка завершения текущей работы — готовый C++ артефакт и его нативная проверка; перенос контекста выполняется после этой проверки отдельным изменением.

## Обнаруженные повторения

Сейчас [GenerationContext](../src/ValIO.Core/Plugins/GenerationContext.cs) содержит IR-документ и изменяемые результаты прогона, но не содержит общих индексов документа. Шаги заново строят словари моделей и наборы enum. Это повторение подготовки данных, а не дополнительные узлы IR: словари ссылаются на те же модели.

| Генератор | Повторная подготовка | Файлы миграции |
| --- | --- | --- |
| C++ | `PrepareInputs` каждый раз создаёт `EnumNames` и словарь моделей. std-шаги дополнительно создают enum-набор для builder и ещё один для mapper. UE-шаги повторяют подготовку. | `Cpp/Ast/Builders/CppAstBuilder.cs`, `Cpp/Steps/CppStdStepBase.cs`, `CppStdModelsStep.cs`, `CppStdSerializersStep.cs`, `CppStdClientStep.cs`, `CppUeStepBase.cs`, `CppUeModelsStep.cs`, `CppUeEndpointsStep.cs` |
| C# | `CSharpAstBuilder` имеет три публичных изменяемых свойства `ModelsByName`, `EnumNames`, `EnumsByName`. `Build(document)` заполняет их, затем отдельно создаёт ещё один словарь моделей. `CSharpModelsStep` повторяет настройку этих свойств. | `CSharp/Ast/Builders/CSharpAstBuilder.cs`, `CSharp/Steps/CSharpModelsStep.cs`, `CSharpClientStep.cs` |
| TypeScript | `TypeScriptModelsStep`, `TypeScriptClientStepBase`, Angular и три validator-шагa создают словари моделей. Transport builders получают их через setters. `Build(document)` также создаёт локальный словарь. | `TypeScript/Ast/Builders/TypeScriptAstBuilder.cs`, `TypeScript/Steps/TypeScriptModelsStep.cs`, `TypeScriptClientStepBase.cs`, `AngularRxjsClientStep.cs`, `ZodSchemasStep.cs`, `ValibotSchemasStep.cs`, `TypeBoxSchemasStep.cs` и transport builders |
| Dart | Model-шаг создаёт словарь перед `IrInheritancePlan.FlattenProperties`. | `Dart/Steps/DartModelsStep.cs` |
| F# | То же. | `FSharp/Steps/FSharpModelsStep.cs` |
| Java | То же; собственная обработка порядка моделей. | `Java/Steps/JavaModelsStep.cs` |
| Kotlin | Model-шаг создаёт словарь и набор известных имён. OkHttp-шаг отдельно создаёт такой же набор имён моделей и enum. | `Kotlin/Steps/KotlinModelsStep.cs`, `OkHttpClientStep.cs` |
| PHP | Model-шаг создаёт словарь перед flattening; собственная обработка порядка моделей. | `Php/Steps/PhpModelsStep.cs` |
| Python | Model-шаг создаёт словарь перед flattening; модельный и HTTP-шаги отдельно готовят карты модулей моделей и enum. | `Python/Steps/PythonModelsStep.cs`, `HttpxClientStep.cs`, `PythonStepHelper.cs` |
| Ruby | Model-шаг создаёт словарь перед flattening. | `Ruby/Steps/RubyModelsStep.cs` |
| Rust | То же. | `Rust/Steps/RustModelsStep.cs` |
| Swift | То же. | `Swift/Steps/SwiftModelsStep.cs` |

В таблице пути генераторов сокращены относительно `src/ValIO.Generators.<Language>/`. При миграции Python карты Python-модулей остаются языковым сервисом: имя модуля зависит от naming convention и структуры пакета.

Дополнительные точки повторения:

- `StdCppTypeMapper` и `UETypeMapper` требуют предварительного вызова `SetEnumNamesForDocument`; `CppAstBuilder` добавляет собственный метод пересылки. Отсутствие вызова меняет классификацию ссылочного типа.
- TypeScript transport builders Ky, Undici, OFetch, Got, Angular, Nextjs и SolidStart, а также основной builder для Fetch/Axios имеют `ModelsByName`. SolidStart пересылает setter вложенному builder. Общий `TypeScriptClientStepBase` тоже хранит изменяемый словарь.
- Zod имеет `ModelsByName` одновременно в AST builder и schema renderer. Valibot и TypeBox создают индексы в своих шагах. Во всех случаях нужен один и тот же поиск модели для текущего документа.
- В TypeScript есть 14 отдельных полей `_orig`: основной builder, builders параметров и результатов, builders их схем, пять transport builders, MSW, Zod renderer, Valibot и TypeBox. Они наполняются через общий `OriginalNameMap`, но остаются изменяемыми накопителями каждого экземпляра.
- `OriginalNameMap.AddOperation` записывает `MethodName` в тот же строковый словарь, что и имена моделей и enum. Области имён операции и типа различны. Общий индекс должен иметь отдельные домены идентичности, а не расширять этот универсальный словарь.

Полезные существующие общие механизмы уже находятся в Core: `IrInheritancePlan`, `IrInheritanceResolver`, `HttpAstBuilder` и граф зависимостей типов. Новая абстракция должна использовать их, а не создавать второй механизм наследования, обхода или нормализации IR.

## Контракт общего контекста

Предлагаемый `IrDocumentContext` создаётся один раз на запрос генерации, после окончательного преобразования имён IR и до первого шага pipeline. Он передаётся через `GenerationContext` всем шагам и их builders.

| Член | Контракт |
| --- | --- |
| `Document` | Единственный исходный `IrDocument` данного прогона. |
| `ModelsByName` | `FrozenDictionary<string, IrModel>`, `StringComparer.Ordinal`, ссылки на модели документа. |
| `EnumsByName` | `FrozenDictionary<string, IrEnum>`, тот же comparer и тот же принцип ссылок. |
| `EnumNames` | `FrozenSet<string>` из ключей `EnumsByName`; производный индекс для существующих membership-проверок. |
| `ModelsInDependencyOrder` | Неизменяемая последовательность моделей по существующему графу с однократным fallback в исходном порядке для отсутствующих в графе моделей. Используется только там, где такой порядок уже нужен. |

`KnownTypeNames` можно добавить как объединение имён моделей и enum при переносе Kotlin, если применение подтверждено. Для него не нужен новый вид IR-узла. Наборы, словари и порядок вычисляются из документа однократно. Новые модели, свойства и операции через контекст не создаются.

Обязательные инварианты:

1. Дубликат имени внутри моделей или enum и пересечение имён модели и enum завершают создание контекста понятной ошибкой. Пустой словарь не служит неявным значением «контекст ещё не настроен».
2. Все индексы одного контекста относятся к одному экземпляру документа и используют ordinal-сравнение. Локальные правила wire-имён, например case-insensitive HTTP-заголовки, к индексам объявлений не применяются.
3. Создание контекста не меняет имена, состав типов, ограничения, nullability, optionality, порядок альтернатив `oneOf` и multiplicity его ветвей.
4. Поле `OriginalName` берётся из модели или enum. Для операции используется её собственный `OriginalMethodName`; при необходимости индекс операций должен иметь составной ключ с областью клиента, а не общий ключ с типами.
5. Неподдерживаемую языком композицию по-прежнему отклоняет соответствующий mapper/builder с диагностикой. Наличие индекса не разрешает молча отбрасывать ветви `allOf`/`anyOf`/`oneOf`.

Текущий IR использует `IReadOnlyList`, но это не доказывает глубокую неизменяемость: под интерфейсом может находиться `List`, а `OriginalSchema` ссылается на OpenAPI-объект. Frozen-индексы защищают их структуру, но не делают весь объектный граф immutable. Для первой миграции нужен явный контракт владения: после создания контекста pipeline не меняет документ и schema. Полное замораживание коллекций IR — отдельная задача, если потребуется внешняя конкурентная передача документа.

В контекст входных данных не входят renderer, mapper, options, активный транспорт, предупреждения, результаты генерации, `StringBuilder` или импорты конкретного выходного файла. `GenerationContext.GeneratedFiles`, `Warnings` и `StepData` остаются результатами одного прогона. Состояние `TypeScriptSourceFileBuilder` с импортами и декларациями остаётся состоянием одного файла.

## Общий builder и границы переиспользования

Первый шаг — внедрить общий контекст через конструктор существующих builders и mappers. Тогда корректная настройка индексов становится обязательной при создании, а публичные setters и последовательность `SetEnumNamesForDocument` исчезают.

Тонкий `AstBuilderBase` оправдан только там, где после этой миграции действительно повторяются поля и конструкторная проверка. Его ответственность — защищённое read-only свойство контекста и доступ к трём общим индексам. Naming и mapper передаются как отдельные зависимости соответствующего языка. Stateless `HttpAstBuilder` и builders, которым не нужен документ, не обязаны наследоваться от него.

В общий базовый класс не переносятся:

- создание language-specific AST-узлов, namespace/import/package и синтаксические особенности C++, C#, TypeScript;
- конкретные типы HTTP-клиентов, transport API, serializer API и validator API;
- выбор scalar-типов, форматов даты/времени, enum representation, правила optional/null и обработка неподдерживаемых конструкций;
- универсальный `BuildModel` с динамическим результатом, набором флагов всех языков или `object` в контракте.

Общий обход ссылок `IrType` может быть чистым сервисом Core с типизированным результатом. Кандидаты на унификацию — обход графа зависимостей, `CppTypeReferenceCollector` и обход `OriginalNameMap`. Перед переносом нужно сопоставить их назначение: сбор только объявлений и обход свойств моделей отличаются от рекурсивного обхода оболочек конкретного типа. Языковые import/module-решения остаются адаптерами результата такого обхода.

Переиспользуемый контракт свойств уже представлен `IrProperty`, ограничений — `IrPropertyConstraints`, операций — `IrOperation`, типов — иерархией `IrType`. Общий контекст предоставляет доступ к этим объектам без параллельных «common model/property/command» DTO. Новые поведенческие интерфейсы добавляются для проверенного повторения, а не по одному на каждый существующий класс.

## Жизненный цикл и конкурентность

[GenerationOrchestrator](../src/ValIO.Application/Services/GenerationOrchestrator.cs) документирован как stateless singleton, безопасный для конкурентных запросов. Он создаёт новый `GenerationContext` и pipeline для каждого вызова. Поэтому сами setters в builders пока не доказывают существующую гонку: большинство builders создаются внутри своего шага. Но они допускают неполную настройку, повторное использование с прежним документом и будущую ошибочную singleton-регистрацию. Constructor injection read-only контекста устраняет эти риски.

Отдельно обнаружено реально общее изменяемое состояние renderers:

| Renderer | Регистрация | Изменяемое состояние |
| --- | --- | --- |
| `CSharpAstRenderer` | Singleton в `PluginsBootstrapExtensions.AddAllGeneratorPlugins` | `_sb` и `_indentLevel`; `Render` очищает и наполняет общий буфер. |
| `CppAstRenderer` | Singleton в `CppGeneratorPlugin.ConfigureServices` | `_indentLevel`; `Render` сбрасывает поле, visitor-методы увеличивают и уменьшают его. |

Это доказанный по исходникам риск пересечения двух вызовов генерации через общий service provider. Runtime-воспроизведение гонки в рамках этой инвентаризации не выполнялось. Исправление: renderer создаётся на pipeline/прогон либо хранит всё изменяемое состояние в локальном объекте одного вызова `Render`. Простая замена singleton на transient недостаточна при продолжении reuse renderer из долгоживущего pipeline; контракт использования тоже должен быть явным.

Статические naming/mapping-сервисы без изменяемого состояния могут оставаться singleton. Массовое изменение lifetime всех плагинов и сервисов не требуется. `TypeScriptAstBuilder._activeEndpointTransport` тоже следует сделать параметром операции или отдельным неизменяемым transport context при переносе этого builder; текущее изменение поля с восстановлением в `finally` не обеспечивает reentrancy.

## Последовательность миграции

1. Завершить текущую проверку C++ артефакта, зафиксировать локальные результаты native build и codec round-trip. Не менять рабочую генерацию во время этой проверки.
2. Добавить `src/ValIO.Core/IR/IrDocumentContext.cs`, подключить в `Core/Plugins/GenerationContext.cs` и `Application/Services/GenerationOrchestrator.cs`. Проверить уникальность имён, ссылки на исходные объекты, порядок и отсутствие изменения IR.
3. Перенести C++: `PrepareInputs`, std/UE steps, `StdCppTypeMapper`, `UETypeMapper`, фасад и document-aware builders. Сохранить язык, профиль, имена файлов и диагностики. Удалить setters и повторные наборы только после перевода всех callers.
4. Исправить lifetime/state C++ renderer и повторить native gate. Контекст не должен сам по себе менять C++ output.
5. Перенести C# model/client paths на один контракт контекста, удалить три setters, проверить прямой `Build(document)` и pipeline. Отдельно исправить lifetime/state C# renderer.
6. Перенести TypeScript model/client base, Angular, восемь transport steps, Zod/Valibot/TypeBox и maps исходных имён. Удалить domain collision между именами операций и типов. Транспорт остаётся отдельной зависимостью.
7. Перенести девять остальных model steps, затем Kotlin known names и Python language-specific module services. Общий порядок моделей применяется только в шагах с соответствующим текущим контрактом.
8. Добавить базовый builder только по остаточному повторению после миграции. Если constructor injection уже устранил дубли, отдельный base class не обязателен.

## Проверка результата

Нужны проверки поведения, а не тесты, повторяющие `ToDictionary`:

- два разных документа с одинаковым именем, но разными model/enum определениями: генерация через один orchestrator параллельно должна давать результаты своих изолированных прогонов без смешения контента;
- общий контекст действительно один для всех шагов запроса; ссылки lookup равны соответствующим объектам документа;
- отсутствие ручной инициализации builder невозможно либо завершается явной ошибкой; прямой builder и plugin pipeline одинаково разрешают enum/default/inheritance;
- fixtures с вложенными композициями, scoped operation names, affixes, nullable/optional контейнерами и исходными wire-именами сохраняют смысл и имена файлов;
- C++ native compilation и round-trip, C# compilation, TypeScript compilation и существующие Node-тесты validators/transports проходят после своего этапа миграции;
- `git diff --check` и аудит оставшихся setters/локальных document lookups подтверждают завершённость переноса, но сами по себе не заменяют генерацию и исполнение артефактов.

Инвентаризация выполнена статически по исходникам всех 12 генераторов. В этом этапе production-код, API builders и lifetimes сервисов не изменялись; сборка и конкурентные runtime-тесты не запускались.
