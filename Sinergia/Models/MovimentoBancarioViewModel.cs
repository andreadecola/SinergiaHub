using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class MovimentoBancarioViewModel
    {
        public int ID_Movimento { get; set; }

        public int ID_Cliente { get; set; }
        public string NomeCliente { get; set; }           // info per visualizzazione
        public string TipoCliente { get; set; }           // Azienda / Professionista

        public int? ID_Pratiche { get; set; }
        public string TitoloPratica { get; set; }         // opzionale: utile per interfaccia

        public int? ID_DatoBancario { get; set; }
        public string NomeBanca { get; set; }             // da DatiBancari, per interfaccia

        public string TipoMovimento { get; set; }         // 'Entrata' o 'Uscita'
        public string MetodoPagamento { get; set; }       // 'Bonifico', 'Contanti', 'POS'...

        public string Descrizione { get; set; }
        public decimal Importo { get; set; }

        public DateTime DataOperazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }
        public DateTime? DataInserimento { get; set; }

        public string Stato { get; set; }                 // es: 'Attivo', 'Archiviato', etc.
    }
}