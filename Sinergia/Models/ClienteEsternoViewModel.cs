using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class ClienteEsternoViewModel
    {
        public int ID_Cliente { get; set; }

        public string Nome { get; set; }
        public string Cognome { get; set; }
        public string RagioneSociale { get; set; }

        public string CodiceFiscale { get; set; }
        public string PIVA { get; set; }

        public string Indirizzo { get; set; }
        public int? ID_Citta { get; set; }
        public int? ID_Nazione { get; set; }

        public string Telefono { get; set; }
        public string Email { get; set; }

        public string Note { get; set; }
        public string TipoCliente { get; set; } // es: "Persona Fisica", "Azienda", ecc.

        public DateTime? DataCreazione { get; set; }
        public string Stato { get; set; }

        public int ID_Operatore { get; set; }            // FK verso OperatoriSinergia
        public string TipoOperatore { get; set; }        // "Azienda" o "Professionista"

        // Proprietà di comodo
        public string NomeCompleto =>
            string.IsNullOrEmpty(Cognome) ? Nome : $"{Nome} {Cognome}";

        public string NomeCitta { get; set; }
        public string NomeNazione { get; set; }

        // 🔹 Separiamo Owner e Associati
        public string OwnerVisualizzato { get; set; }            // Nome del professionista Owner
        public string AssociatiVisualizzati { get; set; }

        public string OperatoreVisualizzato { get; set; } // Nome dell’operatore collegato

        // 👉 AGGIUNGI QUESTI FLAG DI PERMESSO
        public bool PuoModificare { get; set; } = true;
        public bool PuoEliminare { get; set; } = true;

        public bool UtenteCorrenteHaPermessi { get; set; }

    }
}