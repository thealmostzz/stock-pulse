using StockPulse.Application.Services;
using StockPulse.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddStockPulseInfrastructure(builder.Configuration);
builder.Services.AddScoped<WatchlistService>();
builder.Services.AddCors(options =>
    options.AddPolicy("LocalAngularDevelopment", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(exceptionHandlerApp =>
        exceptionHandlerApp.Run(context =>
            Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "An unexpected error occurred.").ExecuteAsync(context)));
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("LocalAngularDevelopment");

app.UseAuthorization();

app.MapControllers();

app.Run();
