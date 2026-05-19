namespace ValIO.Generators.TypeScript.Models;

/// <summary>
/// Целевой фреймворк для TanStack Query обёрток.
/// React использует существующий полнофункциональный <c>TanStackQueryHookAstBuilder</c>;
/// Angular/Solid/Svelte используют лёгкие обёртки (<c>injectQuery</c>/<c>createQuery</c>) с
/// единственной парой <c>useFoo</c>/<c>getFooQueryKey</c>.
/// </summary>
public enum TanStackFramework
{
    /// <summary>@tanstack/react-query — полные хуки с infinite, prefetch, options.</summary>
    React,
    /// <summary>@tanstack/angular-query-experimental — injectQuery/injectMutation.</summary>
    Angular,
    /// <summary>@tanstack/solid-query — createQuery/createMutation внутри геттера () =&gt; ({ ... }).</summary>
    Solid,
    /// <summary>@tanstack/svelte-query — createQuery/createMutation с обычным объектом.</summary>
    Svelte,
}
