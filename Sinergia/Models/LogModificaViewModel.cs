using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
  
        public class LogModificaViewModel
        {
            public int ID { get; set; }
            public DateTime Data { get; set; }
            public string ModificheTestuali { get; set; }
            public string TipoModifica { get; set; }
            public int NumeroVersione { get; set; }
            public string ID_UtenteUltimaModifica { get; set; }

            public string NomeUtente { get; set; }

    }

}
