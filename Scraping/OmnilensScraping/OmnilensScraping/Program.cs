using System.Reflection;
using System.Text.Json.Serialization;
using OmnilensScraping;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlFilePath = Path.Combine(AppContext.BaseDirectory, xmlFileName);
    if (File.Exists(xmlFilePath))
    {
        options.IncludeXmlComments(xmlFilePath, includeControllerXmlComments: true);
    }
});

builder.Logging.AddFilter("System.Net.Http.HttpClient.Default", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);

builder.Services.AddOmnilensScraping(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "OmnilensScraping v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
