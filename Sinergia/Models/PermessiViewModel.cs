using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sinergia.Models
{
    public class PermessiViewModel
    {
        public int ID_Utente { get; set; }
        public string NomeUtente { get; set; }
        public string Studio { get; set; } // <-- aggiunto!
        public List<PermessoSingoloViewModel> Permessi { get; set; }

        // ✅ Aggiungi queste proprietà globali per gestire la visibilità dei bottoni
        public bool PuòAggiungere => Permessi.Any(p => p.Aggiungi);
        public bool PuòModificare => Permessi.Any(p => p.Modifica);
        public bool PuòEliminare => Permessi.Any(p => p.Elimina);
        public bool MostraDelegabili { get; set; }
        public bool PuòGestirePermessi => Permessi.Any(p => p.Aggiungi || p.Modifica || p.Elimina);

    }

    public class PermessoSingoloViewModel
    {
        public int ID_Menu { get; set; }
        public string NomeMenu { get; set; }
        public bool Abilitato { get; set; }
        public bool Vedi { get; set; }
        public bool Aggiungi { get; set; }
        public bool Modifica { get; set; }
        public bool Elimina { get; set; }
    }
}
