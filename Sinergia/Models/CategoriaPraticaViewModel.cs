using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class CategoriaPraticaViewModel
    {
        // ============================
        //  🔑 ID PRINCIPALE
        // ============================
        public int ID_CategoriaPratica { get; set; }

        // ============================
        //  📌 DATI ANAGRAFICA
        // ============================
        public string Tipo { get; set; }
        public string Materia { get; set; }
        public string Autorita { get; set; }

        public string Note { get; set; }


        public bool Attivo { get; set; }

        // ============================
        //  🕓 TRACKING CREAZIONE/MODIFICA
        // ============================
        public int? ID_UtenteCreatore { get; set; }
        public DateTime? DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }
        public DateTime? DataUltimaModifica { get; set; }

        // ============================
        //  🗄️ INFO ARCHIVIAZIONE
        // ============================
        public int? ID_UtenteArchiviazione { get; set; }
        public DateTime? DataArchiviazione { get; set; }

        // ============================
        //  🧩 CAMPI DI SERVIZIO (solo View)
        // ============================

        // ============================
        //  🔐 PERMESSI (AGGIUNTA NECESSARIA)
        // ============================
        public bool PuoModificare { get; set; }
        public bool PuoEliminare { get; set; }
        public bool UtenteCorrenteHaPermessi { get; set; }

        // Codice formattato come richiesto nelle nuove anagrafiche
        public string CodiceFormattato
            => $"CPRA-{ID_CategoriaPratica.ToString().PadLeft(5, '0')}";

        // Nome completo utile nelle tabelle o select
        public string NomeCompleto
            => $"{Tipo} - {Materia} - {Autorita}";
    }
}