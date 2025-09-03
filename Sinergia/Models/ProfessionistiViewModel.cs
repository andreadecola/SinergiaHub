using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class ProfessionistiViewModel
    {
        public int ID_Professionista { get; set; }
       // public int ID_Utente { get; set; } // 👈 necessario per Gestione Permessi


        public string Nome { get; set; }
        public string Cognome {  get; set; }
        public string TipoRagioneSociale { get; set; } // Es: Ditta Individuale, STP...
        public string PartitaIVA { get; set; }
        public string CodiceFiscale { get; set; }
        public string CodiceUnivoco { get; set; }
        public string Indirizzo { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
        public string MAIL1 { get; set; }
        public string MAIL2 { get; set; }
        public string Citta { get; set; }
        public string Nazione { get; set; }
        public string Stato { get; set; }
        public string Note { get; set; }
        public string DescrizioneAttivita { get; set; }
        public int UtentiAssociati { get; set; } // Numero utenti associati
        public bool HaUtentiAssegnati { get; set; } // Per decidere se mostrare "Visualizza"

        public bool PuòGestirePermessi { get; set; } // getsione permessi professionistta 


        // dati bancari 
        public string IBAN { get; set; }
        public string NomeBanca { get; set; }
        public string Intestatario { get; set; }
        public string BIC_SWIFT { get; set; }
        public string IndirizzoBanca { get; set; }
        public string NoteBanca { get; set; }

        public bool PuoModificare { get; set; }
        public bool PuoEliminare { get; set; }


        // dati practice

        public int ID_Professione{ get; set; }
        public string NomePractice { get; set; }

        public string TipoProfessionista { get; set; } // "Resident" o "Esterno"

        public int ID_Citta { get; set; }
        public int ID_CittaResidenza   // 👈 alias per il form
        {
            get { return ID_Citta; }
            set { ID_Citta = value; }
        }

        public int ID_Nazione { get; set; }

        public bool UtenteCorrenteHaPermessi { get; set; }

        public string ProfessionistaAssegnatoDaCollaboratore { get; set; }

        // ✅ Nuove proprietà richieste
        public bool ÈCliente { get; set; }
        public bool ÈFornitore { get; set; }

        public string NomeOwner { get; set; }

        public string TipoRuolo
        {
            get
            {
                if (ÈCliente && ÈFornitore) return "Cliente / Fornitore";
                if (ÈCliente) return "Cliente";
                if (ÈFornitore) return "Fornitore";
                return "N/D";
            }
        }



    }
}