using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public class UserManager
    {/// <summary>
     /// Autentica un utente in base a Nome, Cognome e Password (con Salt).
     /// </summary>
     /// <param name="nome">Nome dell'utente</param>
     /// <param name="cognome">Cognome dell'utente</param>
     /// <param name="password">Password in chiaro da verificare</param>
     /// <returns>L'oggetto Utenti se le credenziali sono corrette, altrimenti null</returns>
        public static Utenti AutenticaUtente(string nome, string password)
        {
            using (var ctx = new SinergiaDB())
            {
                // Cerca l'utente per nome (e stato attivo)
                var utente = ctx.Utenti.FirstOrDefault(u =>
            (
                (u.TipoUtente == "Admin" && u.Nome.ToLower() == nome.ToLower()) ||
                (u.TipoUtente != "Admin" && u.NomeAccount.ToLower() == nome.ToLower())
                 ) && u.Stato == "Attivo"
                    );

                if (utente == null)
                    return null;

                // 🔐 Accesso admin: password in chiaro
                if (utente.TipoUtente == "Admin")
                {
                    if (utente.PasswordHash == password || utente.PasswordTemporanea == password)
                        return utente;

                    return null;
                }

                // 🔐 Accesso con password temporanea
                if (!string.IsNullOrEmpty(utente.PasswordTemporanea) && utente.PasswordTemporanea == password)
                {
                    return utente;
                }

                // 🔐 Accesso standard: hash + salt
                if (!string.IsNullOrEmpty(utente.Salt) && !string.IsNullOrEmpty(utente.PasswordHash))
                {
                    var hashedInput = CriptPassword(password, utente.Salt);

                    if (hashedInput == utente.PasswordHash)
                        return utente;
                }

                return null; // Altrimenti accesso negato
            }
        }

        public static int GetIDUtenteCollegato()
        {
            var utente = HttpContext.Current.Session["User"] as Sinergia.Model.Utenti;
            return utente?.ID_Utente ?? 0; // Ritorna 0 se non trovato
        }

        public static Utenti GetUtenteCollegato()
        {
            return HttpContext.Current.Session["User"] as Utenti;
        }

        //  serve per impersonificare il professionista quando si e admin 
        public static int GetIDUtenteAttivo()
        {
            // 👥 Se è in impersonificazione, usa quell'ID
            if (HttpContext.Current.Session["ID_UtenteImpers"] != null)
            {
                return (int)HttpContext.Current.Session["ID_UtenteImpers"];
            }

            // Altrimenti usa l'utente loggato
            return GetIDUtenteCollegato();
        }

        public static string GetTipoUtente()
        {
            int idUtente = GetIDUtenteCollegato();
            using (var db = new SinergiaDB())
            {
                return db.Utenti
                    .Where(u => u.ID_Utente == idUtente)
                    .Select(u => u.TipoUtente)
                    .FirstOrDefault() ?? "";
            }
        }



        /// <summary>
        /// Cripta una password con algoritmo custom usando un salt (personalizzabile).
        /// </summary>
        /// <param name="password">Password in chiaro</param>
        /// <param name="salt">Salt registrato per l'utente</param>
        /// <returns>Password cifrata</returns>
        public static string CriptPassword(string password, string salt)
        {
            // Esempio semplice con SHA256 + salt (puoi sostituire con tua logica custom)
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var combined = password + salt;
                var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }
}