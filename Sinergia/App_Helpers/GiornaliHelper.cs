using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public class GiornaliHelper
    {

        public static void GeneraPrevisionale(  SinergiaDB db, int idProfessionista, DateTime dal, DateTime al, int idUtenteSistema)
        {
            dal = dal.Date; al = al.Date;

            // ===== chiavi esistenti per dedup =====
            var chiavi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prevEsistenti = db.Previsione
                .Where(e => e.Stato == "Previsionale"
                         && e.ID_Professionista == idProfessionista
                         && e.DataPrevisione >= dal && e.DataPrevisione <= al)
                .Select(e => new { e.TipoOperazione, e.ID_Pratiche, e.DataPrevisione, e.ImportoPrevisto })
                .ToList();

            foreach (var e in prevEsistenti)
            {
                var data = (e.DataPrevisione ?? DateTime.MinValue).Date;
                var imp = Math.Round((e.ImportoPrevisto ?? 0m), 2, MidpointRounding.AwayFromZero);
                chiavi.Add($"{e.TipoOperazione}|{(e.ID_Pratiche ?? 0)}|{data:yyyyMMdd}|{imp}");
            }

            var nuovi = new List<Previsione>();

            // ===== ENTRATE: pratiche del professionista con percentuale cluster =====
            var entrateRaw = (
                from p in db.Pratiche
                join cl in db.Cluster
                     on new { p.ID_Pratiche, ID_Utente = p.ID_UtenteResponsabile }
                     equals new { ID_Pratiche = cl.ID_Pratiche, cl.ID_Utente } into clj
                from cluster in clj.DefaultIfEmpty()
                where p.ID_UtenteResponsabile == idProfessionista
                      && p.Stato != "Eliminato"
                select new
                {
                    p.ID_Pratiche,
                    Percentuale = (cluster != null ? cluster.PercentualePrevisione : 0m), // dec "secco"
                    Budget = p.Budget,                                                    // dec "secco"
                    DataPrev = (DateTime?)(p.DataInizioAttivitaStimata ?? p.DataCreazione ?? DateTime.Today),
                    p.ID_UtenteCreatore
                }
            ).ToList();

            foreach (var r in entrateRaw)
            {
                var data = (r.DataPrev ?? DateTime.Today).Date;
                if (data < dal || data > al) continue;

                var importo = Math.Round((r.Budget * r.Percentuale) / 100m, 2, MidpointRounding.AwayFromZero);
                if (importo == 0m) continue;

                var k = $"Entrata|{r.ID_Pratiche}|{data:yyyyMMdd}|{importo}";
                if (chiavi.Contains(k)) continue;

                nuovi.Add(new Previsione
                {
                    ID_Pratiche = r.ID_Pratiche,
                    ID_Professionista = idProfessionista,
                    Percentuale = r.Percentuale,
                    TipoOperazione = "Entrata",
                    Descrizione = "Ricavo previsto da pratica",
                    ImportoPrevisto = importo,
                    DataPrevisione = data,
                    Stato = "Previsionale",
                    ID_UtenteCreatore = r.ID_UtenteCreatore
                });
            }

            // ===== USCITE: tutte le righe previsionali del professionista (qualsiasi Approvato) =====
            var usciteRaw = db.GenerazioneCosti
                .Where(g => g.ID_Utente == idProfessionista
                         && g.Stato == "Previsionale"
                         && g.DataRegistrazione >= dal && g.DataRegistrazione <= al)
                .Select(g => new
                {
                    g.ID_Pratiche,
                    g.Categoria,
                    g.Descrizione,
                    Importo = (decimal?)g.Importo,
                    g.DataRegistrazione,
                    g.ID_UtenteCreatore
                })
                .ToList();

            foreach (var g in usciteRaw)
            {
                var data = (g.DataRegistrazione ?? DateTime.Today).Date;
                var imp = Math.Round((g.Importo ?? 0m), 2, MidpointRounding.AwayFromZero);
                if (imp == 0m) continue;

                var k = $"Uscita|{(g.ID_Pratiche ?? 0)}|{data:yyyyMMdd}|{-imp}";
                if (chiavi.Contains(k)) continue;

                nuovi.Add(new Previsione
                {
                    ID_Pratiche = g.ID_Pratiche,
                    ID_Professionista = idProfessionista,
                    Percentuale = 100m,
                    TipoOperazione = "Uscita",
                    Descrizione = $"{(g.Categoria ?? "Costo")}{(string.IsNullOrEmpty(g.Descrizione) ? "" : " – " + g.Descrizione)}",
                    ImportoPrevisto = -imp,
                    DataPrevisione = data,
                    Stato = "Previsionale",
                    ID_UtenteCreatore = g.ID_UtenteCreatore
                });
            }

            if (!nuovi.Any()) return;

            db.Previsione.AddRange(nuovi);
            db.SaveChanges();

            var now = DateTime.Now;
            var arch = nuovi.Select(n => new Previsione_a
            {
                ID_PrevisioneOriginale = n.ID_Previsione,
                ID_Pratiche = n.ID_Pratiche,
                ID_Professionista = n.ID_Professionista,
                Percentuale = n.Percentuale,
                TipoOperazione = n.TipoOperazione,
                Descrizione = n.Descrizione,
                ImportoPrevisto = n.ImportoPrevisto,
                DataPrevisione = n.DataPrevisione,
                Stato = n.Stato,
                ID_UtenteCreatore = n.ID_UtenteCreatore,
                DataArchiviazione = now,
                ID_UtenteArchiviazione = idUtenteSistema,
                NumeroVersione = 1,
                ModificheTestuali = "Previsionale semplificato (entrate da pratiche + uscite da GC)"
            }).ToList();

            db.Previsione_a.AddRange(arch);
            db.SaveChanges();
        }



        public static void GeneraEconomicoDaAvvisiNetto( SinergiaDB db, int idProfessionista, DateTime dal, DateTime al, int idUtenteSistema)
        {
            dal = dal.Date;
            al = al.Date;

            // ===== Dedup: carico chiavi già presenti per il professionista nel periodo =====
            var chiaviEsistenti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var esistenti = db.Economico
                .Where(e => e.Stato == "Economico"
                         && ((DateTime?)e.DataRegistrazione ?? DateTime.MinValue) >= dal
                         && ((DateTime?)e.DataRegistrazione ?? DateTime.MinValue) <= al
                         && e.ID_Professionista == idProfessionista)
                .Select(e => new
                {
                    e.TipoOperazione,
                    e.ID_Pratiche,
                    e.DataRegistrazione,
                    e.ImportoEconomico
                })
                .ToList();

            foreach (var e in esistenti)
            {
                var data = ((DateTime?)e.DataRegistrazione ?? DateTime.MinValue).Date;
                var imp = Math.Round(((decimal?)e.ImportoEconomico ?? 0m), 2);
                string k = $"{e.TipoOperazione}|{(e.ID_Pratiche ?? 0)}|{data:yyyyMMdd}|{imp}";
                chiaviEsistenti.Add(k);
            }

            var nuovi = new List<Economico>();

            // ===== ENTRATE (NETTE da AvvisiParcella) =====
            // Netto: se c'è Totale e IVA -> Totale - IVA; altrimenti Importo + ContributoIntegrativo
            var avvisi = (
                from a in db.AvvisiParcella
                join p in db.Pratiche on a.ID_Pratiche equals p.ID_Pratiche
                where p.ID_UtenteResponsabile == idProfessionista
                      && a.Stato != "Annullato"
                      && (((DateTime?)a.DataAvviso ?? DateTime.Today) >= dal)
                      && (((DateTime?)a.DataAvviso ?? DateTime.Today) <= al)
                select new
                {
                    a.ID_AvvisoParcelle,
                    a.ID_Pratiche,
                    DataAvviso = (DateTime?)a.DataAvviso,
                    Totale = (decimal?)a.TotaleAvvisiParcella,
                    IVA = (decimal?)a.ImportoIVA,
                    Importo = (decimal?)a.Importo,
                    CI = (decimal?)a.ContributoIntegrativoImporto,
                    Note = a.Note,
                    p.ID_UtenteResponsabile
                }
            ).ToList();

            foreach (var a in avvisi)
            {
                var data = (a.DataAvviso ?? DateTime.Today).Date;

                decimal netto;
                var tot = a.Totale ?? 0m;
                var iva = a.IVA ?? 0m;
                if (tot > 0m)
                    netto = tot - iva;
                else
                    netto = (a.Importo ?? 0m) + (a.CI ?? 0m);

                netto = Math.Round(netto, 2);
                if (netto == 0m) continue;

                string key = $"Entrata|{(a.ID_Pratiche ?? 0)}|{data:yyyyMMdd}|{netto}";
                if (chiaviEsistenti.Contains(key)) continue;

                nuovi.Add(new Economico
                {
                    ID_Pratiche = a.ID_Pratiche,
                    ID_Professionista = a.ID_UtenteResponsabile,
                    TipoOperazione = "Entrata",
                    Descrizione = string.IsNullOrWhiteSpace(a.Note) ? "Avviso di Parcella (netto)" : a.Note,
                    ImportoEconomico = netto,
                    DataRegistrazione = data,
                    Stato = "Economico",
                    ID_UtenteCreatore = idUtenteSistema
                });
            }

            // ===== USCITE (costi PAGATI da GenerazioneCosti) =====
            var statiPagati = new[] { "Pagato", "Pagata", "Pagati" };

            var costiPagati = db.GenerazioneCosti
                .Where(g => (g.ID_Utente == idProfessionista)
                         && statiPagati.Contains(g.Stato)
                         && ((DateTime?)g.DataRegistrazione ?? DateTime.MinValue) >= dal
                         && ((DateTime?)g.DataRegistrazione ?? DateTime.MinValue) <= al)
                .Select(g => new
                {
                    g.ID_GenerazioneCosto,
                    g.ID_Pratiche,
                    g.ID_Utente,
                    g.Categoria,
                    g.Descrizione,
                    Importo = (decimal?)g.Importo,
                    Data = (DateTime?)g.DataRegistrazione,
                    g.ID_UtenteCreatore
                })
                .ToList();

            foreach (var g in costiPagati)
            {
                var data = (g.Data ?? DateTime.Today).Date;
                var imp = Math.Round((g.Importo ?? 0m), 2);
                if (imp == 0m) continue;

                // chiave con importo negativo (uscita)
                string key = $"Uscita|{(g.ID_Pratiche ?? 0)}|{data:yyyyMMdd}|{-imp}";
                if (chiaviEsistenti.Contains(key)) continue;

                nuovi.Add(new Economico
                {
                    ID_Pratiche = g.ID_Pratiche,
                    ID_Professionista = g.ID_Utente ?? idProfessionista,
                    TipoOperazione = "Uscita",
                    Descrizione = $"{(g.Categoria ?? "Costo")}{(string.IsNullOrEmpty(g.Descrizione) ? "" : " – " + g.Descrizione)}",
                    ImportoEconomico = -imp,
                    DataRegistrazione = data,
                    Stato = "Economico",
                    ID_UtenteCreatore = g.ID_UtenteCreatore
                });
            }

            if (!nuovi.Any()) return;

            // ===== Salvataggi =====
            db.Economico.AddRange(nuovi);
            db.SaveChanges();

            var now = DateTime.Now;
            var archivio = nuovi.Select(n => new Economico_a
            {
                ID_EconomicoOriginale = n.ID_Economico,
                ID_Pratiche = n.ID_Pratiche,
                ID_Professionista = n.ID_Professionista,
                TipoOperazione = n.TipoOperazione,
                Descrizione = n.Descrizione,
                ImportoEconomico = n.ImportoEconomico,
                DataRegistrazione = n.DataRegistrazione,
                Stato = n.Stato,
                ID_UtenteCreatore = n.ID_UtenteCreatore,
                DataArchiviazione = now,
                ID_UtenteArchiviazione = idUtenteSistema,
                NumeroVersione = 1,
                ModificheTestuali = "Economico (avvisi netti + costi pagati) – inserimento semplice con dedup"
            }).ToList();

            db.Economico_a.AddRange(archivio);
            db.SaveChanges();
        }



        public static void GeneraFinanziario(   SinergiaDB db,   DateTime dal,  DateTime al,  int idUtenteSistema)
        {
            // Utente corrente (es. 20) e relativo Cliente/Owner (es. 8)
            int idProfessionista = UserManager.GetIDUtenteAttivo();

            // Se hai un metodo affidabile per ricavare l'ID_Cliente dal professionista, usalo qui.
            // In alternativa, se lo tieni in sessione, sostituisci questa riga.
            // Esempio robusto: prova ID_UtenteCollegato, poi uguale all'utente.
            int idCliente = db.OperatoriSinergia
                .Where(os => os.ID_UtenteCollegato == idProfessionista || os.ID_Cliente == idProfessionista)
                .Select(os => os.ID_Cliente)
                .FirstOrDefault();

            dal = dal.Date;
            al = al.Date;

            // ---------- Dedup ----------
            var chiavi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var finEsistenti = db.Finanziario
                .Where(f => f.Stato == "Finanziario"
                         && f.ID_Professionista == idProfessionista
                         && f.DataIncasso >= dal && f.DataIncasso <= al)
                .Select(f => new { f.TipoOperazione, f.ID_Pratiche, f.DataIncasso, f.ImportoFinanziario })
                .ToList();

            foreach (var f in finEsistenti)
            {
                var data = (f.DataIncasso ?? DateTime.MinValue).Date;
                var imp = Math.Round((f.ImportoFinanziario ?? 0m), 2, MidpointRounding.AwayFromZero);
                chiavi.Add($"{f.TipoOperazione}|{(f.ID_Pratiche ?? 0)}|{data:yyyyMMdd}|{imp}");
            }

            var nuovi = new List<Finanziario>();

            // ======================================
            // ENTRATE: Compensi su pratiche del cliente (owner)
            // ======================================
            var entrate = (
                from cp in db.CompensiPratica
                join p in db.Pratiche on cp.ID_Pratiche equals p.ID_Pratiche
                where cp.Tipo == "Incasso"
                      && cp.DataInserimento >= dal && cp.DataInserimento <= al
                      && p.ID_Owner == idCliente                      // <- owner della pratica è il cliente del professionista
                select new
                {
                    ID_Pratiche = (int?)cp.ID_Pratiche,
                    DataIncasso = cp.DataInserimento,
                    Importo = (decimal?)cp.Importo,
                    Descrizione = cp.Descrizione,
                    ID_UtenteCreatore = cp.ID_UtenteCreatore
                }
            ).ToList();

            foreach (var e in entrate)
            {
                var data = e.DataIncasso.Date;
                var imp = Math.Round((e.Importo ?? 0m), 2, MidpointRounding.AwayFromZero);
                if (imp == 0m) continue;

                var k = $"Entrata|{(e.ID_Pratiche ?? 0)}|{data:yyyyMMdd}|{imp}";
                if (chiavi.Contains(k)) continue;

                nuovi.Add(new Finanziario
                {
                    ID_Professionista = idProfessionista,
                    ID_Pratiche = e.ID_Pratiche,
                    TipoOperazione = "Entrata",
                    Descrizione = string.IsNullOrWhiteSpace(e.Descrizione) ? "Incasso" : e.Descrizione,
                    ImportoFinanziario = imp,
                    DataIncasso = data,
                    Stato = "Finanziario",
                    ID_UtenteCreatore = e.ID_UtenteCreatore
                });
            }

            // ======================================
            // USCITE: GenerazioneCosti (Generale/Prof, Team, Progetto)
            // ======================================
            var statiPagati = new[] { "Pagato", "Pagata", "Pagati" };

            // Team del cliente (membership è su ID_Professionista = ID_Cliente)
            var teamDelCliente = db.MembriTeam
                .Where(mt => mt.ID_Professionista == idCliente && mt.Attivo)
                .Select(mt => mt.ID_Team)
                .ToList();

            var uscite = (
                from gc in db.GenerazioneCosti
                where statiPagati.Contains(gc.Stato)
                      && gc.DataRegistrazione >= dal && gc.DataRegistrazione <= al

                // Pratica via CostiPratica se ID_Pratiche non c'è
                let idPraticaDaCostoPratica = db.CostiPratica
                    .Where(cp => cp.ID_CostoPratica == gc.ID_Riferimento)
                    .Select(cp => (int?)cp.ID_Pratiche)
                    .FirstOrDefault()

                // Pratica "risolta": diretta o via CostiPratica
                let idPraticaRisolta = (int?)(gc.ID_Pratiche ?? idPraticaDaCostoPratica)

                // Inclusione:
                // 1) Costo personale/generale associato all'utente (20) OPPURE al cliente (8)
                let isProfOGenerale = (gc.ID_Utente == idProfessionista) || (gc.ID_Utente == idCliente)

                // 2) Costo Team dove il cliente è membro
                let isTeam = (gc.ID_Team != null) && teamDelCliente.Contains(gc.ID_Team.Value)

                // 3) Costo Progetto dove la pratica risolta appartiene al cliente (owner)
                let isProgetto = (gc.Categoria == "Costo Progetto")
                                 && (idPraticaRisolta != null)
                                 && db.Pratiche.Any(p => p.ID_Pratiche == idPraticaRisolta && p.ID_Owner == idCliente)

                where isProfOGenerale || isTeam || isProgetto

                select new
                {
                    ID_Pratiche = idPraticaRisolta ?? (int?)gc.ID_Pratiche,
                    Categoria = gc.Categoria,
                    Descrizione = gc.Descrizione,
                    Importo = (decimal?)gc.Importo,
                    DataRegistrazione = gc.DataRegistrazione,
                    ID_UtenteCreatore = gc.ID_UtenteCreatore
                }
            ).ToList();

            foreach (var u in uscite)
            {
                var data = (u.DataRegistrazione ?? DateTime.Today).Date;
                var imp = Math.Round((u.Importo ?? 0m), 2, MidpointRounding.AwayFromZero);
                if (imp == 0m) continue;

                var k = $"Uscita|{(u.ID_Pratiche ?? 0)}|{data:yyyyMMdd}|{-imp}";
                if (chiavi.Contains(k)) continue;

                var descr = string.IsNullOrWhiteSpace(u.Descrizione)
                    ? (u.Categoria ?? "Costo")
                    : $"{(u.Categoria ?? "Costo")} – {u.Descrizione}";

                nuovi.Add(new Finanziario
                {
                    ID_Professionista = idProfessionista,
                    ID_Pratiche = u.ID_Pratiche,
                    TipoOperazione = "Uscita",
                    Descrizione = descr,
                    ImportoFinanziario = -imp,
                    DataIncasso = data,
                    Stato = "Finanziario",
                    ID_UtenteCreatore = u.ID_UtenteCreatore
                });
            }

            // ---------- Salvataggio ----------
            if (!nuovi.Any()) return;

            db.Finanziario.AddRange(nuovi);
            db.SaveChanges();

            var now = DateTime.Now;
            var arch = nuovi.Select(n => new Finanziario_a
            {
                ID_FinanziarioOriginale = n.ID_Finanziario,
                ID_Professionista = n.ID_Professionista,
                ID_Pratiche = n.ID_Pratiche,
                TipoOperazione = n.TipoOperazione,
                Descrizione = n.Descrizione,
                ImportoFinanziario = n.ImportoFinanziario,
                DataIncasso = n.DataIncasso,
                Stato = n.Stato,
                ID_UtenteCreatore = n.ID_UtenteCreatore,
                DataArchiviazione = now,
                ID_UtenteArchiviazione = idUtenteSistema,
                NumeroVersione = 1,
                ModificheTestuali = "Finanziario (entrate da CompensiPratica owner, uscite: utente/cliente, team, progetto)"
            }).ToList();

            db.Finanziario_a.AddRange(arch);
            db.SaveChanges();
        }


    }
}