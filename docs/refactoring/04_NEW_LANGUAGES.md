# 04 — Новые языки: PHP, Python, Java, Kotlin, Swift, Dart, C#/F# RestSharp

> **Этап:** 4  
> **Приоритет:** Средний  
> **Зависит от:** 02_PLUGIN_ARCHITECTURE (Plugin Architecture)

---

## Содержание

1. [Общий шаблон нового языкового генератора](#1-общий-шаблон-нового-языкового-генератора)
2. [PHP: Guzzle и Laravel HTTP Client](#2-php-guzzle-и-laravel-http-client)
3. [Python: aiohttp, HTTPX Sync, HTTPX Async](#3-python-aiohttp-httpx-sync-httpx-async)
4. [C# / F#: RestSharp](#4-c--f-restsharp)
5. [Java: Unirest, AsyncHttp, OkHttp](#5-java-unirest-asynchttp-okhttp)
6. [Kotlin: OkHttp](#6-kotlin-okhttp)
7. [Swift: NSURLSession](#7-swift-nsurlsession)
8. [Dart: package:http и Dio](#8-dart-packagehttp-и-dio)
9. [Общая структура AST для новых языков](#9-общая-структура-ast-для-новых-языков)

---

## 1. Общий шаблон нового языкового генератора

Каждый новый язык следует одному шаблону:

### Что нужно создать для каждого языка

```
ValIO.Generators.[Lang]/
├── [Lang]GeneratorPlugin.cs       ← IGeneratorPlugin
├── [Lang]OptionsDescriptor.cs     ← IGeneratorOptionsDescriptor
├── [Lang]TypeMapper.cs            ← IrType → [Lang]Type string
├── [Lang]NamingConverter.cs       ← naming conventions (PascalCase, snake_case, etc.)
│
├── Ast/
│   ├── Nodes/                     ← язык-специфичные AST ноды
│   ├── Builders/[Lang]AstBuilder.cs  ← IR → AST
│   ├── Renderers/[Lang]AstRenderer.cs ← AST → string (код)
│   └── Visitors/I[Lang]AstVisitor.cs
│
├── Steps/
│   ├── [Lang]ModelsStep.cs        ← ICodeGenerationStep
│   ├── [Lang]EnumsStep.cs
│   └── [Transport]ClientStep.cs   ← для каждого транспорта
│
└── ValIO.Generators.[Lang].csproj
```

### Алгоритм TypeMapper (IR → язык-специфичный тип)

```csharp
// Базовый интерфейс (уже есть в Core)
public interface ITypeMapper
{
    string MapType(IrType irType);
}

// Пример для PHP:
public class PhpTypeMapper : ITypeMapper
{
    public string MapType(IrType irType) => irType switch
    {
        IrPrimitiveType pt => pt.Kind switch
        {
            IrPrimitiveKind.String  => "string",
            IrPrimitiveKind.Integer => "int",
            IrPrimitiveKind.Float   => "float",
            IrPrimitiveKind.Boolean => "bool",
            IrPrimitiveKind.Date    => "\\DateTimeInterface",
            IrPrimitiveKind.Binary  => "string",  // base64
            _ => "mixed"
        },
        IrArrayType arr   => "array",             // PHP: array (или typed array doc-comment)
        IrModelReference ref => ref.ModelName,    // MyModel
        IrNullableType n  => $"?{MapType(n.InnerType)}",
        IrOptionalType o  => MapType(o.InnerType),
        IrUnionType u     => "mixed",
        _ => "mixed"
    };
}
```

---

## 2. PHP: Guzzle и Laravel HTTP Client

**Проект:** `ValIO.Generators.PHP`  
**Поддерживаемые транспорты:** `guzzle`, `laravel-http`

### 2.1 Guzzle

#### Генерируемый код (models)

```php
<?php

declare(strict_types=1);

namespace App\Api\Models;

class User
{
    public function __construct(
        public readonly string $id,
        public readonly string $name,
        public readonly string $email,
        public readonly ?string $avatar = null,
    ) {}

    public static function fromArray(array $data): self
    {
        return new self(
            id: $data['id'],
            name: $data['name'],
            email: $data['email'],
            avatar: $data['avatar'] ?? null,
        );
    }

    public function toArray(): array
    {
        return [
            'id' => $this->id,
            'name' => $this->name,
            'email' => $this->email,
            'avatar' => $this->avatar,
        ];
    }
}
```

#### Генерируемый код (Guzzle client)

```php
<?php

declare(strict_types=1);

namespace App\Api\Clients;

use GuzzleHttp\Client;
use GuzzleHttp\Exception\GuzzleException;
use App\Api\Models\User;
use App\Api\Models\CreateUserRequest;

class UsersApiClient
{
    public function __construct(
        private readonly Client $client,
        private readonly string $baseUrl = '',
    ) {}

    /**
     * @throws GuzzleException
     */
    public function getUser(string $id): User
    {
        $response = $this->client->get("{$this->baseUrl}/api/users/{$id}", [
            'headers' => ['Accept' => 'application/json'],
        ]);

        return User::fromArray(json_decode($response->getBody()->getContents(), true));
    }

    /**
     * @throws GuzzleException
     */
    public function createUser(CreateUserRequest $request): User
    {
        $response = $this->client->post("{$this->baseUrl}/api/users", [
            'headers' => [
                'Accept'       => 'application/json',
                'Content-Type' => 'application/json',
            ],
            'json' => $request->toArray(),
        ]);

        return User::fromArray(json_decode($response->getBody()->getContents(), true));
    }
}
```

### 2.2 Laravel HTTP Client

```php
<?php

declare(strict_types=1);

namespace App\Api\Clients;

use Illuminate\Support\Facades\Http;
use Illuminate\Http\Client\Response;
use App\Api\Models\User;

class UsersApiClient
{
    public function __construct(
        private readonly string $baseUrl = '',
    ) {}

    public function getUser(string $id): User
    {
        $response = Http::acceptJson()
            ->get("{$this->baseUrl}/api/users/{$id}");

        $response->throw();

        return User::fromArray($response->json());
    }

    public function createUser(CreateUserRequest $request): User
    {
        $response = Http::acceptJson()
            ->asJson()
            ->post("{$this->baseUrl}/api/users", $request->toArray());

        $response->throw();

        return User::fromArray($response->json());
    }
}
```

### PHP AST ноды

```csharp
// PHP-специфичные AST ноды

// PHP class (с constructor promotion)
public sealed class PhpClassNode : AstNode
{
    public string Namespace { get; }
    public string Name { get; }
    public IReadOnlyList<PhpUseNode> Uses { get; }
    public IReadOnlyList<PhpConstructorPromotedParamNode> ConstructorParams { get; }
    public IReadOnlyList<PhpMethodNode> Methods { get; }
    // ...
}

// Constructor parameter с promotion (public readonly string $name)
public sealed class PhpConstructorPromotedParamNode : AstNode
{
    public string Visibility { get; }   // "public", "private", "protected"
    public bool IsReadonly { get; }
    public string Type { get; }
    public string Name { get; }
    public AstNode? DefaultValue { get; }
}

// PHP method
public sealed class PhpMethodNode : AstNode
{
    public string Name { get; }
    public string Visibility { get; }
    public IReadOnlyList<PhpParameterNode> Parameters { get; }
    public string ReturnType { get; }
    public IReadOnlyList<AstNode> Body { get; }
    public IReadOnlyList<string> DocCommentLines { get; }
}

// PHP nullable type (?string)
public sealed class PhpNullableTypeNode : TypeNode
{
    public string BaseType { get; }
    // Рендерится как "?{BaseType}"
}
```

---

## 3. Python: aiohttp, HTTPX Sync, HTTPX Async

**Проект:** `ValIO.Generators.Python`  
**Поддерживаемые транспорты:** `requests`, `httpx-sync`, `httpx-async`, `aiohttp`

### 3.1 Модели (Pydantic v2)

```python
# models/user.py
from pydantic import BaseModel
from typing import Optional
from datetime import datetime


class User(BaseModel):
    id: str
    name: str
    email: str
    avatar: Optional[str] = None
    created_at: datetime

    model_config = {"populate_by_name": True}
```

### 3.2 HTTPX Sync

```python
# clients/users_api_client.py
from typing import Optional
import httpx
from ..models.user import User
from ..models.create_user_request import CreateUserRequest


class UsersApiClient:
    def __init__(
        self,
        client: httpx.Client,
        base_url: str = "",
    ) -> None:
        self._client = client
        self._base_url = base_url

    def get_user(self, id: str) -> User:
        response = self._client.get(
            f"{self._base_url}/api/users/{id}",
            headers={"Accept": "application/json"},
        )
        response.raise_for_status()
        return User.model_validate(response.json())

    def create_user(self, request: CreateUserRequest) -> User:
        response = self._client.post(
            f"{self._base_url}/api/users",
            json=request.model_dump(by_alias=True, exclude_none=True),
            headers={"Accept": "application/json"},
        )
        response.raise_for_status()
        return User.model_validate(response.json())
```

### 3.3 HTTPX Async

```python
# clients/users_api_client_async.py
import httpx
from ..models.user import User
from ..models.create_user_request import CreateUserRequest


class UsersApiClientAsync:
    def __init__(
        self,
        client: httpx.AsyncClient,
        base_url: str = "",
    ) -> None:
        self._client = client
        self._base_url = base_url

    async def get_user(self, id: str) -> User:
        response = await self._client.get(
            f"{self._base_url}/api/users/{id}",
            headers={"Accept": "application/json"},
        )
        response.raise_for_status()
        return User.model_validate(response.json())

    async def create_user(self, request: CreateUserRequest) -> User:
        response = await self._client.post(
            f"{self._base_url}/api/users",
            json=request.model_dump(by_alias=True, exclude_none=True),
        )
        response.raise_for_status()
        return User.model_validate(response.json())
```

### 3.4 aiohttp Async

```python
# clients/users_api_client_aiohttp.py
import aiohttp
from ..models.user import User
from ..models.create_user_request import CreateUserRequest


class UsersApiClient:
    def __init__(
        self,
        session: aiohttp.ClientSession,
        base_url: str = "",
    ) -> None:
        self._session = session
        self._base_url = base_url

    async def get_user(self, id: str) -> User:
        async with self._session.get(
            f"{self._base_url}/api/users/{id}",
            headers={"Accept": "application/json"},
        ) as response:
            response.raise_for_status()
            data = await response.json()
            return User.model_validate(data)

    async def create_user(self, request: CreateUserRequest) -> User:
        async with self._session.post(
            f"{self._base_url}/api/users",
            json=request.model_dump(by_alias=True, exclude_none=True),
        ) as response:
            response.raise_for_status()
            data = await response.json()
            return User.model_validate(data)
```

### Python TypeMapper

```csharp
public class PythonTypeMapper : ITypeMapper
{
    public string MapType(IrType irType) => irType switch
    {
        IrPrimitiveType pt => pt.Kind switch
        {
            IrPrimitiveKind.String      => "str",
            IrPrimitiveKind.Integer     => "int",
            IrPrimitiveKind.Int64       => "int",
            IrPrimitiveKind.Float       => "float",
            IrPrimitiveKind.Boolean     => "bool",
            IrPrimitiveKind.Date        => "datetime.date",
            IrPrimitiveKind.DateTime    => "datetime",
            IrPrimitiveKind.Binary      => "bytes",
            IrPrimitiveKind.Uuid        => "uuid.UUID",
            _ => "Any"
        },
        IrArrayType arr      => $"list[{MapType(arr.ElementType)}]",
        IrDictionaryType d   => $"dict[{MapType(d.KeyType)}, {MapType(d.ValueType)}]",
        IrNullableType n     => $"Optional[{MapType(n.InnerType)}]",
        IrOptionalType o     => $"Optional[{MapType(o.InnerType)}]",
        IrModelReference ref => ref.ModelName,
        IrUnionType u        => $"Union[{string.Join(", ", u.Types.Select(MapType))}]",
        _ => "Any"
    };
}
```

---

## 4. C# / F#: RestSharp

**Проект:** `ValIO.Generators.CSharp.RestSharp`  
**Транспорт ID:** `restsharp`

### Генерируемый C# код (RestSharp)

```csharp
// UsersApiClient.RestSharp.cs
using RestSharp;
using RestSharp.Serializers.Json;

namespace MyApp.Api.Clients;

public sealed class UsersApiClient : IUsersApiClient, IDisposable
{
    private readonly RestClient _client;

    public UsersApiClient(string baseUrl)
    {
        var options = new RestClientOptions(baseUrl)
        {
            ThrowOnDeserializationError = true,
        };
        _client = new RestClient(options, configureSerialization: s =>
            s.UseSystemTextJson(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }));
    }

    public async Task<User> GetUserAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var request = new RestRequest("/api/users/{id}")
            .AddUrlSegment("id", id);

        return await _client.GetAsync<User>(request, cancellationToken)
            ?? throw new InvalidOperationException("Response was null");
    }

    public async Task<User> CreateUserAsync(
        CreateUserRequest body,
        CancellationToken cancellationToken = default)
    {
        var request = new RestRequest("/api/users")
            .AddJsonBody(body);

        return await _client.PostAsync<User>(request, cancellationToken)
            ?? throw new InvalidOperationException("Response was null");
    }

    public void Dispose() => _client.Dispose();
}
```

### F# вариант

```fsharp
// UsersApiClient.fs
module UsersApiClient

open RestSharp
open System.Threading

let createClient (baseUrl: string) =
    new RestClient(baseUrl)

let getUserAsync (client: RestClient) (id: string) (ct: CancellationToken) =
    task {
        let request = RestRequest("/api/users/{id}").AddUrlSegment("id", id)
        let! result = client.GetAsync<User>(request, ct)
        return result
    }
```

---

## 5. Java: Unirest, AsyncHttp, OkHttp

**Проект:** `ValIO.Generators.Java`

### Java TypeMapper

```csharp
public class JavaTypeMapper : ITypeMapper
{
    private readonly bool _useBoxedTypes;  // Integer vs int

    public string MapType(IrType irType) => irType switch
    {
        IrPrimitiveType pt => pt.Kind switch
        {
            IrPrimitiveKind.String   => "String",
            IrPrimitiveKind.Integer  => _useBoxedTypes ? "Integer" : "int",
            IrPrimitiveKind.Int64    => _useBoxedTypes ? "Long" : "long",
            IrPrimitiveKind.Float    => _useBoxedTypes ? "Double" : "double",
            IrPrimitiveKind.Boolean  => _useBoxedTypes ? "Boolean" : "boolean",
            IrPrimitiveKind.Date     => "java.time.LocalDate",
            IrPrimitiveKind.DateTime => "java.time.OffsetDateTime",
            IrPrimitiveKind.Binary   => "byte[]",
            IrPrimitiveKind.Uuid     => "java.util.UUID",
            _ => "Object"
        },
        IrArrayType arr      => $"java.util.List<{MapType(arr.ElementType)}>",
        IrDictionaryType d   => $"java.util.Map<{MapType(d.KeyType)}, {MapType(d.ValueType)}>",
        IrNullableType n     => MapType(n.InnerType),  // Java использует nullable аннотации
        IrModelReference ref => ref.ModelName,
        IrUnionType u        => "Object",
        _ => "Object"
    };
}
```

### 5.1 Unirest

```java
// UsersApiClient.java
package com.example.api.clients;

import kong.unirest.Unirest;
import kong.unirest.HttpResponse;
import com.example.api.models.User;
import com.example.api.models.CreateUserRequest;

public class UsersApiClient {
    private final String baseUrl;

    public UsersApiClient(String baseUrl) {
        this.baseUrl = baseUrl;
    }

    public User getUser(String id) {
        HttpResponse<User> response = Unirest.get(baseUrl + "/api/users/{id}")
            .routeParam("id", id)
            .header("Accept", "application/json")
            .asObject(User.class);

        if (!response.isSuccess()) {
            throw new RuntimeException("HTTP Error: " + response.getStatus());
        }
        return response.getBody();
    }

    public User createUser(CreateUserRequest request) {
        HttpResponse<User> response = Unirest.post(baseUrl + "/api/users")
            .header("Content-Type", "application/json")
            .header("Accept", "application/json")
            .body(request)
            .asObject(User.class);

        if (!response.isSuccess()) {
            throw new RuntimeException("HTTP Error: " + response.getStatus());
        }
        return response.getBody();
    }
}
```

### 5.2 OkHttp

```java
// UsersApiClient.java (OkHttp)
package com.example.api.clients;

import okhttp3.*;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.example.api.models.User;

import java.io.IOException;

public class UsersApiClient {
    private static final MediaType JSON = MediaType.get("application/json; charset=utf-8");

    private final OkHttpClient client;
    private final ObjectMapper objectMapper;
    private final String baseUrl;

    public UsersApiClient(OkHttpClient client, ObjectMapper objectMapper, String baseUrl) {
        this.client = client;
        this.objectMapper = objectMapper;
        this.baseUrl = baseUrl;
    }

    public User getUser(String id) throws IOException {
        Request request = new Request.Builder()
            .url(baseUrl + "/api/users/" + id)
            .header("Accept", "application/json")
            .get()
            .build();

        try (Response response = client.newCall(request).execute()) {
            if (!response.isSuccessful()) throw new IOException("HTTP Error: " + response.code());
            return objectMapper.readValue(response.body().string(), User.class);
        }
    }
}
```

### 5.3 AsyncHttp (Async HTTP Client)

```java
// UsersApiClient.java (AsyncHttpClient)
import org.asynchttpclient.*;
import java.util.concurrent.CompletableFuture;

public class UsersApiClient {
    private final AsyncHttpClient client;
    private final ObjectMapper objectMapper;
    private final String baseUrl;

    public CompletableFuture<User> getUser(String id) {
        return client.prepareGet(baseUrl + "/api/users/" + id)
            .setHeader("Accept", "application/json")
            .execute()
            .toCompletableFuture()
            .thenApply(response -> {
                try {
                    return objectMapper.readValue(response.getResponseBody(), User.class);
                } catch (Exception e) {
                    throw new RuntimeException(e);
                }
            });
    }
}
```

---

## 6. Kotlin: OkHttp

**Проект:** `ValIO.Generators.Kotlin`  
**Транспорт ID:** `okhttp`

### Генерируемый код

```kotlin
// UsersApiClient.kt
package com.example.api.clients

import com.fasterxml.jackson.module.kotlin.jacksonObjectMapper
import com.fasterxml.jackson.module.kotlin.readValue
import com.example.api.models.User
import com.example.api.models.CreateUserRequest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody

class UsersApiClient(
    private val client: OkHttpClient = OkHttpClient(),
    private val baseUrl: String = "",
) {
    private val mapper = jacksonObjectMapper()
    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    fun getUser(id: String): User {
        val request = Request.Builder()
            .url("$baseUrl/api/users/$id")
            .header("Accept", "application/json")
            .get()
            .build()

        return client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) error("HTTP Error: ${response.code}")
            mapper.readValue(response.body!!.string())
        }
    }

    fun createUser(body: CreateUserRequest): User {
        val requestBody = mapper.writeValueAsString(body)
            .toRequestBody(jsonMediaType)

        val request = Request.Builder()
            .url("$baseUrl/api/users")
            .header("Accept", "application/json")
            .post(requestBody)
            .build()

        return client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) error("HTTP Error: ${response.code}")
            mapper.readValue(response.body!!.string())
        }
    }
}
```

### Kotlin модели (data classes)

```kotlin
// models/User.kt
package com.example.api.models

import com.fasterxml.jackson.annotation.JsonProperty

data class User(
    @JsonProperty("id")
    val id: String,

    @JsonProperty("name")
    val name: String,

    @JsonProperty("email")
    val email: String,

    @JsonProperty("avatar")
    val avatar: String? = null,
)
```

---

## 7. Swift: NSURLSession

**Проект:** `ValIO.Generators.Swift`  
**Транспорт ID:** `nsurlsession`

### Генерируемые модели (Codable)

```swift
// Models/User.swift
import Foundation

public struct User: Codable {
    public let id: String
    public let name: String
    public let email: String
    public let avatar: String?

    public init(
        id: String,
        name: String,
        email: String,
        avatar: String? = nil
    ) {
        self.id = id
        self.name = name
        self.email = email
        self.avatar = avatar
    }

    enum CodingKeys: String, CodingKey {
        case id
        case name
        case email
        case avatar
    }
}
```

### Генерируемый клиент (NSURLSession)

```swift
// Clients/UsersApiClient.swift
import Foundation

public final class UsersApiClient {
    private let session: URLSession
    private let baseURL: URL
    private let decoder: JSONDecoder
    private let encoder: JSONEncoder

    public init(
        baseURL: URL,
        session: URLSession = .shared
    ) {
        self.baseURL = baseURL
        self.session = session
        self.decoder = JSONDecoder()
        self.decoder.dateDecodingStrategy = .iso8601
        self.encoder = JSONEncoder()
        self.encoder.dateEncodingStrategy = .iso8601
    }

    public func getUser(id: String) async throws -> User {
        let url = baseURL.appendingPathComponent("/api/users/\(id)")
        var request = URLRequest(url: url)
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let (data, response) = try await session.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse,
              (200..<300).contains(httpResponse.statusCode) else {
            throw APIError.invalidResponse
        }

        return try decoder.decode(User.self, from: data)
    }

    public func createUser(body: CreateUserRequest) async throws -> User {
        let url = baseURL.appendingPathComponent("/api/users")
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.httpBody = try encoder.encode(body)

        let (data, response) = try await session.data(for: request)

        guard let httpResponse = response as? HTTPURLResponse,
              (200..<300).contains(httpResponse.statusCode) else {
            throw APIError.invalidResponse
        }

        return try decoder.decode(User.self, from: data)
    }
}

public enum APIError: Error {
    case invalidResponse
    case decodingFailed(Error)
}
```

---

## 8. Dart: package:http и Dio

**Проект:** `ValIO.Generators.Dart`  
**Поддерживаемые транспорты:** `dart-http`, `dio`

### Dart модели (json_serializable)

```dart
// lib/models/user.dart
import 'package:json_annotation/json_annotation.dart';

part 'user.g.dart';

@JsonSerializable()
class User {
  const User({
    required this.id,
    required this.name,
    required this.email,
    this.avatar,
  });

  final String id;
  final String name;
  final String email;
  final String? avatar;

  factory User.fromJson(Map<String, dynamic> json) => _$UserFromJson(json);
  Map<String, dynamic> toJson() => _$UserToJson(this);
}
```

### 8.1 package:http

```dart
// lib/clients/users_api_client.dart
import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/user.dart';
import '../models/create_user_request.dart';

class UsersApiClient {
  UsersApiClient({
    required this.baseUrl,
    http.Client? client,
  }) : _client = client ?? http.Client();

  final String baseUrl;
  final http.Client _client;

  Future<User> getUser(String id) async {
    final response = await _client.get(
      Uri.parse('$baseUrl/api/users/$id'),
      headers: {'Accept': 'application/json'},
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('HTTP Error: ${response.statusCode}');
    }

    return User.fromJson(json.decode(response.body) as Map<String, dynamic>);
  }

  Future<User> createUser(CreateUserRequest request) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/api/users'),
      headers: {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
      },
      body: json.encode(request.toJson()),
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('HTTP Error: ${response.statusCode}');
    }

    return User.fromJson(json.decode(response.body) as Map<String, dynamic>);
  }
}
```

### 8.2 Dio

```dart
// lib/clients/users_api_client_dio.dart
import 'package:dio/dio.dart';
import '../models/user.dart';
import '../models/create_user_request.dart';

class UsersApiClient {
  UsersApiClient({
    required String baseUrl,
    Dio? dio,
  }) : _dio = dio ?? Dio(BaseOptions(baseUrl: baseUrl));

  final Dio _dio;

  Future<User> getUser(String id) async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/users/$id',
      options: Options(headers: {'Accept': 'application/json'}),
    );

    return User.fromJson(response.data!);
  }

  Future<User> createUser(CreateUserRequest request) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/users',
      data: request.toJson(),
      options: Options(headers: {'Accept': 'application/json'}),
    );

    return User.fromJson(response.data!);
  }
}
```

---

## 9. Общая структура AST для новых языков

Все новые языки имеют базовую общую структуру:

```csharp
// Каждый язык определяет свои ноды, но они следуют общим паттернам:

// Файл-нода (аналог SourceFileNode для TS, CompilationUnitNode для C#)
public sealed class PhpFileNode : AstNode { ... }
public sealed class PythonFileNode : AstNode { ... }
public sealed class JavaCompilationUnitNode : AstNode { ... }
public sealed class KotlinFileNode : AstNode { ... }
public sealed class SwiftFileNode : AstNode { ... }
public sealed class DartFileNode : AstNode { ... }

// Класс-нода
public sealed class PhpClassNode : AstNode { ... }
public sealed class PythonClassNode : AstNode { ... }
public sealed class JavaClassNode : AstNode { ... }
// ...

// Метод-нода
public sealed class PhpMethodNode : AstNode { ... }
public sealed class PythonMethodNode : AstNode { ... }
// ...
```

### Переиспользование базовых нод

Многие базовые ноды из Core уже могут использоваться:

| Core нода | Используется в |
|-----------|---------------|
| `SimpleTypeNode` | Все языки |
| `ArrayTypeNode` | Все языки |
| `GenericTypeNode` | Java, Kotlin, C# |
| `NullableTypeNode` | C#, Kotlin, Swift, Dart |
| `LiteralExpressionNode` | Все языки |
| `CallExpressionNode` | Все языки |
| `IdentifierNode` | Все языки |
| `CommentNode` | Все языки |
| `ReturnStatementNode` | Все языки |
| `IfStatementNode` | Все языки |
| `BlockNode` | Все языки |

Язык-специфичные ноды нужны для: классов с декораторами, конструкторов, модификаторов доступа, async/await синтаксиса конкретного языка, импортов/namespace/пакетов.

---

## Конфигурация

```json
{
  "projects": {
    "php-api": {
      "language": "php",
      "schema": "./openapi.json",
      "output": "./src/Api",
      "options": {
        "transport": "guzzle",
        "namespace": "App\\Api",
        "generateFromArray": true
      }
    },
    "python-api": {
      "language": "python",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transport": "httpx-async",
        "packageName": "myapp.api"
      }
    },
    "java-api": {
      "language": "java",
      "schema": "./openapi.json",
      "output": "./src/main/java",
      "options": {
        "transport": "okhttp",
        "packageName": "com.example.api"
      }
    },
    "kotlin-api": {
      "language": "kotlin",
      "schema": "./openapi.json",
      "output": "./src/main/kotlin",
      "options": {
        "transport": "okhttp",
        "packageName": "com.example.api"
      }
    },
    "swift-api": {
      "language": "swift",
      "schema": "./openapi.json",
      "output": "./Sources/Api",
      "options": {
        "transport": "nsurlsession",
        "modulePrefix": "MyApp"
      }
    },
    "dart-api": {
      "language": "dart",
      "schema": "./openapi.json",
      "output": "./lib/api",
      "options": {
        "transport": "dio",
        "packageName": "myapp_api"
      }
    }
  }
}
```
