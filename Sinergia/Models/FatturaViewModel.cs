using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class FatturaViewModel
    {
        public int ID_Fattura { get; set; }
        public string NomeCliente { get; set; }
        public DateTime DataFattura { get; set; }
        public decimal Importo { get; set; }
        public string Stato { get; set; }
    }
}