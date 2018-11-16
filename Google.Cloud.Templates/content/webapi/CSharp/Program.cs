
using Google.Cloud.Diagnostics.AspNetCore;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Google.Cloud.Templates.WebApi.CSharp
{
    public static class Program
    {
        private static IHostingEnvironment HostingEnvironment { get; set; }
        private static IConfiguration Configuration { get; set; }

        private static string GcpProjectId { get; set; }
        private static bool HasGcpProjectId => !string.IsNullOrEmpty(GcpProjectId);

        public static void Main(string[] args) => BuildWebHost(args).Run();

        private static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .ConfigureAppConfiguration((context, configBuilder) => {
                    HostingEnvironment = context.HostingEnvironment;

                    configBuilder.SetBasePath(HostingEnvironment.ContentRootPath)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{HostingEnvironment.EnvironmentName}.json", optional: true)
                        .AddEnvironmentVariables();

                    Configuration = configBuilder.Build();
                    GcpProjectId = GetProjectId(Configuration);
                })
                .ConfigureServices(services => {
                    // Add framework services.Microsoft.VisualStudio.ExtensionManager.ExtensionManagerService
                    services.AddMvc();
#if (GcpProjectIdInConfig)
                        // Enables Stackdriver Trace.
                        services.AddGoogleTrace(options => options.ProjectId = GcpProjectId);
                        // Sends Exceptions to Stackdriver Error Reporting.
                        services.AddGoogleExceptionLogging(
                            options => {
                                options.ProjectId = GcpProjectId;
                                options.ServiceName = GetServiceName(Configuration);
                                options.Version = GetVersion(Configuration);
                            });
                        services.AddSingleton<ILoggerProvider>(sp => GoogleLoggerProvider.Create(GcpProjectId));
#else
                    if (HasGcpProjectId)
                    {
                        // Enables Stackdriver Trace.
                        services.AddGoogleTrace(options => options.ProjectId = GcpProjectId);
                        // Sends Exceptions to Stackdriver Error Reporting.
                        services.AddGoogleExceptionLogging(
                            options => {
                                options.ProjectId = GcpProjectId;
                                options.ServiceName = GetServiceName(Configuration);
                                options.Version = GetVersion(Configuration);
                            });
                        services.AddSingleton<ILoggerProvider>(sp => GoogleLoggerProvider.Create(GcpProjectId));
                    }
#endif
                })
                .ConfigureLogging(loggingBuilder => {
                    loggingBuilder.AddConfiguration(Configuration.GetSection("Logging"));
                    if (HostingEnvironment.IsDevelopment())
                    {
                        // Only use Console and Debug logging during development.
                        loggingBuilder.AddConsole(
                            options =>
                                options.IncludeScopes = Configuration.GetValue<bool>("Logging:IncludeScopes"));
                        loggingBuilder.AddDebug();
                    }
                })
                .Configure(app => {
                    ILogger logger = app.ApplicationServices.GetService<ILoggerFactory>().CreateLogger("Startup");
#if (GcpProjectIdInConfig)
                    // Sends logs to Stackdriver Error Reporting.
                    app.UseGoogleExceptionLogging();
                    // Sends logs to Stackdriver Trace.
                    app.UseGoogleTrace();

                    logger.LogInformation("Stackdriver Logging enabled: "+
                        $"https://console.cloud.google.com/logs?project-id={GcpProjectId}");
                    logger.LogInformation("Stackdriver Error Reporting enabled: "+
                        $"https://console.cloud.google.com/errors?project-id={GcpProjectId}");
                    logger.LogInformation("Stackdriver Trace enabled: "+
                        $"https://console.cloud.google.com/traces?project-id={GcpProjectId}");
#else
                    if (HasGcpProjectId)
                    {
                        // Sends logs to Stackdriver Error Reporting.
                        app.UseGoogleExceptionLogging();
                        // Sends logs to Stackdriver Trace.
                        app.UseGoogleTrace();

                        logger.LogInformation("Stackdriver Logging enabled: " +
                            $"https://console.cloud.google.com/logs?project-id={GcpProjectId}");
                        logger.LogInformation("Stackdriver Error Reporting enabled: " +
                            $"https://console.cloud.google.com/errors?project-id={GcpProjectId}");
                        logger.LogInformation("Stackdriver Trace enabled: " +
                            $"https://console.cloud.google.com/traces?project-id={GcpProjectId}");
                    }
                    else
                    {
                        logger.LogWarning(
                            "Stackdriver Logging, Error Reporting, and Trace not enabled. " +
                            "Missing Google:ProjectId in configuration.");
                    }
#endif

                    // So middleware knows AppEngine requests arrived via https.
                    app.UseForwardedHeaders(new ForwardedHeadersOptions {
                        ForwardedHeaders = ForwardedHeaders.XForwardedProto
                    });

                    app.UseMvc();
                })
                .Build();

        /// <summary>
        /// Get the Google Cloud Platform Project ID from the platform it is running on,
        /// or from the appsettings.json configuration if not running on Google Cloud Platform.
        /// </summary>
        /// <param name="config">The appsettings.json configuration.</param>
        /// <returns>
        /// The ID of the GCP Project this service is running on, or the Google:ProjectId
        /// from the configuration if not running on GCP.
        /// </returns>
        private static string GetProjectId(IConfiguration config)
        {
            var instance = Google.Api.Gax.Platform.Instance();
            string projectId = instance?.ProjectId ?? config["Google:ProjectId"];
            if (string.IsNullOrEmpty(projectId))
            {
#if (GcpProjectIdInConfig)
                throw new InvalidOperationException(
                    "The Stackdriver libraries require a Google Cloud Project Id." +
                    "Update appsettings.json by setting the Google:ProjectId property with your " +
                    "Google Cloud Project Id.");
#else
                // Set Google:ProjectId in appsettings.json to enable stackdriver logging outside of GCP.
                return null;
#endif
            }

            return projectId;
        }

        /// <summary>
        /// Gets a service name for error reporting.
        /// </summary>
        /// <param name="config">The appsettings.json configuration to read a service name from.</param>
        /// <returns>
        /// The name of the Google App Engine service hosting this application,
        /// or the Google:ErrorReporting:ServiceName configuration field if running elsewhere.
        /// </returns>
        /// <seealso href="https://cloud.google.com/error-reporting/docs/formatting-error-messages#FIELDS.service"/>
        private static string GetServiceName(IConfiguration config)
        {
            var instance = Google.Api.Gax.Platform.Instance();
            string serviceName = instance?.GaeDetails?.ServiceId ?? config["Google:ErrorReporting:ServiceName"];
            if (string.IsNullOrEmpty(serviceName))
            {
                throw new InvalidOperationException(
                    "The Stackdriver error reporting library requires a service name. " +
                    "Update appsettings.json by setting the Google:ErrorReporting:ServiceName property with your " +
                    "Service Id, then recompile.");
            }

            return serviceName;
        }

        /// <summary>
        /// Gets a version id for error reporting.
        /// </summary>
        /// <param name="config">The appsettings.json configuration to read a version id from.</param>
        /// <returns>
        /// The version of the Google App Engine service hosting this application,
        /// or the Google:ErrorReporting:Version configuration field if running elsewhere.
        /// </returns>
        /// <seealso href="https://cloud.google.com/error-reporting/docs/formatting-error-messages#FIELDS.version"/>
        private static string GetVersion(IConfiguration config)
        {
            var instance = Google.Api.Gax.Platform.Instance();
            string versionId = instance?.GaeDetails?.VersionId ?? config["Google:ErrorReporting:Version"];
            if (string.IsNullOrEmpty(versionId))
            {
                throw new InvalidOperationException(
                    "The Stackdriver error reporting library requires a version id. " +
                    "Update appsettings.json by setting the Google:ErrorReporting:Version property with your " +
                    "service version id, then recompile.");
            }

            return versionId;
        }
    }
}
