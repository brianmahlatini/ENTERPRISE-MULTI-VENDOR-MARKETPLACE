using System.Text.Json.Serialization;
using MarketHub.Api.Endpoints;
using MarketHub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var frontendOrigins = (builder.Configuration["FRONTEND_URLS"] ?? builder.Configuration["FRONTEND_URL"] ?? "http://localhost:4210,http://localhost:4200")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<MarketplaceStore>();

var app = builder.Build();

app.UseCors("Frontend");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MarketHub.Api" }));
app.MapMarketplaceApi();

app.Run();
