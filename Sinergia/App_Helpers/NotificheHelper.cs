using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public static class NotificheHelper
    {
        public static void InviaNotifica(int idUtente, string titolo, string descrizione, string tipo = "Sistema", string stato = "Attiva", int? idPratica = null)
        {
            using (var db = new SinergiaDB())
            {
                db.Notifiche.Add(new Notifiche
                {
                    ID_Utente = idUtente,
                    Titolo = titolo,
                    Descrizione = descrizione,
                    Tipo = tipo,
                    Stato = stato,
                    Contatore = 0,
                    Letto = false,
                    DataCreazione = DateTime.Now,
                    ID_Pratiche = idPratica
                });

                db.SaveChanges();
            }
        }

        public static void InviaNotificaAdmin(string titolo, string descrizione, string tipo = "Costi", string stato = "Critica")
        {
            using (var db = new SinergiaDB())
            {
                var adminList = db.Utenti
                    .Where(u => u.TipoUtente == "Admin" && u.Stato == "Attivo")
                    .ToList();

                foreach (var admin in adminList)
                {
                    InviaNotifica(admin.ID_Utente, titolo, descrizione, tipo, stato);
                }
            }
        }
    }
}