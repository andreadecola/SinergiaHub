using Sinergia.Model;
using Sinergia.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Sinergia.App_Helpers
{
    public class DashboardHelper
    {
        public static DashboardViewModel GetDashboard(int idUtenteAttivo, int idCliente, string nomeCliente, int intervalloGiorni = 30)
        {
            using (var db = new SinergiaDB())
            {
                var utenteTarget = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteAttivo);
                if (utenteTarget == null) return new DashboardViewModel(); // opzionale fallback

                string tipoUtente = utenteTarget.TipoUtente;


                DateTime dataLimite = DateTime.Now.AddDays(-intervalloGiorni);

                var pratiche = new List<PraticaViewModel>();
                var documenti = new List<DocumentiPraticaViewModel>();
                var collaboratoriAssegnati = new List<UtenteViewModel>();
                var professionistiSeguiti = new List<ProfessionistiViewModel>();
                decimal? utilePersonale = null;

                if (tipoUtente == "Admin" || tipoUtente == "Professionista")
                {
                    pratiche = db.Pratiche
                        .Where(p => p.Stato == "Attiva" && p.ID_Cliente == idCliente)
                        .OrderByDescending(p => p.UltimaModifica ?? p.DataCreazione)
                        .Take(5)
                        .Select(p => new PraticaViewModel
                        {
                            ID_Pratiche = p.ID_Pratiche,
                            Titolo = p.Titolo,
                            Descrizione = p.Descrizione,
                            Stato = p.Stato,
                            Budget = p.Budget,
                            DataInizioAttivitaStimata = p.DataInizioAttivitaStimata,
                            DataFineAttivitaStimata = p.DataFineAttivitaStimata,
                            ID_UtenteUltimaModifica = p.ID_UtenteUltimaModifica,
                            TipoCliente = db.OperatoriSinergia.Where(c => c.ID_Cliente == p.ID_Cliente).Select(c => c.TipoCliente).FirstOrDefault()
                        }).ToList();
                }
                else if (tipoUtente == "Collaboratore")
                {
                    var professionisti = db.RelazioneUtenti
                        .Where(r => r.ID_UtenteAssociato == idUtenteAttivo && r.Stato == "Attivo")
                        .Select(r => r.ID_Utente)
                        .Distinct()
                        .ToList();

                    // ✅ Pratiche collegate a questi professionisti
                    pratiche = db.Pratiche
                        .Where(p => p.Stato == "Attiva" && professionisti.Contains(p.ID_Cliente))
                        .OrderByDescending(p => p.UltimaModifica ?? p.DataCreazione)
                        .Take(5)
                        .Select(p => new PraticaViewModel
                        {
                            ID_Pratiche = p.ID_Pratiche,
                            Titolo = p.Titolo,
                            Descrizione = p.Descrizione,
                            Stato = p.Stato,
                            Budget = p.Budget,
                            DataInizioAttivitaStimata = p.DataInizioAttivitaStimata,
                            DataFineAttivitaStimata = p.DataFineAttivitaStimata,
                            ID_UtenteUltimaModifica = p.ID_UtenteUltimaModifica,
                            TipoCliente = db.OperatoriSinergia.Where(c => c.ID_Cliente == p.ID_Cliente).Select(c => c.TipoCliente).FirstOrDefault()
                        }).ToList();

                    // ✅ Lista completa dei professionisti a cui è assegnato
                    professionistiSeguiti = db.OperatoriSinergia
                        .Where(c => professionisti.Contains(c.ID_Cliente))
                        .Select(c => new ProfessionistiViewModel
                        {
                            ID_Professionista = c.ID_Cliente,
                            Nome = c.Nome,
                            Cognome = c.Cognome,
                            Email = c.MAIL1,
                            Telefono = c.Telefono,
                            TipoProfessionista = c.TipoProfessionista,
                            NomePractice = db.Professioni.Where(p => p.ProfessioniID == c.ID_Professione).Select(p => p.Descrizione).FirstOrDefault()
                        }).ToList();
                }


                // ✅ Carica collaboratori se il cliente selezionato è un professionista (valido anche se utente è Admin)
                var idUtenteProfessionista = db.OperatoriSinergia
                    .Where(c => c.ID_Cliente == idCliente && c.TipoCliente == "Professionista")
                    .Select(c => c.ID_UtenteCollegato)
                    .FirstOrDefault();

                if (idUtenteProfessionista.HasValue)
                {
                    collaboratoriAssegnati = db.RelazioneUtenti
                        .Where(r => r.ID_Utente == idUtenteProfessionista.Value && r.Stato == "Attivo")
                        .Join(db.Utenti, r => r.ID_UtenteAssociato, u => u.ID_Utente, (rel, u) => new UtenteViewModel
                        {
                            ID_Utente = u.ID_Utente,
                            Nome = u.Nome,
                            Cognome = u.Cognome,
                            Stato = u.Stato,
                            TipoUtente = u.TipoUtente
                        }).ToList();
                }

                // 📄 Documenti recenti delle pratiche
                var idPratiche = pratiche.Select(p => p.ID_Pratiche).ToList();

                documenti = db.DocumentiPratiche
                    .Where(d => d.Stato == "Attivo" && d.DataCaricamento >= dataLimite && idPratiche.Contains(d.ID_Pratiche))
                    .Select(d => new DocumentiPraticaViewModel
                    {
                        ID_Documento = d.ID_Documento,
                        ID_Pratiche = d.ID_Pratiche,
                        NomeFile = d.NomeFile,
                        Estensione = d.Estensione,
                        TipoContenuto = d.TipoContenuto,
                        DataCaricamento = d.DataCaricamento,
                        Stato = d.Stato,
                        ID_UtenteCaricamento = d.ID_UtenteCaricamento
                    }).ToList();

                return new DashboardViewModel
                {
                    NomeCliente = nomeCliente,
                    Pratiche = pratiche,
                    DocumentiRecenti = documenti,
                    CollaboratoriAssegnati = collaboratoriAssegnati,
                    ProfessionistiSeguiti = professionistiSeguiti,
                    UtilePersonale = utilePersonale,
                    IntervalloGiorni = intervalloGiorni
                };
            }
        }





        private static string GetRuoloUtente(SinergiaDB db, int idUtente)
        {
            return db.Utenti
                     .Where(u => u.ID_Utente == idUtente)
                     .Select(u => u.TipoUtente)
                     .FirstOrDefault();
        }

        public static List<SelectListItem> GetClientiDisponibiliPerNavbar(int idUtente, string tipoUtente)
        {
            using (var db = new SinergiaDB())
            {
                var lista = new List<SelectListItem>();

                if (tipoUtente == "Admin")
                {
                    // ✅ Solo professionisti, niente aziende
                    var clienti = db.OperatoriSinergia
                        .Where(c => c.Stato == "Attivo" && c.TipoCliente == "Professionista")
                        .OrderBy(c => c.Nome)
                        .Select(c => new SelectListItem
                        {
                            Value = "P_" + c.ID_Cliente.ToString(),
                            Text = c.Nome + " " + (c.Cognome ?? "") + " | Professionista"
                        }).ToList();


                    lista.AddRange(clienti);
                }
                else if (tipoUtente == "Professionista")
                {
                    // Singolo cliente professionista legato all'utente
                    var cliente = db.OperatoriSinergia.FirstOrDefault(c =>
                        c.ID_UtenteCollegato == idUtente &&
                        c.TipoCliente == "Professionista" &&
                        c.Stato == "Attivo");

                    if (cliente != null)
                    {
                        lista.Add(new SelectListItem
                        {
                            Value = "P_" + cliente.ID_Cliente.ToString(), // 👈 prefisso P_
                            Text = cliente.Nome + " " + (cliente.Cognome ?? "") + " | " + cliente.TipoCliente
                        });
                    }
                }

                return lista;
            }
        }

        public static List<SelectListItem> GetUtentiDisponibiliPerPratica(int idCliente, int idUtente)
        {
            using (var db = new SinergiaDB())
            {
                var lista = new List<SelectListItem>();

                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                if (utenteCorrente == null)
                    return lista;

                var cliente = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Cliente == idCliente && c.Stato == "Attivo");
                if (cliente == null || cliente.ID_Owner == null)
                    return lista;

                var owner = db.Utenti.FirstOrDefault(u => u.ID_Utente == cliente.ID_Owner.Value && u.Stato == "Attivo");
                if (owner == null)
                    return lista;

                // Se Admin o Owner possono vedere tutti
                if (utenteCorrente.TipoUtente == "Admin" || idUtente == owner.ID_Utente)
                {
                    // 👤 Owner
                    lista.Add(new SelectListItem
                    {
                        Value = owner.ID_Utente.ToString(),
                        Text = $"{owner.Nome} {owner.Cognome} (Owner)"
                    });

                    // 👥 Utenti collegati
                    var utentiCollegati = db.RelazioneUtenti
                        .Where(r => r.ID_Utente == owner.ID_Utente && r.Stato == "Attivo")
                        .Join(db.Utenti, r => r.ID_UtenteAssociato, u => u.ID_Utente, (rel, u) => u)
                        .Where(u => u.Stato == "Attivo")
                        .ToList();

                    foreach (var u in utentiCollegati)
                    {
                        lista.Add(new SelectListItem
                        {
                            Value = u.ID_Utente.ToString(),
                            Text = $"{u.Nome} {u.Cognome}"
                        });
                    }
                }

                return lista;
            }
        }

    }
}
