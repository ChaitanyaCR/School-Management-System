using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Encodings.Web;
using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolMs.Infrastructure.Configuration;

namespace SchoolMs.Infrastructure.Email;

/// <summary>
/// Renders Liquid email templates embedded in a module assembly: <c>{name}.html.liquid</c> (wrapped in the shared
/// layout) and <c>{name}.txt.liquid</c>. Values are HTML-encoded in the HTML part, so user-entered text such as
/// names cannot inject markup. Every template also receives <c>product_name</c>.
/// </summary>
public sealed class EmailTemplates(IOptions<AppOptions> appOptions)
{
    private const string LayoutName = "_layout.html.liquid";

    private static readonly FluidParser Parser = new();
    private static readonly ConcurrentDictionary<(Assembly, string), IFluidTemplate> Cache = new();

    public async Task<(string Html, string Text)> RenderAsync(
        Assembly assembly,
        string name,
        IReadOnlyDictionary<string, object?> model)
    {
        var values = new Dictionary<string, object?>(model) { ["product_name"] = appOptions.Value.ProductName };

        var body = await Get(assembly, $"{name}.html.liquid").RenderAsync(Context(values), HtmlEncoder.Default);
        var html = await Get(typeof(EmailTemplates).Assembly, LayoutName)
            .RenderAsync(Context(new Dictionary<string, object?>(values) { ["content"] = body }), HtmlEncoder.Default);
        var text = await Get(assembly, $"{name}.txt.liquid").RenderAsync(Context(values), NullEncoder.Default);

        return (html, text);
    }

    private static TemplateContext Context(IReadOnlyDictionary<string, object?> values)
    {
        var context = new TemplateContext();
        foreach (var (key, value) in values)
        {
            context.SetValue(key, value);
        }

        return context;
    }

    private static IFluidTemplate Get(Assembly assembly, string fileName) =>
        Cache.GetOrAdd((assembly, fileName), static key =>
        {
            var (assembly, fileName) = key;
            var resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith("." + fileName, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Email template '{fileName}' is not embedded in {assembly.GetName().Name}.");

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            return Parser.TryParse(reader.ReadToEnd(), out var template, out var error)
                ? template
                : throw new InvalidOperationException($"Email template '{fileName}' is invalid: {error}");
        });
}

public static class EmailExtensions
{
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<EmailTemplates>();

        return services;
    }
}
