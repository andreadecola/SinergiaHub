using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class StoricoGenericoViewModel
    {
        // 🔹 Campi comuni di versionamento
        public string ModificheTestuali { get; set; }
        public string TipoModifica { get; set; }
        public int NumeroVersione { get; set; }
        public string ID_UtenteUltimaModifica { get; set; }
        public string NomeUtente { get; set; }
        public DateTime DataUltimaModifica { get; set; }

        // 🔹 Campi specifici dinamici (NomeCampo → Valore)
        public Dictionary<string, string> CampiSpecifici { get; set; } = new Dictionary<string, string>();

        // ✅ Utility: aggiunge un campo se non nullo
        public void AggiungiCampo(string nomeCampo, object valore)
        {
            if (valore != null)
            {
                CampiSpecifici[nomeCampo] = valore.ToString();
            }
        }

        // ✅ Utility: recupera un campo in modo sicuro
        public string GetCampo(string nomeCampo)
        {
            return CampiSpecifici.ContainsKey(nomeCampo) ? CampiSpecifici[nomeCampo] : string.Empty;
        }
    }
}