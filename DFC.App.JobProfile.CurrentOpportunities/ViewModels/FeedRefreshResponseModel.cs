﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DFC.App.JobProfile.CurrentOpportunities.ViewModels
{
    public class FeedRefreshResponseModel
    {
        public int NumberPulled { get; set; }

        public string RequestErrorMessage { get; set; }
    }
}
