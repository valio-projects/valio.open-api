using Scriban;
using Scriban.Runtime;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация рендеринга шаблонов на основе Scriban
/// </summary>
public class TemplateRenderer : ITemplateRenderer
{
    /// <summary>
    /// Рендеринг шаблона из строки
    /// </summary>
    public string Render(string template, object model)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Template cannot be null or empty", nameof(template));

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        try
        {
            var scribanTemplate = Template.Parse(template);
            if (scribanTemplate.HasErrors)
            {
                var errors = string.Join(", ", scribanTemplate.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template parsing errors: {errors}");
            }

            var scriptObject = CreateScriptObject(model);
            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            return scribanTemplate.Render(context);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to render template: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Рендеринг шаблона из файла
    /// </summary>
    public async Task<string> RenderFileAsync(string templatePath, object model)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
            throw new ArgumentException("Template path cannot be null or empty", nameof(templatePath));

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template file not found: {templatePath}");

        var templateContent = await File.ReadAllTextAsync(templatePath);
        return Render(templateContent, model);
    }

    /// <summary>
    /// Создает ScriptObject из модели для использования в Scriban
    /// </summary>
    private static ScriptObject CreateScriptObject(object model)
    {
        var scriptObject = new ScriptObject();

        if (model == null)
            return scriptObject;

        var type = model.GetType();

        // Добавляем все публичные свойства
        foreach (var property in type.GetProperties())
        {
            var value = property.GetValue(model);
            scriptObject[property.Name] = value;
        }

        // Добавляем все публичные поля
        foreach (var field in type.GetFields())
        {
            var value = field.GetValue(model);
            scriptObject[field.Name] = value;
        }

        // Если модель - словарь, добавляем все его элементы
        if (model is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                scriptObject[key] = entry.Value;
            }
        }

        return scriptObject;
    }
}

