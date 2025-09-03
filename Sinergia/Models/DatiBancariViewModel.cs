using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class DatiBancariViewModel
    {
        public int ID_DatoBancario { get; set; }
        public int ID_Cliente { get; set; }

        public string NomeBanca { get; set; }
        public string IBAN { get; set; }
        public string BIC { get; set; }
        public string IntestatarioConto { get; set; }
        public string MetodoPagamentoPreferito { get; set; } // es: Bonifico, Contanti, Assegno, etc.

        public DateTime? DataInserimento { get; set; }
        public int? ID_UtenteInserimento { get; set; }

        public string Note {  get; set; }

        // Info cliente per visualizzazione
        public string NomeCliente { get; set; }
        public string TipoCliente { get; set; }

        
    }
}