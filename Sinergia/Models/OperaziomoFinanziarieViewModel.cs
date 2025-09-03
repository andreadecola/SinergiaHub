using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class OperaziomoFinanziarieViewModel
    {
        public int ID_Finanza { get; set; }
        public int? ID_Pratiche { get; set; }
        public string TipoOperazione { get; set; }
        public decimal Importo { get; set; }
        public string Descrizione { get; set; }
        public DateTime? DataOperazione { get; set; }
        public string Stato { get; set; }
        public string MetodoDiPagamento { get; set; }

        public int ID_UtenteCreatore { get; set; }
        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataArchiviazione { get; set; }
        public int? ID_UtenteArchiviazione { get; set; }
        public string TipoCliente { get; set; } // <-- AGGIUNGERE QUESTO

        public string NomeResponsabile { get; set; }

        public List<string> NomiUtentiAssegnati {  get; set; }

        public string NomeCliente { get; set; }

        public int? ID_Operazione { get; set; }
        public DateTime? UltimaModifica { get; set; } // Aggiungi questa proprietà se la usi
        public int? ID_Cliente { get; set; } // Aggiungi ID_Cliente se ti serve

        public string NumeroFattura { get; set; }

        public string NomePratica { get; set; }

        public string Categoria { get; set; }

    }
}