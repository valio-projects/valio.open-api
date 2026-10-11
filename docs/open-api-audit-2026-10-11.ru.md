# Аудит valio.open-api — 11 октября 2026 года

## Основание и границы проверки

Проверяется ветка codex/admin-e2e-multipart репозитория valio-projects/valio.open-api.
Исходная точка: e2cdf3a93d2101f58f627ca2575067b6e23ab91c.
История ниже — ровно 50 коммитов, достижимых из этой точки, в порядке git log -50;
это не 50 коммитов первой родительской цепочки и не 50 отдельных неповторяющихся изменений.

Исторический анализ основан на родительских связях, именах и изменениях файлов,
first-parent diff merge-коммитов и git patch-id --stable для обычных коммитов.
Проверка архитектуры основана на исходном коде Core и генераторов.
Все номера строк в разделе находок относятся к исходной точке e2cdf3a, до текущих исправлений.
Исторические сообщения о зелёной сборке не считаются доказательством текущей сборки.

Документ различает:

- подтверждённый исходным кодом дефект;
- порученное исправление, результат которого ещё требует итогового gate;
- ограничение модели возможностей, для которого нужна отдельная проработка;
- фактически выполненную локальную проверку, которая записывается в последнем разделе.

Наличие плагина, транспорта или теста не означает проверку всех сочетаний языков,
форматов, протоколов, моделей, enum и валидаторов.

### Принятый порядок поставки

Первая поставка ограничена готовым локальным C++ std артефактом и его проверкой.
После создания этого архива пользователь разрешил commit/push и продолжение работы;
перед первым коммитом координатор выполнил общий межъязыковой gate, описанный ниже.
Его результаты не меняют состав и происхождение ранее собранного архива. Исходники
именно этой C++ поставки зафиксированы в её артефакте:
новая миграция ModelsByName/EnumNames и общего builder context в неё не входит.
Её инвентаризация и план записаны в [builder-context-plan.ru.md](builder-context-plan.ru.md);
общий контекст и базовый builder пока не реализованы.

## Уточнённые требования и нормативная матрица

Пользователь расширил приёмку: проверять реальные JSON/YAML документы через production
parser, сохранять семантику композиции и её локальные ограничения, выполнять
сгенерированные валидаторы в Node и отдельно компилировать TypeScript-клиенты.
В C++ DTO должны использоваться без зависимостей от JSON и HTTP.
Эти требования дополняют исходный аудит; их выполнение подтверждается отдельными gates.

Целевые версии фиксированы, без подмены ближайшей старой версией:

| Версия | Официальная норма | Целевой вход | Требуемое доказательство |
| --- | --- | --- | --- |
| Swagger/OpenAPI 2.0 | [OpenAPI 2.0](https://spec.openapis.org/oas/v2.0.html) | JSON и YAML, swagger: 2.0; definitions и версия-специфичные constructs. | Реальный parser → IR → целевые генераторы. |
| OpenAPI 3.0.4 | [OpenAPI 3.0.4](https://spec.openapis.org/oas/v3.0.4.html) | JSON и YAML, openapi: 3.0.4. | Реальный parser → IR; nullable, allOf/oneOf/anyOf и ограничения по правилам этой версии. |
| OpenAPI 3.1.2 | [OpenAPI 3.1.2](https://spec.openapis.org/oas/v3.1.2.html) | JSON и YAML, openapi: 3.1.2. | Реальный parser → IR; JSON null и композиция с учётом диалекта схем. |
| OpenAPI 3.2.0 | [OpenAPI 3.2.0](https://spec.openapis.org/oas/v3.2.0.html) | JSON и YAML, openapi: 3.2.0. | Реальный parser → IR для явно проверяемых возможностей; прочие возможности версии перечисляются отдельно. |

Представление документа в JSON/YAML не требует таких же media types у API.
Это различие установлено в разделах Format
[OpenAPI 2.0](https://spec.openapis.org/oas/v2.0.html#format) и
[OpenAPI 3.2.0](https://spec.openapis.org/oas/v3.2.0.html#format).
Для 3.1 Schema Object опирается на JSON Schema Draft 2020-12; смена одного
значения openapi в старом fixture не доказывает поддержку диалекта.
[OpenAPI 3.1.2, Schema Object](https://spec.openapis.org/oas/v3.1.2.html#schema-object).

Приёмка этой матрицы требует:

- восьми обязательных сочетаний версия × JSON/YAML с проверкой parser diagnostics;
- отсутствия Ignore/skip/catch-and-continue, скрывающих неподдержанный reader или dialect;
- нативных для каждой версии fixtures: oneOf/anyOf не выдаются за стандартные
  constructs Swagger 2.0, а x-nullable явно отмечается как extension;
- проверки IR и наблюдаемого поведения сгенерированного кода, а не только успешного чтения;
- отдельных положительных/отрицательных значений для required/optional/nullable,
  композиции и ограничений внутри каждой ветви.

В текущем diff снят прежний пропуск reader failures в MultiVersionEquivalenceTests.
OpenApiParserMatrixTests уже перечисляет точные версии 2.0/3.0.4/3.1.2/3.2.0 × JSON/YAML
и читает отдельные файлы из tests/Fixtures/OpenApi/Parser. Есть проверки контрактов,
nullable bodies, композиции/вложенной композиции и отрицательные parser-сценарии.
Эта parser/composition matrix входит в подтверждённый Application gate 77/77;
охват конкретных генераторов и runtime остаётся отдельным доказательством.

## Последние 50 коммитов

В выборке 10 merge-коммитов и 40 обычных коммитов. Группы взаимно исключающие;
все 50 записей учтены один раз.

| Группа | Количество | Смысл и результат анализа |
| --- | ---: | --- |
| C++ W1–W8 | 8 | Регистрация профилей, общий AST, std mapper, модели/кодеки/клиенты/runtime, compile fixture и удаление повторов. Перенос общих AST-деклараций выполнен; обнаружены проблемы общего плана allOf. |
| Multipart | 6 | C#/TS form-data и structured multipart. Две пары одинаковых patch-id; поведение конкретных транспортов проверяется отдельно. |
| Интеграционные merges | 4 | EOF/multipart интеграция. Два merge-коммита не меняют дерево относительно первого родителя. |
| Wire names и query transport | 7 | Сохранение исходных имён параметров, построение wire-ключей, Next.js. В Core остались ошибки идентичности и удаления параметров. |
| C#/общее наследование | 6 | EOF, paging/query/defaults/enums/allOf и идентификаторы. Коммит blobs содержит реальные C#/TS исправления. |
| TS schemas/hooks/AST | 8 | Next.js, cache keys, ограничения параметров, inline type literals, discriminator/schema/result и тесты. Обнаружены общие ошибки нормализации и извлечения ограничений. |
| Зависимости | 4 | Два обновления зависимостей и два интегрирующих merge-коммита. |
| Инфраструктура/лицензия | 4 | Trusted publishing, LGPL, переход CI и Release-конфигурация F#. Текущий удалённый CI этим аудитом не подтверждён. |
| Cubemap/документация | 3 | Ветка Cubemap, Hungarian naming, конфигурация и описание отклонения. |

| № | SHA | Исходная тема коммита | Группа |
| ---: | --- | --- | --- |
| 1 | e2cdf3a | Merge remote-tracking branch 'origin/dependabot/npm_and_yarn/packaging/npm/valio/types/node-26.2.0' into codex/admin-e2e-multipart | Зависимости |
| 2 | 3dbd577 | Merge remote-tracking branch 'gitlab/feature/cubemap_generator_mpv1' into codex/admin-e2e-multipart | Cubemap/документация |
| 3 | bed4df5 | Merge remote-tracking branch 'origin/dependabot/nuget/microsoft-extensions-5d9c2f4fa5' into codex/admin-e2e-multipart | Зависимости |
| 4 | 7cf6f1e | Merge remote-tracking branch 'gitlab/fix/ts-nested-query-param-names' into codex/admin-e2e-multipart | Wire names и query transport |
| 5 | 5e66c28 | Merge branch 'codex/publish-valio-eof' into codex/admin-e2e-multipart | Интеграционные merges |
| 6 | aebb663 | Merge branch 'codex/admin-ts-multipart' into codex/admin-e2e-multipart | Интеграционные merges |
| 7 | 24b3b9c | Merge branch 'codex/publish-valio-multipart' into codex/admin-e2e-multipart | Multipart |
| 8 | 0f01358 | refactor(cpp): W8 audit sweep — dedupe collectors/helpers, kill suppressions, fix drain + query-model edge | C++ W1–W8 |
| 9 | 7aeee92 | refactor(cpp): principled literal rendering — kill the TEXT/QuotedString hacks (W7) | C++ W1–W8 |
| 10 | 59aa234 | test(cpp): compile fixture green on 17/20/23 + CMake step + CI + docs (W6) | C++ W1–W8 |
| 11 | b4dfc6a | feat(cpp): std profile end-to-end — clients (interface+impl), runtime, steps (W5) | C++ W1–W8 |
| 12 | 956d698 | feat(cpp): std-profile enum/model/codec builders + Core IrInheritancePlan (W4) | C++ W1–W8 |
| 13 | ee5e602 | feat(cpp): StdCppTypeMapper + StdNamingConvention for the std profile (W3) | C++ W1–W8 |
| 14 | 5b19a51 | refactor(core): promote Namespace/Class/Struct/Field nodes from C# to Core AST (W2) | C++ W1–W8 |
| 15 | c677e73 | feat(cpp): register CppGeneratorPlugin with runtime-profile options (W1) | C++ W1–W8 |
| 16 | 4693852 | merge: integrate C# generator EOF support | Интеграционные merges |
| 17 | c8fa20a | fix(csharp): Normalize generated file endings | C#/общее наследование |
| 18 | 8edabd4 | fix(typescript): support structured multipart in solid clients | Multipart |
| 19 | d48d5ec | fix(typescript): serialize multipart requests as form data | Multipart |
| 20 | 21718f2 | fix(typescript): support structured multipart in solid clients | Multipart |
| 21 | 618eb5a | fix(typescript): serialize multipart requests as form data | Multipart |
| 22 | bede7d0 | fix(csharp): Merge multipart form generation | Интеграционные merges |
| 23 | a83a48a | fix(csharp): Generate multipart form content | Multipart |
| 24 | 54c91ea | fix(nextjs): Avoid nullish coalescing header objects | Wire names и query transport |
| 25 | 32ca588 | fix(typescript): Render wire parameter keys once | Wire names и query transport |
| 26 | 8085bf1 | merge: Align wire serialization with ValIO master | Wire names и query transport |
| 27 | 00d2a0b | fix(typescript): preserve OpenAPI wire parameter names | Wire names и query transport |
| 28 | 4661a0e | blobs | C#/общее наследование |
| 29 | 53cc981 | fix(typescript): точечные «проводные» имена query-параметров в axios/nextjs | Wire names и query transport |
| 30 | 998ec23 | fix(typescript): «проводные» имена query-параметров и заголовков | Wire names и query transport |
| 31 | 153a671 | Bump Microsoft.Extensions.DependencyInjection and 6 others | Зависимости |
| 32 | 1babc46 | chore(deps-dev): bump @types/node in /packaging/npm/valio | Зависимости |
| 33 | fd1513a | ci(nuget): поддержать Trusted Publishing вместо долгоживущего API-ключа | Инфраструктура/лицензия |
| 34 | f34d4c3 | chore(license): перевести проект с MIT на LGPL-2.1 | Инфраструктура/лицензия |
| 35 | 3d1d489 | ci: переезд с GitLab CI на GitHub Actions с публикацией по веткам | Инфраструктура/лицензия |
| 36 | 00c1baf | fix(build): вернуть Release-конфигурацию проектам ValIO.Generators.FSharp | Инфраструктура/лицензия |
| 37 | d82136a | fix(csharp): имена paging-параметров, nullable enum-свойства, пустое тело | C#/общее наследование |
| 38 | 04a3457 | fix(csharp): DefaultValue типизируется по схеме, а не по CLR-типу значения | C#/общее наследование |
| 39 | ed862ba | fix(csharp): рабочая генерация клиентов — ApiException, null/array query, enum-сериализация, allOf | C#/общее наследование |
| 40 | 2568aa1 | fix(codegen): общий allOf-наследование + zod-импорты под verbatimModuleSyntax | C#/общее наследование |
| 41 | a22688f | fix: require Hungarian-notation prefix check to be case-sensitive on the following letter, repoint config to repo-relative output | Cubemap/документация |
| 42 | 1e3ecd9 | feat(typescript): TanStack cache keys, queryTransport, params constraints, fetch query-string | TS schemas/hooks/AST |
| 43 | 7179daf | feat(typescript): add Next.js (App Router) fetch transport | TS schemas/hooks/AST |
| 44 | 030707b | feat(core): add TypeLiteralNode for inline object types | TS schemas/hooks/AST |
| 45 | b8e06f6 | feat(typescript): emit z.discriminatedUnion for oneOf + discriminator | TS schemas/hooks/AST |
| 46 | 0a275b0 | feat(typescript): co-located & typed/coerced schemas + discriminable result union (BUG #6) | TS schemas/hooks/AST |
| 47 | de51382 | tests | TS schemas/hooks/AST |
| 48 | a402079 | split query & mutation imports; fix argument types; | TS schemas/hooks/AST |
| 49 | 9215b37 | nullfix; axios import types fixes | TS schemas/hooks/AST |
| 50 | 5cdb885 | docs: mark branch as CubemapGeneratorMVP1 deviation in README | Cubemap/документация |

### Что доказано о повторах истории

| Пара | Stable patch-id | Значение |
| --- | --- | --- |
| 21718f2 = 8edabd4 | 39b9ab8ee36349ca6b0df04d55c7f9a3624d412b | Одинаковое изменение structured multipart SolidStart в двух цепочках. |
| 618eb5a = d48d5ec | 2d019e399b14ce4829046c7c97ed1505a4980252 | Одинаковое изменение сериализации TypeScript FormData в двух цепочках. |

Коммиты 5e66c28 и aebb663 имеют пустой diff относительно первого родителя:
соответствующее содержимое уже присутствовало. У merge-коммитов 4693852 и bede7d0
есть изменения относительно первого родителя; они интегрируют EOF/multipart из боковых веток.
Эти факты не обосновывают переписывание опубликованной истории и не доказывают наличие
двух действующих реализаций. Исправлять нужно повторяющиеся реализации в текущем коде.

## AST, IR, команды и архитектурные границы

По декларациям исходной точки не обнаружены повторяющиеся точные имена типов,
оканчивающиеся на Node, Type или Command. Это ограниченная инвентаризация деклараций,
а не доказательство отсутствия семантически одинакового кода.

Коммит 5b19a51 действительно перенёс общие NamespaceDeclarationNode,
ClassDeclarationNode, StructDeclarationNode и FieldDeclarationNode в Core.
Параллельных одноимённых C#-деклараций в исходной точке нет.
CLI регистрирует отдельные generate, validate и init; повторение --schema или --language
в разных командах само по себе не является дублированием команды.

Разные уровни и смыслы должны оставаться явными:

- IR описывает входной контракт и семантику типов; AST описывает вывод конкретного языка.
- Class и Struct — разные синтаксические/семантические декларации.
- Optional означает отсутствие значения, Nullable — допустимый JSON null.
- Binary, ByteArray и File нельзя автоматически объединять только потому,
  что конкретный язык отображает их в одинаковый тип; wire-кодирование может различаться.

В проекте уже применяются Builder, Visitor, Strategy и последовательный Pipeline.
Практическое улучшение SOLID/DRY/GRASP — единые операции сравнения, обхода,
выделения имён и разрешения свойств IR с явными границами возможностей плагинов.
Добавление универсальных фабрик поверх существующих классов само по себе не устраняет
обнаруженные ошибки и не является критерием качества.

## Подтверждённые исходным кодом находки

Статус «изменено в текущем diff» означает наличие соответствующего изменения исходников.
Он не означает прохождение общего gate или runtime каждого потребителя.
Финальный Core gate первой C++ поставки подтверждает 108/108 без skips;
он отдельно отмечен в последнем разделе и не заменяет проверку всех языковых toolchains.
Окончательное состояние каждой находки и результаты проверок дополняет координатор.

| ID | Приоритет | Источник в e2cdf3a | Дефект и воспроизводящий сценарий | Направление исправления | Статус |
| --- | --- | --- | --- | --- | --- |
| CORE-01 | P1 | src/ValIO.Core/IR/Builders/IrBuilder.cs:1712; присваивания :547 и :625 | Имя inline-модели строится только из имени свойства. A.details с id и B.details с name перезаписывают один DetailsInlineModel; обе ссылки ведут к последней форме. Возможна коллизия с объявленным компонентом. | Стабильное выделение имён по контексту/пути, резервирование компонентных имён, переиспользование только эквивалентных форм. | Изменено в текущем diff: allocator/reservation и проверки коллизий. |
| CORE-02 | P1 | src/ValIO.Core/IR/Utilities/IrInheritancePlan.cs:58–62 | Flatten обращается к Composition! обычной базовой модели, где Composition=null. Несколько обычных баз allOf вызывают NullReferenceException. Нет защиты от циклов flatten; одиночные циклы могут породить некорректное наследование. | Терминальный случай обычной модели, контроль циклов, детерминированное разрешение diamond/override. | Изменено в текущем diff: общий cycle-safe resolver и FlattenProperties. |
| CORE-03 | P1 | src/ValIO.Core/IR/Utilities/IrTypeComparer.cs:19–27; IrCompositionNormalizer.cs:108 | Дедупликация стирает optional/nullable при сравнении. string перед nullable string способен удалить null из итогового union. Для вложенных композиционных коллекций нет полного структурного сравнения. | Разделить точное структурное равенство и осознанное сравнение без обёрток; нормализовать nullability независимо от порядка. | Изменено в текущем diff: структурное сравнение, presence policy и cardinality oneOf. |
| CORE-04 | P1 | src/ValIO.Core/IR/Builders/IrBuilder.cs:1276, :1323, :1350–1401 | Все имена параметров сравниваются без регистра; dedup зависит от наличия warnings. Удаляется легальный query id при path id. Санитизированные имена смешиваются с wire-идентичностью; порядок {user-id}/{other.id} не совпадает с sanitized lookup. | Идентичность location+OriginalName, case folding только для headers; отдельное разрешение коллизий символов; одинаковая семантика с warnings и без. | Изменено в текущем diff: wire identity, порядок OriginalName и отдельные уникальные символы. |
| CORE-05 | P2 | src/ValIO.Core/IR/Builders/IrBuilder.cs:347–368 | Presence-check не учитывает exclusiveMinimum/Maximum, поэтому exclusive-only ограничение исчезает. decimal.TryParse зависит от текущей культуры. | Проверять exclusive bounds и разбирать числа invariant; регрессии под en-US/ru-RU. | Изменено в текущем diff: exclusive presence и invariant NumberStyles.Float. |
| CORE-06 | P2 | src/ValIO.Core/IR/Builders/IrBuilder.cs:1513–1706; src/ValIO.Core/IR/Builders/IrTypeDependencyGraphBuilder.cs:19–152 | Вынесенный graph builder используется тестами, а production повторяет извлечение зависимостей/топологическую сортировку. Старый обход клиентских зависимостей собирает и выбрасывает результаты. | Production делегирует извлечённому builder; удалить копию/no-op traversal; проверить production parity. | Изменено в текущем diff: production вызывает единственный IrTypeDependencyGraphBuilder. |
| PLUGIN-01 | P2 | src/ValIO.Core/Plugins/GeneratorRegistry.cs:16–18 | Повторный LanguageId/alias другого плагина молча перезаписывает регистрацию. Текущего конфликта между зарегистрированными плагинами не найдено. | Ошибка при конфликте владельцев; разрешить повтор собственного ID в aliases того же плагина. | Изменено в текущем diff: конфликт разных владельцев вызывает InvalidOperationException. |
| LANG-01 | P1 | IrBuilder.cs:148–175 и model builders ниже | Core хранит собственные свойства в Properties, базы allOf — в Composition. Девять генераторов читают только собственные свойства и не добавляют поля баз: Base.id + Child.name теряет id. | Общий cycle-safe flatten resolver на границе генераторов без наследования; сохранить own-only IR для C#/TS/C++. | Изменено в steps девяти генераторов; ограничение unions не закрыто. |

### Межъязыковая проверка allOf

Отсутствие flatten подтверждается прямым обходом model.Properties в:

| Язык | Файл и строка исходной точки |
| --- | --- |
| Java | src/ValIO.Generators.Java/Ast/Builders/JavaRecordModelAstBuilder.cs:33 |
| Kotlin | src/ValIO.Generators.Kotlin/Ast/Builders/KotlinDataClassAstBuilder.cs:31 |
| Python | src/ValIO.Generators.Python/Ast/Builders/PydanticModelAstBuilder.cs:43 |
| Swift | src/ValIO.Generators.Swift/Ast/Builders/SwiftStructAstBuilder.cs:30 |
| Rust | src/ValIO.Generators.Rust/Ast/Builders/RustModelAstBuilder.cs:34 |
| Ruby | src/ValIO.Generators.Ruby/Ast/Builders/RubyModelAstBuilder.cs:29 |
| PHP | src/ValIO.Generators.Php/Ast/Builders/PhpModelAstBuilder.cs:31 |
| Dart | src/ValIO.Generators.Dart/Ast/Builders/DartModelAstBuilder.cs:32 |
| F# | src/ValIO.Generators.FSharp/Ast/Builders/FSharpModelAstBuilder.cs:47 |

Поиск Composition/Flatten/Inherited в исходной точке во всех этих генераторах не обнаружил обработки
AllOf; F# отдельно обрабатывает OneOf/AnyOf как DU.
Изменение только Core inheritance plan не исправляет эти потребители автоматически.
Нужны интеграционные сценарии Base/Child, multiple-base, diamond и повтор свойства.

В текущей рабочей копии steps этих девяти генераторов уже изменены для общего
разрешения свойств allOf; добавлены AllOfModelsStepTests в соответствующих проектах.
Это описание присутствующего кода, а не результат итоговой проверки или компиляции
вывода каждого native toolchain.

### Null и обход типов в остальных языках

TypeScript, Python, PHP, Dart и Ruby явно отображают IrPrimitiveKind.Null.
Java, Kotlin, Rust, Swift и F# в исходной точке попадают в общий fallback.
Kotlin Any без nullable-маркера не представляет required null-only свойство.
Это требует отдельной политики целевого языка; наличие fallback не доказывает
точность сохранения контракта.

Во многих GetImportTarget возвращается только одна ссылка из словаря.
Полный общий обход типов позволил бы собирать все вложенные ссылки единообразно,
вместо повторения switch по array/dictionary/nullable/optional/union/intersection.
Этот вывод является направлением архитектурного улучшения, а не заявлением,
что каждый импорт каждого языка уже проверен компилятором.

### OneOf, AnyOf и ограничения отдельных ветвей

oneOf требует ровно одного успешного совпадения, anyOf — хотя бы одного.
Для пересекающихся ветвей один и тот же payload должен отклоняться oneOf и приниматься
anyOf. Это нормативное различие:
[OpenAPI 3.0.4, Discriminator Object](https://spec.openapis.org/oas/v3.0.4.html#discriminator-object),
[JSON Schema 2020-12, anyOf](https://json-schema.org/draft/2020-12/json-schema-core#section-10.2.1.2),
[JSON Schema 2020-12, oneOf](https://json-schema.org/draft/2020-12/json-schema-core#section-10.2.1.3).

В текущем diff добавлен IrUnionKind с AnyOf/OneOf; IrUnionType хранит исходный
режим совпадения. Дедупликация или flatten не должны превращать oneOf[A,A]
в A: такое изменение меняет число совпавших ветвей.
В IrType.Constraints добавлены локальные ограничения схемы, включая отдельные
ветви композиции; прежних ограничений только на IrProperty для этого недостаточно.
Режим union и ограничения должны сохраняться при rewrite, сравнении и генерации
валидатора. Наличие этих полей в исходниках проверено, общий gate ещё не завершён.

Пример приёмки: anyOf строка minLength=5 либо число minimum=10.
Короткая строка и число 9 отклоняются, строка длины 5 и число 10 принимаются.
Для oneOf с пересекающимися числовыми диапазонами значение в пересечении отклоняется.
Проверяются также sibling properties/required рядом с composition и отсутствие coercion,
которое неожиданно расширяет множество допустимых JSON-значений.

Расширение Core IR не означает автоматически точные unions во всех девяти
альтернативных языках. Java, Kotlin, Swift, Rust, Ruby, Dart и F# type mappers
по-прежнему имеют широкие Object/Any/obj/JSON-value mappings для произвольных unions.
Python/PHP выводят синтаксический union, но он сам по себе не обеспечивает oneOf
exactly-one. F# имеет отдельный DU-путь для модельных OneOf/AnyOf; это не общая
runtime-валидация каждой ветви. Эти ограничения остаются открытыми и не закрываются
исправлением flatten для allOf.

## Текущее состояние изменений по C++, C# и TypeScript

Ниже перечислены присутствующие изменения и ожидаемая проверка. Точный итоговый diff,
добавленные регрессии и результаты общей приёмки дополняются после gate.

| Область | Порученная работа | Ограничение доказательств на момент подготовки раздела |
| --- | --- | --- |
| Core AST/IR | Целостность AST-потомков/родителей, IrUnionKind, локальные IrType.Constraints, общие резолверы и удаление повторов. | 108/108 passed, 0 skipped; включает добавленные Core presence/container проверки. |
| C++ | Чистые DTO/enum headers, отдельные codecs .h/.cpp, model/serializer/client CMake targets; явный отказ для неподдержанной модельной композиции. | 161/161 unit и 4/4 native; generated example CTest 2/2, models-only CTest 1/1. Точная матрица ниже. |
| C# | Nullable reference/enum/default/query и multipart, единые options, согласованность mapper/builders; явный отказ для неподдержанной модельной композиции и неоднородных success results. | Общий precommit gate: 122 passed, 0 failed, 0 skipped. Это отдельное доказательство от C++ архива. |
| TypeScript | Общая multipart-сериализация, wire-параметры, validators/AST; Node runtime Zod 3/4, Valibot, TypeBox; строгая компиляция transport matrix. | Общий suite: 706 passed, 1 baseline skip; реальные tsc/Node проверки пока имеют 5 и 4 failures соответственно, исправления выполняются. |
| Остальные языки | Совместное подключение flatten-резолвера и проверки allOf-полей. | Unit suites всех девяти генераторов пройдены в общем precommit gate; broad union mappings и отсутствующее полное native coverage остаются ограничениями. |
| SocratiCode | Индексирование valio.open-api запущено по точному пути. | Последний статус: 10%, 480/4616 файлов, 0 points, batch 1/26, 2967 секунд; индекс работает, поиск не готов. |

### Явные ошибки вместо потери неподдержанной модельной семантики

CSharpAstBuilder.BuildClass теперь отклоняет модельные композиции, которые не являются
allOf ссылками на модели. Разнородные success body types также отклоняются, если
для них нужен ещё не реализованный result union.
В C++ CppTypeReferenceCollector.EnsureSupportedComposition проверяет достижимые
базовые модели, отклоняет OneOf/AnyOf, вложенный non-reference allOf и неразрешённую базу.
Проверка используется UE/std model builders и std JSON builder.

Сообщения требуют composed value representation или typed variant/composition-aware
codec. Это намеренно более строгая граница: такие случаи завершаются
NotSupportedException вместо генерации модели, потерявшей часть исходного контракта.
Исключение не означает реализованную поддержку; точные положительные и отрицательные
fixture-сценарии принимаются только после общего gate.
Обычное allOf модельных ссылок использует общий план наследования/flatten.

### Файловые fixtures и граница доказательства property matrix

Контракты вынесены в отдельные файлы tests/Fixtures/OpenApi:
Parser, CoreIR, Inheritance, Validators, Transport и TypeScriptCompile.
tests/Shared/OpenApiFixture читает скопированные рядом с test assembly документы;
версии/схемы не заменяются текстовыми подстановками и не зависят от CWD.
Это делает конкретный вход теста доступным для самостоятельной проверки.

Добавлен IrBuilderCompositionPresenceTests: required/non-required и nullable/non-nullable
свойства проверяются отдельно для anyOf/oneOf/allOf; дополнительные сценарии проверяют
nullable array/dictionary/object и nullable вложенные значения. Эти Core проверки
входят в 108/108. Они различают отсутствие свойства и допустимый null в IR.
Полная accept/reject matrix сгенерированных моделей/валидаторов каждого языка,
включая унаследованные свойства, остаётся отдельным gate следующих этапов.
Наличие прежнего nullable body теста или одного успешного Core suite её не заменяет.

### Реальные TypeScript validators и transport compile

Проверяются установленные библиотеки Zod 3, Zod 4, Valibot и TypeBox в Node:
сгенерированные schema modules должны принимать/отклонять значения в соответствии
с исходным контрактом. Наличие строк .min(), union или oneOf в snapshot недостаточно.
TypeScript transpileModule без diagnostics также не заменяет tsc type-check.

Первоначальная transport compile матрица: axios, fetch, solid, nextjs, ky, got, ofetch,
undici и angular-rxjs. В текущем precommit запуске добавлен angular-promise:
50 случаев строгой компиляции с реальными зависимостями. Это не покрывает каждую
комбинацию дополнительных query wrappers.
Проверяются required/optional multipart, вложенные metadata и коллекции,
wire query/header names и совместимые типы запросов/ответов.

В текущей копии имеются ValidatorRuntimeIntegrationTests, ValidatorBoundaryPipelineTests
и TransportRequestSerializationRegressionTests; runtime-контракты читаются из файлов.
На последнем статическом просмотре runtime matrix ещё перечисляла 3.0.3/3.1.0,
а общий GeneratedTypeScriptRuntime мог пропускаться без VALIO_VALIDATOR_NODE_MODULES.
Production parser matrix уже использует точные 3.0.4/3.1.2. Нельзя смешивать эти два
набора доказательств. Обязательный запуск с установленными зависимостями, согласование
runtime versions и отсутствие скрытых skips остаются условиями приёмки координатора.
Перед первым коммитом координатор запустил отдельные реальные tsc/Node gates.
Их текущие результаты и оставшиеся ошибки приведены в следующем разделе;
полный TypeScript/runtime gate пока не принят.

### Межъязыковая проверка перед первым коммитом

После первой C++ поставки выполнен общий Release gate:
[precommit-general-fixed.log](../artifacts/audit/precommit-general-fixed.log).
Все 14 перечисленных suites завершились без failures. Единственный пропуск —
baseline тест TypeScript GeneratedAngularOutput_CompilesUnderTscStrict;
его нельзя считать пройденной компиляцией. Предупреждения анализаторов в логе
сохраняются; warning-clean сборка не заявляется.

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Core | 108 | 0 | 0 |
| Application | 77 | 0 | 0 |
| C++ | 161 | 0 | 0 |
| C# | 122 | 0 | 0 |
| TypeScript | 706 | 0 | 1 |
| Ruby | 59 | 0 | 0 |
| F# | 59 | 0 | 0 |
| Rust | 62 | 0 | 0 |
| Java | 112 | 0 | 0 |
| Kotlin | 105 | 0 | 0 |
| Python | 123 | 0 | 0 |
| Dart | 68 | 0 | 0 |
| PHP | 89 | 0 | 0 |
| Swift | 90 | 0 | 0 |

Девять альтернативных генераторов теперь имеют свежие unit результаты, включая
подключённые allOf регрессии. Это не означает native/runtime приёмку всех этих
языков и не снимает перечисленные выше ограничения union mappings.

Отдельные проверки с установленными реальными зависимостями ещё требуют исправлений:

| Gate | Текущий результат | Оставшаяся работа |
| --- | --- | --- |
| TypeScript strict compile | 45 passed / 5 failed / 0 skipped: [typescript-compile-precommit.log](../artifacts/audit/typescript-compile-precommit.log). | Пять случаев angular-promise: отсутствующий mode исправляется; требуется повторный запуск. |
| Node validator/runtime | 66 passed / 4 failed / 0 skipped: [typescript-runtime-precommit.log](../artifacts/audit/typescript-runtime-precommit.log). | Четыре Valibot случая отклоняют RFC3339 date-time с Z; исправление выполняется, требуется повторный запуск. |

Пользователь разрешил commit/push, но до сообщения координатора с точным SHA
документ не заявляет выполненный commit, push или успешный удалённый CI.

### Реализованные границы C++ DTO, codecs и CMake targets

Пользователь требует использовать сгенерированные DTO независимо от транспорта
и сериализатора. Конкретный контракт разделения:

1. Model/enum headers содержат DTO и необходимые стандартные C++ типы, а также
   ссылки на другие DTO. Они не включают nlohmann/json, httplib, client/runtime
   или transport headers и не объявляют JSON/HTTP API внутри модели.
2. JSON codecs имеют отдельные headers с объявлениями и отдельные implementation
   .cpp. JSON-зависимости принадлежат codec-границе, а не каждому DTO consumer.
3. CMake предоставляет отдельный model target без find_package/link к JSON/HTTP;
   codec target зависит от models и JSON; client/transport target добавляет HTTP
   и необходимые codecs. Условные зависимости подключаются только для реально
   создаваемого target.
4. Model-only TU включает сгенерированные model/enum headers, создаёт DTO и
   компилируется с доступом только к STL и каталогу models/enums; внешние JSON/HTTP
   headers и библиотеки отсутствуют в его dependency closure.
5. DTO без out-of-line поведения остаются header-only. Пустой DTO.cpp для создания
   вида разделения не генерируется; model target может быть INTERFACE.
6. Отдельный compile/runtime gate проверяет codecs/client, чтобы независимость DTO
   не скрывала несовместимость serialization ADL, wire names или optional/null semantics.

В текущем diff StdModelAstBuilder строит JSON-independent header, а
CppStdSerializersStep создаёт отдельные codecHeader/codecSource для моделей и enum.
StdRuntimeTemplates.CmakeLists теперь создаёт valio_models INTERFACE,
valio_serializers и valio_clients с условными VALIO_BUILD_SERIALIZERS/VALIO_BUILD_CLIENTS;
valio_generated остался агрегирующим INTERFACE target.
find_package JSON/HTTP расположен внутри соответствующих условных ветвей.
CppStdCmakeStep учитывает enum-only output.

Добавлен tests/ValIO.Generators.Cpp.CompileTests/fixtures/model_only.cpp,
включающий только model/enum headers; отдельная граница не требует пустых DTO.cpp.
Targets/fixture проверены не только статически: первая C++ поставка прошла
native 4/4 и отдельный models-only CTest 1/1. Полный generated example прошёл
CTest 2/2. Точные toolchain, варианты и логи приведены в последнем разделе.

## Ограничения media, протоколов и возможностей IR

Следующие ограничения подтверждены исходной точкой и не должны скрываться формулировкой
«поддержаны все языки/стэки/протоколы»:

- IrBuilder.cs:1457 выбирает первый request content type; строка 1499 — первый response
  content/schema. Альтернативные media representations целиком не сохраняются.
- IrResponse не содержит media type. Поэтому кодогенератор не может восстановить
  точную политику Content-Type/Accept/декодирования из одного response type.
- IrParameter не сохраняет style, explode и allowReserved. Сохранённое OriginalName
  решает имя wire-ключа, но не все правила сериализации массивов/объектов.
- Cookie-параметры не попадают в отдельную коллекцию IrOperation: switch в
  IrBuilder.cs:1209–1220 учитывает только path/query/header.
- Security requirements/schemes не представлены в IR операции; универсальная
  генерация аутентификации и авторизации этим контрактом не обеспечивается.
- Метаданные сохраняют первый server URL; полноценная семантика server variables
  и operation/path-specific servers этим аудитом не подтверждена.
- IsJson/IsForm в IrRequestBody проверяют ограниченный набор точных строк.
  Media parameters, регистр и vendor +json требуют явной политики.
- ITransportDescriptor в исходной точке не имеет реализаций и потребителей;
  его наличие не является действующей абстракцией протокола.
- IR ориентирован на HTTP/OpenAPI. WebSocket, streaming, event/RPC и другие
  протоколы нельзя объявлять проверенными на основании текущих HTTP-генераторов.

В частности, OpenAPI 3.2.0 описывает itemSchema/itemEncoding для последовательных
media types и streaming multipart. Чтение простого документа с openapi: 3.2.0
не подтверждает реализацию этих механизмов.
[OpenAPI 3.2.0, Media Type Object](https://spec.openapis.org/oas/v3.2.0.html#media-type-object).

Для расширения нужен явный контракт возможностей: что сохраняется в IR,
какой плагин способен вывести эту семантику и какой диагностикой сопровождается
неподдержанный случай. Это отдельная задача проектирования, а не повод молча
заменять неизвестный случай на any/object.

В зависимостях npm исходной точки @types/node обновлён до 26, тогда как CI запускает
Node 22. Это основание проверить совместимость доступных API, но не доказанный
runtime-дефект.

## Следующие этапы после первой C++ поставки

Этот список описывает конкретные следующие gates и открытые возможности. Он не
означает, что текущий diff реализовал всю спецификацию OpenAPI или все протоколы.
Следующие изменения не должны задним числом менять происхождение первого C++
артефакта. Текущая межъязыковая приёмка перед коммитом описана отдельным разделом выше.

| Приоритет | Действие | Проверяемый критерий завершения |
| --- | --- | --- |
| Следующая фаза | Реализовать принятый builder/document context план отдельным изменением. | Один контекст lookup-наборов и явные зависимости builder после миграции всех callers; поведение и файлы генерации сохранены; новый C++ gate после изменения. Сейчас выполнена только инвентаризация. |
| P0 текущей приёмки | Исправить пять angular-promise compile случаев и четыре Valibot runtime случая; повторить реальные gates. | 50/50 strict compile и 70/70 Node runtime без скрытых пропусков; обычный TypeScript unit suite не заменяет эти результаты. |
| P0 следующей приёмки | Распространить presence matrix на сгенерированные модели и валидаторы. | Собственные/унаследованные свойства покрывают required × nullable, absence/null/value и локальные ограничения; Core108 служит исходным IR-доказательством, не заменяя runtime. |
| P0 следующей приёмки | Дополнить прошедшие unit suites девяти альтернативных генераторов доступными native/runtime проверками. | Точные охваченные плагины/сценарии, passed/failed/skipped и отсутствующие toolchains записаны отдельно. |
| P0 следующей приёмки | Сделать обязательные интеграционные проверки воспроизводимыми. | Версии reader/runtime fixtures согласованы с 2.0/3.0.4/3.1.2/3.2.0; зависимости установлены; отсутствие требуемого toolchain не превращается в успех. |
| P1 | Спроектировать typed composed values/codecs для C#/C++ с явной cardinality. | Heterogeneous/nested OneOf/AnyOf имеют сохраняющую контракт representation и accept/reject regressions; до этого остаётся явная NotSupportedException. |
| P1 | Заменить broad union fallbacks в альтернативных языках по отдельным plugin slices. | Для каждой заявленной возможности есть целевой тип, codec/validator, compiler/runtime evidence и diagnostics для ещё неподдержанных случаев. allOf flatten не используется как доказательство union support. |
| P1 | Ввести сохраняемые wire capabilities вместо потери media/style/security/cookie metadata. | IR хранит необходимые media alternatives/encoding/parameter metadata; каждый выбранный transport либо реализует их, либо выдаёт точную диагностику. Нужны version-specific fixtures. |
| P1 | Отдельно определить поддерживаемый объём 3.2 streaming и иных протоколов. | Список поддержанных constructs и runtime scenarios принят явно; простое чтение openapi: 3.2.0 не считается поддержкой itemSchema/itemEncoding или WebSocket/RPC. |
| P2 | Унифицировать полные IR traversal/import collections и расширить проверки имён. | Вложенные dictionary/union/intersection references собираются без потерь; совпадения wire имён отделены от symbol conflicts; нет независимых копий graph/flatten алгоритма. |
| P2 | Дождаться готовности SocratiCode и подтвердить поиск. | Индекс именно C:\Work\valio\valio.open-api завершён, доступны ожидаемые файлы и контрольный запрос возвращает корректный путь/фрагмент. Текущие 10% и 0 points не являются search proof. |

## Итоговые проверки первой C++ поставки

Результаты относятся к локальной Windows x64 поставке C++ std из текущей рабочей
копии на базе e2cdf3a с незакоммиченными изменениями. Исполнитель документа прочитал
указанные лог-файлы, но сам не запускал сборки и не менял production-код.
Этот раздел фиксирует происхождение и проверки первого архива; более поздний общий
precommit gate описан выше. Опубликованный commit и удалённый CI этим не подтверждены.

Артефакт: [artifacts/releases/valio-cpp-win-x64-20261011](../artifacts/releases/valio-cpp-win-x64-20261011).
Внутри cli — self-contained win-x64 CLI на .NET 10, example — готовый пример и
build.ps1, validation — доказательства проверок, BUILD-INFO.json — версии и hashes.
Готовый архив: [valio-cpp-win-x64-20261011.zip](../artifacts/releases/valio-cpp-win-x64-20261011.zip),
43 125 253 байта. SHA256:
`f378d1c771f7c8339c3a30693c393295d9a45b9d34e09cd7be0f85abc3b951b3`;
[контрольная сумма](../artifacts/releases/valio-cpp-win-x64-20261011.zip.sha256).

| Gate | Статус и доказательство |
| --- | --- |
| История: ровно 50 коммитов, 10 merges / 40 non-merges | Проверено локально относительно e2cdf3a. |
| Повторяющиеся patch-id | Проверены две пары; значения приведены выше. |
| Инвентаризация точных имён AST/IR/Command | В исходной точке повторов не найдено в описанной выборке деклараций. |
| Core | 108 passed / 0 failed / 0 skipped: [core-cpp-release.log](../artifacts/audit/core-cpp-release.log). |
| Application | 77 passed / 0 failed / 0 skipped: [application-cpp-final.log](../artifacts/audit/application-cpp-final.log). Включает production parser matrix 4 версии × JSON/YAML, композиции и три Bootstrap сценария. |
| Production parser versions/formats | 2.0 / 3.0.4 / 3.1.2 / 3.2.0 × JSON/YAML входят в Application gate; это не полная сертификация всех constructs этих версий. |
| IrUnionKind, IrType.Constraints и presence/container IR | Входят в прошедший Core gate; runtime всех языков этим не подтверждён. |
| C++ unit | 161 passed / 0 failed / 0 skipped: [cpp-ready-unit.log](../artifacts/audit/cpp-ready-unit.log). |
| Сгенерированный C++ native | 4 passed / 0 failed / 0 skipped: [cpp-native-offline.log](../artifacts/audit/cpp-native-offline.log). Варианты перечислены ниже. |
| Windows CLI publish | Self-contained win-x64 publish выполнен: [cpp-cli-ready-publish.log](../artifacts/audit/cpp-cli-ready-publish.log). Лог содержит предупреждения; warning-clean сборка не заявляется. |
| Реальная генерация готовым CLI | 22 файла generated и 5 definition-only файлов: [cpp-ready-generation.log](../artifacts/audit/cpp-ready-generation.log). |
| Полный generated CMake/example | Сборка и CTest 2/2: [cpp-example-cmake-build.log](../artifacts/audit/cpp-example-cmake-build.log), [cpp-example-ctest.log](../artifacts/audit/cpp-example-ctest.log). |
| Models-only CMake/example | Сборка и CTest 1/1 без JSON/HTTP dependency linkage: [cpp-example-model-build.log](../artifacts/audit/cpp-example-model-build.log), [cpp-example-model-ctest.log](../artifacts/audit/cpp-example-model-ctest.log). |
| Готовый example/build.ps1 | Проверен исполнением: [cpp-example-script.log](../artifacts/audit/cpp-example-script.log). |
| Переносимость готового архива | Координатор проверил hash, распаковку и CLI/codec из пути с пробелами; CLI generation подтверждён [cpp-archive-portable.log](../artifacts/audit/cpp-archive-portable.log). |
| C#/TypeScript full gates | В первую C++ поставку не входили. Позднее C#122 и TypeScript706+1skip прошли общий precommit gate; strict compile45/50 и Node runtime66/70 требуют исправлений и повторного запуска. |
| Девять альтернативных генераторов | В первую C++ поставку не входили. Позднее unit suites всех девяти пройдены в precommit-general-fixed.log; полное native coverage не подтверждено. |
| Общий builder/document context | Только инвентаризация и план; не реализован, следующая фаза. |
| SocratiCode | Индексация запущена: последний статус координатора 10%, 480/4616, 0 points, batch 1/26, elapsed 2967 s. Индекс работает; поиск не готов. |
| git diff --check | Пройден по результату координатора. Рабочая копия содержит незакоммиченные изменения. |
| Commit/push/удалённый CI | Commit и push не выполнялись; публикация в remote и удалённый CI не заявляются. |

Native gate выполнен MSVC из Visual Studio 18 2026 Enterprise с CMake 4.4.3,
с точными зависимостями Conan httplib 0.18.3 и nlohmann_json 3.12.0:

| C++ standard | API style | Client shape | Результат |
| --- | --- | --- | --- |
| 17 | both | interface+impl | Native compile и HTTP/codec round-trip passed. |
| 20 | sync | class | Native compile и HTTP/codec round-trip passed. |
| 23 | both | interface+impl | Native compile и HTTP/codec round-trip passed. |
| 17 | async | class | Native compile и HTTP/codec round-trip passed. |

Это четыре конкретных сочетания, а не полный декартов продукт всех options.
По BUILD-INFO.json bundled httplib собран без TLS; локальный HTTP smoke не
подтверждает HTTPS/TLS. C++ UE runtime native gate этой поставкой также не заявляется.
Старые логи не заменяют итоговые: cpp-example-generation.log содержит промежуточный
неуспешный запуск, а успешное доказательство генерации — cpp-ready-generation.log.
