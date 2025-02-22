﻿using DFC.App.JobProfile.CurrentOpportunities.Data.Configuration;
using DFC.App.JobProfile.CurrentOpportunities.Data.Contracts;
using DFC.App.JobProfile.CurrentOpportunities.Data.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DFC.App.JobProfile.CurrentOpportunities.AVService
{
    public class AVAPIService : IAVAPIService, IHealthCheck
    {
        private readonly IApprenticeshipVacancyApi apprenticeshipVacancyApi;
        private readonly ILogger<AVAPIService> logger;
        private readonly AVAPIServiceSettings aVAPIServiceSettings;

        public AVAPIService(IApprenticeshipVacancyApi apprenticeshipVacancyApi, ILogger<AVAPIService> logger, AVAPIServiceSettings aVAPIServiceSettings)
        {
            this.apprenticeshipVacancyApi = apprenticeshipVacancyApi;
            this.logger = logger;
            this.aVAPIServiceSettings = aVAPIServiceSettings;
        }

        public async Task<ApprenticeshipVacancyDetails> GetApprenticeshipVacancyDetailsAsync(int vacancyRef)
        {
            var responseResult = await apprenticeshipVacancyApi.GetAsync($"{vacancyRef}", RequestType.VacancyByReference).ConfigureAwait(true);
            logger.LogInformation($"Got details for vacancy ref : {vacancyRef}");
            return JsonConvert.DeserializeObject<ApprenticeshipVacancyDetails>(responseResult);
        }

        public async Task<IEnumerable<ApprenticeshipVacancySummary>> GetAVsForMultipleProvidersAsync(AVMapping mapping)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            List<ApprenticeshipVacancySummary> avSummary = new List<ApprenticeshipVacancySummary>();

            var pageNumber = 0;

            logger.LogInformation($"Getting vacancies for mapping {JsonConvert.SerializeObject(mapping)}");

            //Allways break after a given number off loops
            while (aVAPIServiceSettings.FAAMaxPagesToTryPerMapping > pageNumber)
            {
                var apprenticeshipVacancySummaryResponse = await GetAVSumaryPageAsync(mapping, ++pageNumber).ConfigureAwait(false);

                logger.LogInformation($"Got {apprenticeshipVacancySummaryResponse.TotalFiltered} vacancies of {apprenticeshipVacancySummaryResponse.Total} on page: {pageNumber} of {apprenticeshipVacancySummaryResponse.TotalPages}");

                avSummary.AddRange(apprenticeshipVacancySummaryResponse.Vacancies);

                //stop when there are no more pages or we have more then multiple supplier
                if (apprenticeshipVacancySummaryResponse.TotalPages < pageNumber ||
                     avSummary.Select(v => v.ProviderName).Distinct().Count() > 1)
                {
                    break;
                }
            }

            return avSummary;
        }

        public async Task<ApprenticeshipVacancySummaryResponse> GetAVSumaryPageAsync(AVMapping mapping, int pageNumber)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            logger.LogInformation($"Extracting AV summaries for Standards = {mapping.Standards} page : {pageNumber}");
            string queryString = GetQueryString(mapping, pageNumber);

            var responseResult = await apprenticeshipVacancyApi.GetAsync(queryString, RequestType.ListVacancies).ConfigureAwait(false);

            return JsonConvert.DeserializeObject<ApprenticeshipVacancySummaryResponse>(responseResult);
        }

        private string GetQueryString(AVMapping mapping, int pageNumber)
        {
            var queryStringList = new List<string>();

            if (mapping.Standards != null)
            {
                foreach (var standard in mapping.Standards)
                {
                    queryStringList.Add($"StandardLarsCode={standard}");
                }
            }

            queryStringList.Add($"PageSize={aVAPIServiceSettings.FAAPageSize}");
            queryStringList.Add($"PageNumber={pageNumber}");
            queryStringList.Add($"Sort={aVAPIServiceSettings.FAASortBy}");

            var queryString = string.Join("&", queryStringList);
            return queryString;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var description = $"{typeof(AVAPIService).Namespace} - Mappings used standards[{aVAPIServiceSettings.StandardsForHealthCheck}]";
            logger.LogInformation($"{nameof(CheckHealthAsync)} has been called - service {description}");

            var apprenticeshipVacancySummaryResponse = await GetAVSumaryPageAsync(new AVMapping { Standards = aVAPIServiceSettings.StandardsForHealthCheck.Split(',') }, 1).ConfigureAwait(false);

            if (apprenticeshipVacancySummaryResponse.Vacancies.Any())
            {
                return HealthCheckResult.Healthy(description);
            }
            else
            {
                return HealthCheckResult.Degraded(description);
            }
        }
    }
}
