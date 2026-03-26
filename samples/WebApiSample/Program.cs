using System.Net.Mime;
using BlazorLocalization.Extensions;
using BlazorLocalization.Extensions.Providers.JsonFile;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Replaces services.AddLocalization() — registers IStringLocalizerFactory backed by pluggable providers.
builder.Services.AddProviderBasedLocalization(builder.Configuration)
    .AddJsonFileTranslationProvider();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Culture detection — Accept-Language header drives culture selection for APIs.
var supportedCultures = new[] { "en-US", "de", "pl", "da" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

// ── Simple translation ──

app.MapGet("/greetings", (IStringLocalizer<Program> loc) =>
{
    return TypedResults.Ok(new GreetingResponse(
        loc.Translation("Api.Greeting", "Hello from the API!").ToString()));
})
.WithName("GetGreeting")
.Produces<GreetingResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json);

// ── Localized 404 ProblemDetails ──

app.MapGet("/products/{id:int}",
    Results<Ok<ProductResponse>, ProblemHttpResult>
    (int id, IStringLocalizer<Program> loc) =>
{
    if (id <= 0)
    {
        return TypedResults.Problem(
            title: loc.Translation("Api.Error.NotFound.Title", "Product not found").ToString(),
            detail: loc.Translation("Api.Error.NotFound.Detail", "No product exists with the given ID.").ToString(),
            statusCode: StatusCodes.Status404NotFound);
    }

    return TypedResults.Ok(new ProductResponse(id, "Widget", 9.99m));
})
.WithName("GetProduct")
.Produces<ProductResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)
.Produces<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson);

// ── Localized validation errors ──

app.MapPost("/products",
    Results<Created<ProductResponse>, ValidationProblem>
    (CreateProductRequest request, IStringLocalizer<Program> loc) =>
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Name))
        errors["name"] = [loc.Translation("Api.Validation.NameRequired", "Name is required.").ToString()];

    if (request.Price <= 0)
        errors["price"] = [loc.Translation("Api.Validation.PricePositive", "Price must be greater than zero.").ToString()];

    if (errors.Count > 0)
        return TypedResults.ValidationProblem(
            errors,
            title: loc.Translation("Api.Validation.Title", "Validation failed").ToString());

    var product = new ProductResponse(1, request.Name!, request.Price);
    return TypedResults.Created($"/products/{product.Id}", product);
})
.WithName("CreateProduct")
.Produces<ProductResponse>(StatusCodes.Status201Created, MediaTypeNames.Application.Json)
.Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson);

app.Run();

public record GreetingResponse(string Greeting);
public record ProductResponse(int Id, string Name, decimal Price);
public record CreateProductRequest(string? Name, decimal Price);

public partial class Program;
