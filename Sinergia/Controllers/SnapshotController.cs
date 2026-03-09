using Sinergia.Model;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Sinergia.Controllers
{
    public class SnapshotController : Controller
    {
        private SinergiaDB db = new SinergiaDB();

        // ======================================================
        // 🔍 Recupera snapshot
        // ======================================================
        [HttpGet]
        public JsonResult GetSnapshot(string tipo, int id)
        {
            try
            {
                tipo = (tipo ?? "").Trim().ToUpper();

                SnapshotModali snap = null;

                if (tipo == "PRATICA")
                {
                    snap = db.SnapshotModali
                        .FirstOrDefault(x => x.ID_Pratiche == id);
                }
                else if (tipo == "AVVISO")
                {
                    snap = db.SnapshotModali
                        .FirstOrDefault(x => x.ID_AvvisoParcella == id);
                }
                else if (tipo == "INCASSO")
                {
                    snap = db.SnapshotModali
                        .FirstOrDefault(x => x.ID_Incasso == id);
                }

                if (snap == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Snapshot non trovato"
                    }, JsonRequestBehavior.AllowGet);
                }

                // =========================
                // DESERIALIZZO JSON SNAPSHOT
                // =========================

                dynamic snapshot =
                    Newtonsoft.Json.JsonConvert.DeserializeObject(snap.HtmlSnapshot);


                // ======================================================
                // JOIN RESPONSABILE → UTENTI
                // ======================================================

                if (snapshot.ID_UtenteResponsabile != null)
                {
                    int idResp = (int)snapshot.ID_UtenteResponsabile;

                    var responsabile = db.Utenti
                        .FirstOrDefault(u => u.ID_Utente == idResp);

                    if (responsabile != null)
                    {
                        snapshot.Responsabile =
                            responsabile.Nome + " " + responsabile.Cognome;
                    }
                }


                // ======================================================
                // JOIN OWNER → OPERATORI SINERGIA
                // ======================================================

                if (snapshot.ID_Owner != null)
                {
                    int idOwner = (int)snapshot.ID_Owner;

                    var owner = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_UtenteCollegato == idOwner);

                    if (owner != null)
                    {
                        snapshot.Owner =
                            owner.Nome + " " + owner.Cognome;
                    }
                }

                // UTENTI ASSOCIATI
                // ======================================================

                if (snapshot.utentiAssociati != null)
                {
                    foreach (var u in snapshot.utentiAssociati)
                    {
                        int idOperatore = (int)u.ID_Utente;

                        var operatore = db.OperatoriSinergia
                            .FirstOrDefault(o => o.ID_Operatore == idOperatore);

                        if (operatore != null)
                        {
                            u.Nome = operatore.Nome + " " + operatore.Cognome;
                        }
                    }
                }


                // SERIALIZZO DI NUOVO IL JSON ARRICCHITO

                string snapshotArricchito =
                    Newtonsoft.Json.JsonConvert.SerializeObject(snapshot);


                return Json(new
                {
                    success = true,
                    tipo = snap.Tipo,
                    data = snap.DataCreazione,
                    snapshot = snapshotArricchito
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // ======================================================
        // 💾 Salva snapshot
        // ======================================================
        [HttpPost]
        public JsonResult SalvaSnapshot(
            string tipo,
            int? idPratica,
            int? idAvviso,
            int? idIncasso,
            string json)
        {
            try
            {
                tipo = (tipo ?? "").Trim().ToUpper();

                SnapshotModali snap = null;

                if (tipo == "PRATICA" && idPratica.HasValue)
                {
                    snap = db.SnapshotModali
                        .FirstOrDefault(x => x.ID_Pratiche == idPratica);
                }
                else if (tipo == "AVVISO" && idAvviso.HasValue)
                {
                    snap = db.SnapshotModali
                        .FirstOrDefault(x => x.ID_AvvisoParcella == idAvviso);
                }
                else if (tipo == "INCASSO" && idIncasso.HasValue)
                {
                    snap = db.SnapshotModali
                        .FirstOrDefault(x => x.ID_Incasso == idIncasso);
                }

                if (snap == null)
                {
                    snap = new SnapshotModali
                    {
                        Tipo = tipo,
                        ID_Pratiche = idPratica,
                        ID_AvvisoParcella = idAvviso,
                        ID_Incasso = idIncasso,
                        HtmlSnapshot = json,
                        DataCreazione = DateTime.Now
                    };

                    db.SnapshotModali.Add(snap);
                }
                else
                {
                    snap.HtmlSnapshot = json;
                    snap.DataCreazione = DateTime.Now;
                }

                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }
}