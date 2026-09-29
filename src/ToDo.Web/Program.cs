using Scalar.AspNetCore;
using ToDo.Application;
using ToDo.Repository;
using ToDo.Web.Endpoints;
using ToDo.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todo")
    ?? throw new InvalidOperationException("ConnectionStrings:Todo is not configured.");

builder.Services.AddTodoApplication();

// The only line in this project that knows the Repository assembly exists. Everything else talks
// to ITodoListService and has no way to reach EF Core.
builder.Services.AddTodoRepository(connectionString);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Demo convenience only. A real deployment migrates as a separate, gated step — see README.
    await app.Services.MigrateTodoDatabaseAsync();

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapTodoLists();

await app.RunAsync();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in the test project can boot this host.
/// Top-level statements otherwise compile to an internal entry point.
/// </summary>
public partial class Program;
