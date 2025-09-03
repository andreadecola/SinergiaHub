using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    /// <summary>
    /// Classe helper per recuperare le informazioni relative all'azienda selezionata tramite cookie.
    /// </summary>
    public class AziendaHelper
    {/// <summary>
     /// Restituisce il nome dell'azienda selezionata, in maiuscolo.
     /// </summary>
     /// <param name="aziendaCookie">Cookie che contiene il nome dell'azienda</param>
     /// <returns>Nome azienda in maiuscolo, oppure stringa vuota se non presente</returns>
        public static string GetNomeAzienda(int idAzienda)
        {
            using (var db = new SinergiaDB())
            {
                var azienda = db.Clienti.FirstOrDefault(c => c.ID_Cliente == idAzienda && c.TipoCliente == "Azienda");
                return azienda?.Nome?.ToUpper() ?? "";
            }
        }


        /// <summary>
        /// Restituisce l'ID dell'azienda selezionata.
        /// </summary>
        /// <param name="aziendaIdCookie">Cookie che contiene l'ID dell'azienda</param>
        /// <returns>ID azienda come stringa, oppure stringa vuota se non presente</returns>
        public static string GetIDAzienda(HttpCookie aziendaIdCookie)
        {
            if (aziendaIdCookie != null && !string.IsNullOrEmpty(aziendaIdCookie.Value))
                return aziendaIdCookie.Value;

            return "";
        }
    }
}