using System.Globalization;
using System.Text.Json.Serialization;
using EquipmentBorrowing.Api;
using EquipmentBorrowing.Application;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Infrastructure;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
    builder.WebHost.UseUrls("http://127.0.0.1:5185");

builder.Services.AddDbContextFactory<EquipmentBorrowingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EquipmentBorrowing") ?? DatabaseConfiguration.ConnectionString));
builder.Services.AddScoped<ILabRepository, PostgresLabRepository>();
builder.Services.AddScoped<LabService>();
builder.Services.AddSingleton<ICalendar, ManilaCalendar>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.AllowDuplicateProperties = false;
    options.SerializerOptions.PropertyNameCaseInsensitive = false;
});

if (builder.Environment.IsEnvironment("Testing") && builder.Configuration["Lab5:FixedDate"] is { } fixedDate)
{
    var date = DateOnly.ParseExact(fixedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    builder.Services.AddSingleton<ICalendar>(new FixedCalendar(date));
}

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(context =>
    ApiEndpoints.Error(500, "server_error", "The server could not complete the request. Try again after service is restored.")
        .ExecuteAsync(context)));
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    var result = context.Response.StatusCode switch
    {
        400 => ApiEndpoints.Error(400, "invalid_request", "Check the request fields, route, or query values."),
        404 => ApiEndpoints.Error(404, "not_found", "The requested route or record does not exist."),
        405 => ApiEndpoints.Error(405, "method_not_allowed", "This route does not support the requested HTTP method."),
        _ => null
    };
    if (result is not null) await result.ExecuteAsync(context);
});
app.MapGet("/openapi/v1.json", () => Results.Stream(
    typeof(ApiEndpoints).Assembly.GetManifestResourceStream("EquipmentBorrowing.OpenApi")!, "application/json; charset=utf-8"));
app.MapLabEndpoints();
app.Run();

sealed class FixedCalendar(DateOnly date) : ICalendar
{
    public DateTime Today => date.ToDateTime(TimeOnly.MinValue);
}
