using Sinergia.App_Helpers;
using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Sinergia.Security
{
    public class Security
    {/// <summary>
     /// Cambia la password per un determinato utente.
     /// </summary>
        public static bool CambiaPassword(int idUtente, string nuovaPassword)
        {
            using (var ctx = new SinergiaDB ())
            {
                var utente = ctx.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                if (utente == null)
                    return false;

                // Eccezione: se admin, salviamo la password in chiaro
                if (utente.TipoUtente == "Admin")
                {
                    utente.PasswordHash = nuovaPassword;
                    utente.Salt = "";
                }
                else
                {
                    string nuovoSalt = Guid.NewGuid().ToString("N");
                    string nuovoHash = UserManager.CriptPassword(nuovaPassword, nuovoSalt);
                    utente.Salt = nuovoSalt;
                    utente.PasswordHash = nuovoHash;
                }

                utente.UltimaModifica = DateTime.Now;
                ctx.SaveChanges();
                return true;
            }
        }
    }
}

