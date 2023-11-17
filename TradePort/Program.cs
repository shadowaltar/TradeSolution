using Autofac;
using Autofac.Extensions.DependencyInjection;
using Common;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OfficeOpenXml;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

var log = Logger.New();

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// authorization flag
var isAuthorizationEnabled = false;

// create asp.net core application
var builder = WebApplication.CreateBuilder(args);

var secretStr = CryptographyUtils.Encrypt("SecuritySpell", "");
var secretKey = Encoding.ASCII.GetBytes(secretStr);

builder.Services.AddControllers();
builder.Services
    .AddEndpointsApiExplorer()
    .AddDirectoryBrowser()
    .AddDistributedMemoryCache();
if (isAuthorizationEnabled)
    builder.Services.AddSession(o =>
    {
        o.Cookie.Name = "TradePort.Session";
        o.IdleTimeout = TimeSpan.FromHours(24);
        o.Cookie.IsEssential = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });


if (isAuthorizationEnabled)
{
    builder.Services.AddAuthorization();
    builder.Services.AddAuthentication(x =>
     {
         x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
         x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
     })
    .AddJwtBearer(x =>
    {
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey),
            ValidateIssuer = true,
            ValidIssuer = "TradingPort",
            ValidateAudience = true,
            ValidAudience = "SpecialTradingUnicorn",
            ValidateLifetime = true
        };
    });
}

builder.Services.AddSwaggerGen(c =>
{
    // Set the comments path for the Swagger JSON and UI.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
    c.UseInlineDefinitionsForEnums();
});

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>(builder =>
    {
        // TODO need to use multiple asp.net core instances for different external systems
        builder.RegisterModule<TradeConnectivity.Binance.Dependencies>();
        builder.RegisterModule<TradeDataCore.Dependencies.DependencyModule>();
        builder.RegisterModule<TradeLogicCore.Dependencies.DependencyModule>();
    });

builder.Services.AddMvc().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// both prod and dev have SwaggerUI enabled. if (app.Environment.IsDevelopment() || app.Environment.IsProduction())

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    //c.RoutePrefix = string.Empty;
    c.SwaggerEndpoint("../swagger/v1/swagger.json", "Web UI");
});

app.UseHttpsRedirection();

if (isAuthorizationEnabled)
{
    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "$public, max-age=3600");
    }
});

if (isAuthorizationEnabled)
    app.MapControllers().RequireAuthorization();
else
    app.MapControllers();
app.Run();
