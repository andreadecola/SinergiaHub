using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class CalcoloFatturaResult
    {
        public decimal ImportoNetto { get; set; }
        public decimal ImportoIVA { get; set; }
        public decimal ImportoTotale { get; set; }
    }

}
