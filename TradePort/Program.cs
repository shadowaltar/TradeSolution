using Autofac;
using Autofac.Extensions.DependencyInjection;
using Common;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OfficeOpenXml;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeLogicCore.Services;
using TradePort.Utils;

public class Program
{
    private static readonly ILog _log = Logger.New();
    private static WebApplication _app;
    private static EnvironmentType _environment;

    static Program()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    private static void Main(string[] args)
    {
        var envString = args.IsNullOrEmpty() ? "PROD" : args[0].ToUpperInvariant();
        _environment = TradeCommon.Constants.Environments.Parse(envString);
        _log.Info("Selected environment: " + _environment);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        switch (_environment)
        {
            case EnvironmentType.Prod:
                builder.Configuration.AddJsonFile("appsettings.prod.json");
                break;
            case EnvironmentType.Uat:
                builder.Configuration.AddJsonFile("appsettings.uat.json");
                break;
            case EnvironmentType.Simulation:
                builder.Configuration.AddJsonFile("appsettings.sim.json");
                break;
            case EnvironmentType.Test:
                builder.Configuration.AddJsonFile("appsettings.test.json");
                break;
            default:
                throw Exceptions.Invalid<EnvironmentType>("Invalid environment.");
        }

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
                        IHttpContextAccessor accessor = _app.Services.GetService<IHttpContextAccessor>()!;
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
            c.DocumentFilter<TitleFilter>();
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
                switch (_environment)
                {
                    case EnvironmentType.Simulation:
                        builder.RegisterModule<TradeConnectivity.CryptoSimulator.Dependencies>();
                        _log.Info("Loaded module for: " + nameof(TradeConnectivity.CryptoSimulator));
                        break;
                    case EnvironmentType.Test:
                    case EnvironmentType.Uat:
                    case EnvironmentType.Prod:
                        builder.RegisterModule<TradeConnectivity.Binance.Dependencies>();
                        _log.Info("Loaded module for: " + nameof(TradeConnectivity.Binance));
                        break;
                    default:
                        throw Exceptions.Invalid<EnvironmentType>("Invalid environment.");
                }
                builder.RegisterModule<TradeDataCore.Dependencies.DependencyModule>();
                builder.RegisterModule<TradeLogicCore.Dependencies.DependencyModule>();
            });

        builder.Services.AddMvc().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        _app = builder.Build();

        // both prod and dev have SwaggerUI enabled. if (_app.Environment.IsDevelopment() || _app.Environment.IsProduction())

        _app.UseSession();
        _app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromHours(1) });
        _app.UseDeveloperExceptionPage();
        _app.UseSwagger();
        _app.UseSwaggerUI(c =>
        {
            //builder.RoutePrefix = string.Empty;
            c.SwaggerEndpoint("v1/swagger.json", "Trade Port");

            switch (_environment)
            {
                case EnvironmentType.Simulation:
                    c.DocumentTitle = "SIMULATION";
                    c.InjectStylesheet("/swagger-custom/sim-styles.css");
                    break;
                case EnvironmentType.Test:
                    c.InjectStylesheet("/swagger-custom/test-styles.css");
                    break;
                case EnvironmentType.Uat:
                    c.InjectStylesheet("/swagger-custom/uat-styles.css");
                    break;
                case EnvironmentType.Prod:
                    c.DocumentTitle = "PRODUCTION";

                    c.InjectStylesheet("/swagger-custom/prod-styles.css");
                    break;
                default:
                    throw Exceptions.Invalid<EnvironmentType>("Invalid environment.");
            }
        });

        _app.UseHttpsRedirection();
        _app.UseAuthentication();
        _app.UseRouting();
        _app.UseAuthorization();
        _app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.Append("Cache-Control", "$public, max-age=3600");
            }
        });

        _app.MapControllers().RequireAuthorization();

        // TODO supports Binance only now
        var context = _app.Services.GetService<Context>();
        var exchange = _environment == EnvironmentType.Simulation ? ExchangeType.Simulator : ExchangeType.Binance;
        var broker = _environment == EnvironmentType.Simulation ? BrokerType.Simulator : ExternalNames.Convert(exchange);
        context!.Initialize(_environment, exchange, broker);
        _app.Run();
    }

    private class TitleFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument doc, DocumentFilterContext context)
        {
            switch (_environment)
            {
                case EnvironmentType.Simulation:
                    doc.Info.Title = "TradePort (SIMULATION)";
                    break;
                case EnvironmentType.Test:
                    doc.Info.Title = "TradePort (TEST)";
                    break;
                case EnvironmentType.Uat:
                    doc.Info.Title = "TradePort (UAT)";
                    break;
                case EnvironmentType.Prod:
                    doc.Info.Title = "TradePort (PRODUCTION)";
                    break;
                default:
                    throw Exceptions.Invalid<EnvironmentType>("Invalid environment.");
            }
        }
    }
}