using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class EventoViewModel
    {
        public int ID_Evento { get; set; }
        public string Titolo { get; set; }
        public string Descrizione { get; set; }
        public DateTime DataEvento { get; set; }
        public string Stato { get; set; }
        public string Note { get; set; }
    }
}