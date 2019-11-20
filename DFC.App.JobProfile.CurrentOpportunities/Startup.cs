﻿using AutoMapper;
using DFC.App.FindACourseClient.Models.Configuration;
using DFC.App.JobProfile.CurrentOpportunities.AVService;
using DFC.App.JobProfile.CurrentOpportunities.CourseService;
using DFC.App.JobProfile.CurrentOpportunities.Data.Configuration;
using DFC.App.JobProfile.CurrentOpportunities.Data.Contracts;
using DFC.App.JobProfile.CurrentOpportunities.Data.Models;
using DFC.App.JobProfile.CurrentOpportunities.Data.ServiceBusModels;
using DFC.App.JobProfile.CurrentOpportunities.Repository.CosmosDb;
using DFC.App.JobProfile.CurrentOpportunities.SegmentService;
using DFC.FindACourseClient;
using DFC.FindACourseClient.Contracts;
using DFC.FindACourseClient.Models.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DFC.App.JobProfile.CurrentOpportunities
{
    public class Startup
    {
        public const string CosmosDbConfigAppSettings = "Configuration:CosmosDbConnections:JobProfileSegment";
        public const string ServiceBusOptionsAppSettings = "ServiceBusOptions";
        public const string AVAPIServiceAppSettings = "Configuration:AVAPIService";
        public const string AVFeedAuditSettings = "Configuration:CosmosDbConnections:AVFeedAudit";
        public const string CourseSearchAppSettings = "Configuration:CourseSearch";
        public const string CourseSearchClientSvcSettings = "Configuration:CourseSearchClient:CourseSearchSvc";
        public const string CourseSearchClientAuditSettings = "Configuration:CourseSearchClient:CosmosAuditConnection";

        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                //This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            var aVAPIServiceSettings = configuration.GetSection(AVAPIServiceAppSettings).Get<AVAPIServiceSettings>();
            services.AddSingleton(aVAPIServiceSettings ?? new AVAPIServiceSettings());

            var courseSearchSettings = configuration.GetSection(CourseSearchAppSettings).Get<CourseSearchSettings>();
            services.AddSingleton(courseSearchSettings ?? new CourseSearchSettings());

            var courseSearchClientSettings = new CourseSearchClientSettings
            {
                CourseSearchSvcSettings = configuration.GetSection(CourseSearchClientSvcSettings).Get<CourseSearchSvcSettings>() ?? new CourseSearchSvcSettings(),
                CourseSearchAuditCosmosDbSettings = configuration.GetSection(CourseSearchClientAuditSettings).Get<CourseSearchAuditCosmosDbSettings>() ?? new CourseSearchAuditCosmosDbSettings(),
            };
            services.AddSingleton(courseSearchClientSettings);
            services.AddSingleton<ICourseSearchClient, CourseSearchClient>();
            services.AddFindACourseServices(courseSearchClientSettings);

            var serviceBusOptions = configuration.GetSection(ServiceBusOptionsAppSettings).Get<ServiceBusOptions>();
            services.AddSingleton(serviceBusOptions ?? new ServiceBusOptions());

            services.AddSingleton<ICosmosRepository<CurrentOpportunitiesSegmentModel>, CosmosRepository<CurrentOpportunitiesSegmentModel>>(s =>
            {
                var cosmosDbConnection = configuration.GetSection(CosmosDbConfigAppSettings).Get<CosmosDbConnection>();
                var documentClient = new DocumentClient(cosmosDbConnection.EndpointUrl, cosmosDbConnection.AccessKey);

                return new CosmosRepository<CurrentOpportunitiesSegmentModel>(cosmosDbConnection, documentClient, s.GetService<IHostingEnvironment>());
            });

            services.AddSingleton<ICosmosRepository<APIAuditRecordAV>, CosmosRepository<APIAuditRecordAV>>(s =>
            {
                var cosmosDbAuditConnection = configuration.GetSection(AVFeedAuditSettings).Get<CosmosDbConnection>();
                var documentClient = new DocumentClient(cosmosDbAuditConnection.EndpointUrl, cosmosDbAuditConnection.AccessKey);

                return new CosmosRepository<APIAuditRecordAV>(cosmosDbAuditConnection, documentClient, s.GetService<IHostingEnvironment>());
            });

            services.AddScoped<ICourseCurrentOpportuntiesRefresh, CourseCurrentOpportuntiesRefresh>();
            services.AddScoped<IAVCurrentOpportuntiesRefresh, AVCurrentOpportuntiesRefresh>();
            services.AddScoped<ICurrentOpportunitiesSegmentService, CurrentOpportunitiesSegmentService>();
            services.AddSingleton<IJobProfileSegmentRefreshService<RefreshJobProfileSegmentServiceBusModel>, JobProfileSegmentRefreshService<RefreshJobProfileSegmentServiceBusModel>>();
            services.AddAutoMapper(typeof(Startup).Assembly);

            services.AddHttpClient<IApprenticeshipVacancyApi, ApprenticeshipVacancyApi>();
            services.AddScoped<IAVAPIService, AVAPIService>();

            services.AddHealthChecks()
            .AddCheck<CurrentOpportunitiesSegmentService>("Current Opportunities Segment Service")
            .AddCheck<CourseCurrentOpportuntiesRefresh>("Course Search")
            .AddCheck<AVAPIService>("Apprenticeship Service");

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");

                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseMvcWithDefaultRoute();
        }
    }
}