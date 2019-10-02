﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DFC.App.JobProfile.CurrentOpportunities.Data.Contracts
{
    public interface IAVCurrentOpportunatiesUpdate
    {
      Task<bool> UpdateApprenticeshipVacanciesAsync(string article);
    }
}
