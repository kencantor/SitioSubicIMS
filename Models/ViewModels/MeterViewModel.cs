using SitioSubicIMS.Web.Models;
using System.Collections.Generic;

namespace SitioSubicIMS.Web.ViewModels
{
    public class MeterViewModel
    {
        public List<Meter> MeterList { get; set; } = new List<Meter>();
        public Meter MeterForm { get; set; }
    }
}
