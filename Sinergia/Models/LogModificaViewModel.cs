using System;

namespace Sinergia.Models
{
    public class LogModificaViewModel
    {
        public int ID { get; set; }
        public DateTime? Data { get; set; }
        public string ModificheTestuali { get; set; }
        public string TipoModifica { get; set; }
        public int NumeroVersione { get; set; }
        public string ID_UtenteUltimaModifica { get; set; }
        public string NomeUtente { get; set; }

        // 👇 Campo calcolato per la lista
        public string Riassunto
        {
            get
            {
                string utente = string.IsNullOrWhiteSpace(NomeUtente)
                    ? $"ID {ID_UtenteUltimaModifica}"
                    : NomeUtente;

                string data = Data.HasValue
                    ? Data.Value.ToString("dd/MM/yyyy HH:mm")
                    : "Data non disponibile";

                // 🔑 Versione 1 = creazione, >=2 = modifica
                string tipoOperazione = NumeroVersione <= 1
                    ? "Creazione"
                    : (TipoModifica ?? "Modifica");

                return $"{tipoOperazione} da {utente} il {data}";
            }
        }
    }
}
