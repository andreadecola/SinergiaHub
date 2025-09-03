using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public class ArchiviazioneHelper
    {
        public static void ArchiviaPratica(int id, SinergiaDB db, int idUtente)
        {
            var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == id && p.Stato == "Conclusa");
            if (pratica == null) return;

            var praticaArchivio = new Pratiche_a
            {
                Titolo = pratica.Titolo,
                Descrizione = pratica.Descrizione,
                DataInizioAttivitaStimata = pratica.DataInizioAttivitaStimata,
                DataFineAttivitaStimata = pratica.DataFineAttivitaStimata,
                Stato = "Conclusa",
                ID_Cliente = pratica.ID_Cliente,
                ID_UtenteResponsabile = pratica.ID_UtenteResponsabile,
                Budget = pratica.Budget,
                DataCreazione = pratica.DataCreazione,
                UltimaModifica = pratica.UltimaModifica,
                Note = pratica.Note,
                ID_Pratica_Originale = pratica.ID_Pratiche,
                DataArchiviazione = DateTime.Now,
                ID_UtenteArchiviazione = idUtente
            };
            db.Pratiche_a.Add(praticaArchivio);

            // ➕ Copia entità correlate (esempio base per Cluster)
            var cluster = db.Cluster.Where(c => c.ID_Pratiche == id).ToList();
            foreach (var c in cluster)
            {
                db.Cluster_a.Add(new Cluster_a
                {
                    ID_Pratiche = praticaArchivio.ID_Pratiche_a,
                    ID_Utente = c.ID_Utente,
                    TipoCluster = c.TipoCluster,
                    PercentualePrevisione = c.PercentualePrevisione,
                    DataAssegnazione = c.DataAssegnazione
                });
            }


            var relazioni = db.RelazionePraticheUtenti.Where(r => r.ID_Pratiche == id).ToList();
            foreach (var r in relazioni)
            {
                db.RelazionePraticheUtenti_a.Add(new RelazionePraticheUtenti_a
                {
                    ID_Pratiche = praticaArchivio.ID_Pratiche_a,
                    ID_Utente = r.ID_Utente,
                    Ruolo = r.Ruolo
                });
            }

            // Elimina la pratica originale e le sue entità collegate
            db.Cluster.RemoveRange(cluster);
            db.RelazionePraticheUtenti.RemoveRange(relazioni);
            db.Pratiche.Remove(pratica);

            db.SaveChanges();
        }
    }
}
