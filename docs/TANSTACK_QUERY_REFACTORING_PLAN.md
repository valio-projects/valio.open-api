# План доработки генерации TanStack Query хуков

## ✅ Выполненные задачи

### 1. Mutation Hooks - Variables как объект
- ✅ Созданы методы `BuildMutationVariablesType` и `BuildMutationVariablesDestructuring`
- ✅ Изменен `BuildMutationHookParameters` - теперь возвращает только `options` и `queryClient`
- ✅ Обновлены `RenderMutationOptionsHelper` и `RenderMutationHookFunction` - используют правильную структуру с variables
- ✅ Mutation hooks теперь принимают `options?: { mutation?: UseMutationOptions<...> }` и `queryClient?: QueryClient`
- ✅ Variables формируются как объект типа `{ id: string, params?: ..., data: ... }`
- ✅ mutationFn деструктурирует variables: `const { id, params, data } = props ?? {};`

### 2. Query Function - Signal передача
- ✅ Создан метод `BuildQueryFnClientMethodParameters` - добавляет signal, timeout, axiosInstance к параметрам
- ✅ Обновлены все query functions (useQuery, useInfiniteQuery, prefetch) - теперь queryFn принимает `{ signal }` и передает signal в клиентский метод

### 3. useInfiniteQuery
- ✅ Добавлены `initialPageParam: 0` и `getNextPageParam: (lastPage) => lastPage.nextCursor` (обязательные параметры в TanStack Query v5)
- ✅ queryFn правильно принимает `{ signal }` и передает его в клиентский метод

### 4. Query Key функция
- ✅ Query key функция корректно формирует массив с path и параметрами
- ✅ Тип возврата `: QueryKey =>` явно указан (лучше, чем в старой реализации)

### 5. Query Options Helper
- ✅ Query options helper корректно генерируется с правильной типизацией
- ✅ Использует `QueryFunction` тип правильно

### 6. Типы QueryKey и QueryFunction
- ✅ QueryKey используется правильно во всех местах
- ✅ QueryFunction используется правильно во всех query функциях
- ✅ MutationFunction используется правильно в mutation hooks

## Итоговая структура

### Mutation Hook
```typescript
export const getXxxMutationOptions = <TError = Error, TContext = unknown>(
  options?: {
    mutation?: UseMutationOptions<XxxAwaitedReturnType, TError, {
      id: string;
      params?: XxxParams;
      data: XxxData;
    }, unknown>;
  },
  queryClient?: QueryClient
) : UseMutationOptions<...> => {
  const mutationKey = ["xxx"];
  const { mutation: mutationOptions } = options ? ... : { mutation: { mutationKey } };
  
  const mutationFn: MutationFunction<...> = (props) => {
    const { id, params, data } = props ?? {};
    return xxx(id, params, data);
  };
  
  return { mutationFn, ...mutationOptions };
};

export function useXxx<TError = Error, TContext = unknown>(
  options?: { ... },
  queryClient?: QueryClient
) : UseMutationResult<...> {
  const mutationOptions = getXxxMutationOptions(options);
  return useMutation(mutationOptions, queryClient);
}
```

### Query Hook
```typescript
export const getXxxQueryKey = (
  id: string,
  params?: XxxParams
): QueryKey => [
  '/api/xxx',
  id,
  params
];

export const getXxxQueryOptions = <TData = XxxAwaitedReturnType, TError = Error>(
  id: string,
  params?: XxxParams,
  options?: {
    query?: UseQueryOptions<XxxAwaitedReturnType, TError>;
  }
) : UseQueryOptions<...> & { queryKey: QueryKey } => {
  const queryKey = getXxxQueryKey(id, params);
  const queryFn: QueryFunction<XxxAwaitedReturnType> = ({ signal }) => {
    return xxx(id, params, signal, timeout, axiosInstance);
  };
  
  return {
    queryKey,
    queryFn,
    enabled: true,
    ...options
  };
};

export function useXxx<TData = XxxAwaitedReturnType, TError = Error>(
  id: string,
  params?: XxxParams,
  options?: {
    query?: UseQueryOptions<XxxAwaitedReturnType, TError>;
  }
) : UseQueryResult<TData, TError> {
  const queryKey = getXxxQueryKey(id, params);
  const queryFn: QueryFunction<XxxAwaitedReturnType> = ({ signal }) => {
    return xxx(id, params, signal, timeout, axiosInstance);
  };
  
  return useQuery<XxxAwaitedReturnType, TError, TData>({
    queryKey,
    queryFn,
    ...options
  });
}
```

### Infinite Query Hook
```typescript
export function useXxxInfinite<TData = InfiniteData<XxxAwaitedReturnType, unknown>, TError = Error>(
  id: string,
  params?: XxxParams,
  options?: {
    query?: UseInfiniteQueryOptions<...>;
  }
) : UseInfiniteQueryResult<TData, TError> {
  const queryKey = getXxxQueryKey(id, params);
  const queryFn: QueryFunction<XxxAwaitedReturnType, QueryKey, unknown> = ({ signal }) => {
    return xxx(id, params, signal, timeout, axiosInstance);
  };
  
  return useInfiniteQuery<XxxAwaitedReturnType, TError, TData, QueryKey, unknown>({
    queryKey,
    queryFn,
    initialPageParam: 0,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    enabled: true,
    ...options
  });
}
```

## Сравнение со старой реализацией

### Что улучшено:
1. ✅ Mutation variables теперь явно типизированы как объект
2. ✅ Query functions правильно принимают и передают signal
3. ✅ useInfiniteQuery включает обязательные параметры v5 (initialPageParam, getNextPageParam)
4. ✅ QueryKey тип явно указан в query key функции
5. ✅ Все типы (QueryKey, QueryFunction, MutationFunction) используются правильно

### Что может отличаться:
- Query key функция имеет явный тип возврата `: QueryKey =>` (в старой реализации его нет, но это улучшение)
- Query options helper может иметь немного другую структуру параметров, но функционально эквивалентен
