using Autofac;
using Autofac.Extensions.DependencyInjection;
using Common;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OfficeOpenXml;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using TradeDataCore.MarketData;
using TradeLogicCore.Services;
using TradePort.Utils;

var _log = Logger.New();

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

// to be used in callback
IServiceProvider? services = null;

// create asp.net core application
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services
    .AddEndpointsApiExplorer()
    .AddDirectoryBrowser()
    .AddHttpContextAccessor()
    .AddDistributedMemoryCache();
builder.Services
    .AddSession(o =>
    {
        o.Cookie.Name = "TradePort.Session";
        o.IdleTimeout = TimeSpan.FromHours(24);
        o.Cookie.IsEssential = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    })
    .AddAuthorization()
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.RequireHttpsMetadata = false;
        o.SaveToken = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidAudience = "SpecialTradingUnicorn",
            ValidIssuer = "TradePort",
            //IssuerSigningKey = new SymmetricSecurityKey(secretKey),
            IssuerSigningKeyResolver = (tokenString, securityToken, identifier, parameters) =>
            {
                IHttpContextAccessor accessor = services!.GetService<IHttpContextAccessor>()!;
                string sessionId = accessor.HttpContext!.Session.Id;
                return Authentication.ValidateKey(sessionId,
                                                  tokenString,
                                                  (JwtSecurityToken)securityToken,
                                                  identifier,
                                                  parameters.ValidIssuer,
                                                  parameters.ValidAudience);
            }
        };
    });

builder.Services.AddSwaggerGen(c =>
{
    // set the comments path for the Swagger JSON and UI.
    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
    c.UseInlineDefinitionsForEnums();

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Scheme = "bearer",
        Description = "Please provide the tokenString value from login response."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
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

WebApplication app = builder.Build();

// both prod and dev have SwaggerUI enabled. if (app.Environment.IsDevelopment() || app.Environment.IsProduction())

app.UseSession();
app.UseWebSockets();

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    //builder.RoutePrefix = string.Empty;
    c.SwaggerEndpoint("../swagger/v1/swagger.json", "Web UI");
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "$public, max-age=3600");
    }
});

app.Use(async (context, nextFunction) =>
{
    var publisher = services!.GetService<MarketDataPublisher>()!;
    if (context.Request.Path.ToString().Contains("/ws"))
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await publisher.Process(context.Request.Path.ToString(), webSocket);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
    else
    {
        await nextFunction(context);
    }
});

app.MapControllers().RequireAuthorization();

services = app.Services;

app.Run();
