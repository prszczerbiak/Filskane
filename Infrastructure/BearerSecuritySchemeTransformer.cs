using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var authenticationScheme = "Bearer";

        // 1. Definicja schematu (Klasyczna)
        var requirements = new Dictionary<string, OpenApiSecurityScheme>
        {
            [authenticationScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer", // Małe litery
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Wpisz token JWT."
            }
        };

        document.Components ??= new OpenApiComponents();
        foreach (var req in requirements)
        {
            // Tutaj po prostu dodajemy do słownika
            document.Components.SecuritySchemes[req.Key] = req.Value;
        }

        // 2. Wymaganie bezpieczeństwa (Używamy starych nazw właściwości)
        // W wersji 1.6.x to pole nazywa się 'SecurityRequirements' i działa z 'Reference'
        document.SecurityRequirements = new List<OpenApiSecurityRequirement>
        {
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        // To teraz zadziała, bo wróciliśmy do wersji 1.6.22!
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = authenticationScheme
                        }
                    },
                    new string[] { }
                }
            }
        };

        return Task.CompletedTask;
    }
}