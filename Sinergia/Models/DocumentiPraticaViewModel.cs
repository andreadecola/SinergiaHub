using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class DocumentiPraticaViewModel
    {
        public int ID_Documento { get; set; }
        public int ID_Pratiche { get; set; }
        public string NomeFile { get; set; }
        public string Estensione { get; set; }
        public string TipoContenuto { get; set; }
        public DateTime? DataCaricamento { get; set; }
        public string Stato { get; set; }

        public int? ID_UtenteCaricamento { get; set; }

        public DateTime? DataArchiviazione { get; set; }
        public int? ID_UtenteArchiviazione { get; set; }


    }
}