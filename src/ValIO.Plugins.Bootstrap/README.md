# ValIO.Plugins.Bootstrap

A single DI helper, `AddAllGeneratorPlugins(this IServiceCollection)`, that registers every generator
plugin in one place so the CLI, Web and Desktop frontends don't each duplicate the list.

When you add a new language plugin, register it here (`PluginsBootstrapExtensions.cs`).
