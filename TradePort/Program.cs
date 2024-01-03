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
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeLogicCore.Services;
using TradePort.Utils;

namespace TradePort;

public class Program
{
    private static readonly ILog _log = Logger.New();
    private static readonly Dictionary<EnvironmentType, EnvironmentConfig> _envConfigs;
    private static WebApplication _app;
    private static EnvironmentType _environment;

    static Program()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        _envConfigs = Enum.GetValues<EnvironmentType>().Where(t => t != EnvironmentType.Unknown)
            .ToDictionary(e => e, e => new EnvironmentConfig(e));
    }

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            _log.Error("Must specify environment string.");
            return -1;
        }
        var envString = args.IsNullOrEmpty() ? "PROD" : args[0].ToUpperInvariant();

        // if true, do not append env string to the path
        var skipEnvStringInApiPath = false;
        if (args.Length == 2 && bool.TryParse(args[1], out var trueThenSkip) && trueThenSkip)
        {
            skipEnvStringInApiPath = true;
        }

        _environment = TradeCommon.Constants.Environments.Parse(envString);
        _log.Info("Selected environment: " + _environment);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile(_envConfigs[_environment].AppSettingsFileName);
        builder.Services.AddControllers();
        builder.Services
            .AddEndpointsApiExplorer()
            .AddDirectoryBrowser()
            .AddHttpContextAccessor()
            .AddDistributedMemoryCache();
        builder.Services
            .AddSession(o =>
            {
                o.Cookie.Name = _envConfigs[_environment].CookieName;
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
                _envConfigs[_environment].RegisterDependencyModule(builder);
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
        _app.UseSwagger(c =>
        {
            var subPath = _envConfigs[_environment].SubUrl;
            c.PreSerializeFilters.Add((swagger, httpReq) =>
            {
                if (skipEnvStringInApiPath) return;

                var oldPaths = swagger.Paths.Select(e => (e.Key, e.Value)).ToList();
                foreach (var (path, pathItem) in oldPaths)
                {
                    var newPath = path;
                    newPath = Path.Join(subPath, newPath); // must start with '/'
                    swagger.Paths.Remove(path);
                    swagger.Paths.Add(newPath, pathItem);
                }
            });
        });
        _app.UseSwaggerUI(c =>
        {
            var config = _envConfigs[_environment];
            c.DocumentTitle = config.Title;
            c.InjectStylesheet(config.CustomStylePath);
        });

        _app.UseSession();
        _app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromHours(1) });
        _app.UseDeveloperExceptionPage();
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
        return 0;
    }

    private class TitleFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument doc, DocumentFilterContext context)
        {
            doc.Info.Title = _envConfigs[_environment].Title;
        }
    }
}