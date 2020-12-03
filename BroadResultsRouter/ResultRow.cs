using System;
using System.Collections.Generic;
using System.Text;

namespace BroadResultsRouter
{
    public class ResultRow
    {
        public string sample_id { get; set; }
        public string patient_id { get; set; }
        public string patient_name { get; set; }
        public string institution_id { get; set; }
        public string physician { get; set; }
        public string time_collected { get; set; }
        public string specimen_type { get; set; }
        public string time_completed { get; set; }
        public string result { get; set; }
        public string reason { get; set; }
        public string matrix_id { get; set; }
    }
}
