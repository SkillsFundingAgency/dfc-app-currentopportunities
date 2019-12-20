﻿using DFC.App.FindACourseClient.Models.Configuration;
using DFC.App.JobProfile.CurrentOpportunities.Data.Contracts;
using DFC.App.JobProfile.CurrentOpportunities.Data.Models;
using DFC.FindACourseClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DFC.App.JobProfile.CurrentOpportunities.CourseService
{
    public class CourseCurrentOpportuntiesRefresh : ICourseCurrentOpportuntiesRefresh, IHealthCheck
    {
        private readonly ILogger<CourseCurrentOpportuntiesRefresh> logger;
        private readonly ICosmosRepository<CurrentOpportunitiesSegmentModel> repository;
        private readonly ICourseSearchApiService courseSearchApiService;
        private readonly AutoMapper.IMapper mapper;
        private readonly CourseSearchSettings courseSearchSettings;

        public CourseCurrentOpportuntiesRefresh(ILogger<CourseCurrentOpportuntiesRefresh> logger, ICosmosRepository<CurrentOpportunitiesSegmentModel> repository, ICourseSearchApiService courseSearchApiService, AutoMapper.IMapper mapper, CourseSearchSettings courseSearchSettings)
        {
            this.logger = logger;
            this.repository = repository;
            this.courseSearchApiService = courseSearchApiService;
            this.mapper = mapper;
            this.courseSearchSettings = courseSearchSettings;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var description = $"{typeof(CourseCurrentOpportuntiesRefresh).Namespace} - SearchKeywords used [{courseSearchSettings.HealthCheckKeyWords}]";
            logger.LogInformation($"{nameof(CheckHealthAsync)} has been called - service {description}");

            var result = await courseSearchApiService.GetCoursesAsync(courseSearchSettings.HealthCheckKeyWords).ConfigureAwait(false);
            if (result.Any())
            {
                return HealthCheckResult.Healthy(description);
            }
            else
            {
                return HealthCheckResult.Degraded(description);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to catch all errors that happen when we call the external API")]
        public async Task<int> RefreshCoursesAsync(Guid documentId)
        {
            logger.LogInformation($"{nameof(RefreshCoursesAsync)} has been called for document {documentId}");
            var currentOpportunitiesSegmentModel = await repository.GetAsync(d => d.DocumentId == documentId).ConfigureAwait(false);

            logger.LogInformation($"Getting course for {currentOpportunitiesSegmentModel.CanonicalName} - course keywords {currentOpportunitiesSegmentModel.Data.Courses.CourseKeywords}");

            //if the the call to the courses API fails for anyreason we should log and continue as if there are no courses available.
            List<Course> courseSearchResults;
            try
            {
                var results = await courseSearchApiService.GetCoursesAsync(currentOpportunitiesSegmentModel.Data.Courses.CourseKeywords).ConfigureAwait(false);
                courseSearchResults = results.ToList();
            }
            catch (Exception ex)
            {
                var errorMessage = $"{nameof(RefreshCoursesAsync)} had error";
                logger.LogError(ex, errorMessage);
                throw;
            }

            var opportunities = new List<Opportunity>();
            foreach (var course in courseSearchResults)
            {
                var opportunity = mapper.Map<Opportunity>(course);
                opportunity.URL = new Uri($"{courseSearchSettings.CourseSearchUrl}{opportunity.CourseId}");
                opportunities.Add(opportunity);
                logger.LogInformation($"{nameof(RefreshCoursesAsync)} added details for {course.CourseId} to list");
            }

            currentOpportunitiesSegmentModel.Data.Courses.Opportunities = opportunities;
            await repository.UpsertAsync(currentOpportunitiesSegmentModel).ConfigureAwait(false);
            return courseSearchResults.Count;
        }
    }
}