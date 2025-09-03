using Sinergia.Model;
using Sinergia.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public class MenuHelper
    {
        public static List<MenuViewModel> GetMenuUtente(int idUtente, string tipoUtente, int? idAzienda = null)
        {
            using (var db = new SinergiaDB())
            {
                if (tipoUtente == "Admin")
                {
                    // Admin vede tutto
                    var queryAdmin = db.Menu
                        .Where(m => m.MostraNelMenu == "SI" && m.ÈValido == "SI");

                    if (idAzienda.HasValue)
                        queryAdmin = queryAdmin.Where(m => m.ID_Azienda == null || m.ID_Azienda == idAzienda.Value);

                    return queryAdmin.OrderBy(m => m.Ordine).Select(m => new MenuViewModel
                    {
                        ID_Menu = m.ID_Menu,
                        NomeMenu = m.NomeMenu,
                        DescrizioneMenu = m.DescrizioneMenu,
                        Percorso = m.Percorso,
                        Controller = m.Controller,
                        Azione = m.Azione,
                        CategoriaMenu = m.CategoriaMenu,
                        CategoriaMenu2 = m.CategoriaMenu2,
                        Icona = m.Icona,
                        RuoloPredefinito = m.RuoloPredefinito,
                        VoceSingola = m.VoceSingola,
                        Ordine = m.Ordine,
                        ÈValido = m.ÈValido,
                        MostraNelMenu = m.MostraNelMenu,
                        ID_Azienda = m.ID_Azienda,
                        AccessoRiservato = m.AccessoRiservato,
                        PermessoLettura = m.PermessoLettura,
                        PermessoAggiunta = m.PermessoAggiunta,
                        PermessoModifica = m.PermessoModifica,
                        PermessoEliminazione = m.PermessoEliminazione,
                        DataCreazione = m.DataCreazione,
                        UltimaModifica = m.UltimaModifica,
                        ID_UtenteCreatore = m.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = m.ID_UtenteUltimaModifica
                    }).ToList();
                }
                else
                {
                    // Join Menu + Permessi per utenti normali
                    var query = from m in db.Menu
                                join p in db.Permessi on m.ID_Menu equals p.ID_Menu
                                where p.ID_Utente == idUtente
                                      && m.MostraNelMenu == "SI"
                                      && m.ÈValido == "SI"
                                      && p.Vedi == true
                                select new MenuViewModel
                                {
                                    ID_Menu = m.ID_Menu,
                                    NomeMenu = m.NomeMenu,
                                    DescrizioneMenu = m.DescrizioneMenu,
                                    Percorso = m.Percorso,
                                    Controller = m.Controller,
                                    Azione = m.Azione,
                                    CategoriaMenu = m.CategoriaMenu,
                                    CategoriaMenu2 = m.CategoriaMenu2,
                                    Icona = m.Icona,
                                    RuoloPredefinito = m.RuoloPredefinito,
                                    VoceSingola = m.VoceSingola,
                                    Ordine = m.Ordine,
                                    ÈValido = m.ÈValido,
                                    MostraNelMenu = m.MostraNelMenu,
                                    ID_Azienda = m.ID_Azienda,
                                    AccessoRiservato = m.AccessoRiservato,
                                    PermessoLettura = p.Vedi.HasValue && p.Vedi.Value ? "SI" : "NO",
                                    PermessoAggiunta = p.Aggiungi.HasValue && p.Aggiungi.Value ? "SI" : "NO",
                                    PermessoModifica = p.Modifica.HasValue && p.Modifica.Value ? "SI" : "NO",
                                    PermessoEliminazione = p.Elimina.HasValue && p.Elimina.Value ? "SI" : "NO",
                                    DataCreazione = m.DataCreazione,
                                    UltimaModifica = m.UltimaModifica,
                                    ID_UtenteCreatore = m.ID_UtenteCreatore,
                                    ID_UtenteUltimaModifica = m.ID_UtenteUltimaModifica
                                };

                    if (idAzienda.HasValue)
                        query = query.Where(m => m.ID_Azienda == null || m.ID_Azienda == idAzienda.Value);

                    return query.OrderBy(m => m.Ordine).ToList();
                }
            }
        }
    }
}
