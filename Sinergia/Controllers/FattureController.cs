//using Sinergia.ActionFilters;
//using Sinergia.App_Helpers;
//using Sinergia.Model;
//using Sinergia.Models;
//using System;
//using System.Collections.Generic;
//using System.Configuration;
//using System.IO;
//using System.Linq;
//using System.Net.Http.Headers;
//using System.Net.Http;
//using System.Text;
//using Newtonsoft.Json;
//using System.Threading.Tasks;
//using System.Web;
//using System.Web.Mvc;
//using System.Xml.Linq;


//namespace Sinergia.Controllers
//{
//    [PermissionsActionFilter]
//    public class FattureController : Controller
//    {

//        private SinergiaDB db = new SinergiaDB();
//        // GET: Fatture
//        public ActionResult GestioneFatture()
//        {
//            return View("~/Views/Fatture/GestioneFatture.cshtml");
//        }

//        [HttpGet]
//        public ActionResult GestioneFattureList(string ricerca = "", int giorniFiltro = 30, string tipoFiltro = "Tutti")
//        {
//            DateTime dataLimite = DateTime.Now.AddDays(-giorniFiltro);

//            var query = db.Pratiche.Where(p => p.Stato != "Eliminato");

//            if (!string.IsNullOrEmpty(ricerca))
//            {
//                query = query.Where(p => p.Titolo.Contains(ricerca) || p.Descrizione.Contains(ricerca));
//            }

//            query = query.Where(p => p.DataCreazione >= dataLimite);

//            var praticheList = (from p in query
//                                join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
//                                join u in db.Utenti on p.ID_UtenteResponsabile equals u.ID_Utente into utentiJoin
//                                from utenteResp in utentiJoin.DefaultIfEmpty()
//                                select new PraticaViewModel
//                                {
//                                    ID_Pratiche = p.ID_Pratiche,
//                                    Titolo = p.Titolo,
//                                    Descrizione = p.Descrizione,
//                                    DataInizioAttivitaStimata = p.DataInizioAttivitaStimata,
//                                    DataFineAttivitaStimata = p.DataFineAttivitaStimata,
//                                    Stato = p.Stato,
//                                    ID_Cliente = p.ID_Cliente,
//                                    NomeCliente = c.TipoCliente == "Professionista"
//                                                  ? c.Nome + " " + c.Cognome
//                                                  : c.Nome,
//                                    TipoCliente = c.TipoCliente,
//                                    ID_UtenteResponsabile = p.ID_UtenteResponsabile,
//                                    NomeUtenteResponsabile = utenteResp != null ? utenteResp.Nome + " " + utenteResp.Cognome : "",
//                                    Budget = p.Budget,
//                                    DataCreazione = p.DataCreazione,
//                                    UltimaModifica = p.UltimaModifica,
//                                    Note = p.Note
//                                }).ToList();

//            if (tipoFiltro == "Azienda")
//                praticheList = praticheList.Where(p => p.TipoCliente == "Azienda").ToList();
//            else if (tipoFiltro == "Professionista")
//                praticheList = praticheList.Where(p => p.TipoCliente == "Professionista").ToList();

//            // 🔍 Solo pratiche con fatture (emesse o ricevute)
//            var idPraticheConFatture = db.GiornaleCreditoDebito
//                                         .Where(g => g.FatturaEmessa == true || g.FatturaRicevuta == true)
//                                         .Select(g => g.ID_Pratiche)
//                                         .Distinct()
//                                         .ToList();

//            praticheList = praticheList
//                .Where(p => idPraticheConFatture.Contains(p.ID_Pratiche))
//                .ToList();

//            int idUtente = UserManager.GetIDUtenteCollegato();
//            var utenteCorrente = db.Utenti.Find(idUtente);

//            var permessiUtente = new PermessiViewModel { Permessi = new List<PermessoSingoloViewModel>() };

//            if (utenteCorrente?.TipoUtente == "Admin")
//            {
//                permessiUtente.Permessi.Add(new PermessoSingoloViewModel
//                {
//                    Aggiungi = true,
//                    Modifica = true,
//                    Elimina = true
//                });
//            }
//            else if (utenteCorrente != null)
//            {
//                var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtente).ToList();
//                foreach (var p in permessiDb)
//                {
//                    permessiUtente.Permessi.Add(new PermessoSingoloViewModel
//                    {
//                        Aggiungi = p.Aggiungi ?? false,
//                        Modifica = p.Modifica ?? false,
//                        Elimina = p.Elimina ?? false
//                    });
//                }

//                var clientiAssegnati = db.RelazioneUtenti
//                    .Where(r => r.ID_Utente == idUtente && r.Stato == "Attivo")
//                    .Select(r => r.ID_UtenteAssociato)
//                    .ToList();

//                praticheList = praticheList
//                    .Where(p => clientiAssegnati.Contains(p.ID_Cliente))
//                    .ToList();
//            }

//            ViewBag.Permessi = permessiUtente;

//            return PartialView("~/Views/Fatture/_GestioneFattureList.cshtml", praticheList);
//        }

//        // SEZIONE FATTURE 

//        [HttpGet]
//        public ActionResult GestioneFattureView(int idPratica)
//        {
//            var fatture = db.GiornaleCreditoDebito
//                .Where(g => g.ID_Pratiche == idPratica &&
//                       (g.FatturaEmessa == true || g.FatturaRicevuta == true))
//                .OrderByDescending(g => g.DataOperazione)
//                .ToList();

//            ViewBag.ID_Pratiche = idPratica;

//            return View("~/Views/Fatture/GestioneFatture.cshtml", fatture);
//        }
//        [HttpGet]
//        public JsonResult GetFattura(int idOperazione)
//        {
//            var voce = db.GiornaleCreditoDebito
//                .Where(g => g.ID_Operazione == idOperazione)
//                .Select(g => new
//                {
//                    g.ID_Operazione,
//                    g.ID_Pratiche,
//                    g.ID_Cliente,
//                    g.TipoCliente,
//                    g.TipoOperazione,
//                    g.Importo,
//                    g.Descrizione,
//                    g.Stato,
//                    g.FatturaEmessa,
//                    g.FatturaRicevuta,
//                    g.NumeroFattura,
//                    g.DataFattura,
//                    g.NoteFattura,
//                    g.AliquotaIva,
//                    g.CausaleIva
//                })
//                .FirstOrDefault();

//            if (voce == null)
//                return Json(new { success = false, message = "Fattura non trovata." }, JsonRequestBehavior.AllowGet);

//            return Json(new { success = true, fattura = voce }, JsonRequestBehavior.AllowGet);
//        }

//        [HttpGet]
//        public JsonResult GetFattureByPratica(int idPratica)
//        {
//            var fatture = db.GiornaleCreditoDebito
//                .Where(g => g.ID_Pratiche == idPratica &&
//                           (g.FatturaEmessa == true || g.FatturaRicevuta == true))
//                .Select(g => new
//                {
//                    g.ID_Operazione,
//                    g.TipoOperazione,
//                    g.Importo,
//                    g.Descrizione,
//                    g.DataOperazione,
//                    g.Stato,
//                    g.FatturaEmessa,
//                    g.FatturaRicevuta,
//                    g.NumeroFattura,
//                    g.DataFattura,
//                    g.NoteFattura,
//                    g.AliquotaIva,       // ✅
//                    g.CausaleIva         // ✅
//                })
//                .OrderByDescending(g => g.DataOperazione)
//                .ToList();

//            return Json(fatture, JsonRequestBehavior.AllowGet);
//        }


//        public ActionResult CreaAnteprimaFattura(int idPratica)
//        {
//            var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
//            if (pratica == null)
//                return HttpNotFound("Pratica non trovata.");

//            var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);
//            if (cliente == null)
//                return HttpNotFound("Cliente non trovato.");

//            var cluster = db.Cluster.FirstOrDefault(c => c.ID_Pratiche == idPratica);
//            if (cluster == null)
//                return HttpNotFound("Cluster non trovato per la pratica.");

//            decimal importo = pratica.Budget * (cluster.PercentualePrevisione / 100m);
//            string tipoOperazione = db.GiornaleEconomico
//                .Any(g => g.ID_Pratiche == idPratica && g.Categoria == "Costo") ? "Debito" : "Credito";

//            var anteprima = new GiornaleCreditoDebito
//            {
//                ID_Pratiche = pratica.ID_Pratiche,
//                ID_Cliente = pratica.ID_Cliente,
//                TipoCliente = cliente.TipoCliente,
//                Importo = importo,
//                TipoOperazione = tipoOperazione,
//                Descrizione = $"Anteprima fattura per pratica: {pratica.Titolo}",
//                Stato = StatoCreditoDebito.InAttesa,
//                DataOperazione = DateTime.Now
//            };

//            return View("~/Views/Fatture/AnteprimaFattura.cshtml", anteprima);
//        }



//        [HttpPost]
//        public JsonResult ModificaFattura(GiornaleCreditoDebito model)
//        {
//            try
//            {
//                var voce = db.GiornaleCreditoDebito.FirstOrDefault(g => g.ID_Operazione == model.ID_Operazione);
//                if (voce == null)
//                {
//                    return Json(new { success = false, message = "Fattura non trovata." });
//                }

//                // 🔁 Aggiorna tutti i campi modificabili
//                voce.TipoOperazione = model.TipoOperazione;
//                voce.Importo = model.Importo;
//                voce.Descrizione = model.Descrizione;
//                voce.Stato = model.Stato;

//                voce.FatturaEmessa = model.FatturaEmessa;
//                voce.FatturaRicevuta = model.FatturaRicevuta;
//                voce.NumeroFattura = model.NumeroFattura;
//                voce.DataFattura = model.DataFattura;
//                voce.NoteFattura = model.NoteFattura;

//                voce.AliquotaIva = model.AliquotaIva;
//                voce.CausaleIva = model.CausaleIva;



//                // 📅 Tracciamento modifica
//                voce.UltimaModifica = DateTime.Now;
//                voce.ID_UtenteUltimaModifica = UserManager.GetIDUtenteCollegato();

//                db.SaveChanges();

//                return Json(new { success = true, message = "Fattura modificata correttamente." });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = "Errore durante la modifica della fattura: " + ex.Message });
//            }
//        }



//        [HttpPost]
//        public JsonResult EliminaFattura(int idOperazione)
//        {
//            try
//            {
//                var voce = db.GiornaleCreditoDebito.FirstOrDefault(g => g.ID_Operazione == idOperazione);
//                if (voce == null)
//                {
//                    return Json(new { success = false, message = "Voce non trovata." });
//                }

//                int idUtente = UserManager.GetIDUtenteCollegato();
//                DateTime now = DateTime.Now;

//                var archivio = new GiornaleCreditoDebito_a
//                {
//                    ID_Operazione = voce.ID_Operazione,
//                    ID_Pratiche = voce.ID_Pratiche,
//                    TipoCliente = voce.TipoCliente,
//                    ID_Cliente = voce.ID_Cliente,
//                    ID_UtenteCreatore = voce.ID_UtenteCreatore,
//                    TipoOperazione = voce.TipoOperazione,
//                    Importo = voce.Importo,
//                    Descrizione = voce.Descrizione,
//                    DataOperazione = voce.DataOperazione,
//                    UltimaModifica = voce.UltimaModifica,
//                    FatturaEmessa = voce.FatturaEmessa,
//                    FatturaRicevuta = voce.FatturaRicevuta,
//                    NumeroFattura = voce.NumeroFattura,
//                    DataFattura = voce.DataFattura,
//                    NoteFattura = voce.NoteFattura,
//                    AliquotaIva = voce.AliquotaIva,
//                    CausaleIva = voce.CausaleIva,
//                    Stato = voce.Stato,

//                    // ✅ Campi aggiuntivi per tracciamento invio SDI
//                    StatoInvioFattura = voce.StatoInvioFattura,
//                    StatoFatturaSDI = voce.StatoFatturaSDI,
//                    DataInvioFattura = voce.DataInvioFattura,

//                    DataArchiviazione = now,
//                    ID_UtenteArchiviazione = idUtente
//                };
//                db.GiornaleCreditoDebito_a.Add(archivio);

//                // 🧹 Pulizia solo dei campi relativi alla parte modificabile
//                voce.FatturaEmessa = null;
//                voce.FatturaRicevuta = null;
//                voce.NumeroFattura = null;
//                voce.DataFattura = null;
//                voce.NoteFattura = null;
//                voce.AliquotaIva = null;
//                voce.CausaleIva = null;

//                // 🔒 NON azzeriamo i campi di tracciamento invio
//                // voce.StatoInvioFattura = ... (mantieni)
//                // voce.StatoFatturaSDI = ...  (mantieni)
//                // voce.DataInvioFattura = ... (mantieni)

//                voce.UltimaModifica = now;
//                voce.ID_UtenteUltimaModifica = idUtente;

//                db.SaveChanges();

//                return Json(new { success = true, message = "Fattura archiviata e rimossa correttamente." });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = "Errore durante l’archiviazione della fattura: " + ex.Message });
//            }
//        }


//        [HttpPost]
//        public JsonResult SalvaDatiFattura(GiornaleCreditoDebito model)
//        {
//            try
//            {
//                var voce = db.GiornaleCreditoDebito.FirstOrDefault(g => g.ID_Operazione == model.ID_Operazione);
//                if (voce == null)
//                {
//                    return Json(new { success = false, message = "Voce non trovata." });
//                }

//                voce.FatturaEmessa = model.FatturaEmessa;
//                voce.FatturaRicevuta = model.FatturaRicevuta;
//                voce.NumeroFattura = model.NumeroFattura;
//                voce.DataFattura = model.DataFattura;
//                voce.NoteFattura = model.NoteFattura;

//                // ✅ Nuovi campi IVA
//                voce.AliquotaIva = model.AliquotaIva;
//                voce.CausaleIva = model.CausaleIva;

//                voce.UltimaModifica = DateTime.Now;
//                voce.ID_UtenteUltimaModifica = UserManager.GetIDUtenteCollegato();

//                db.SaveChanges();

//                return Json(new { success = true, message = "Dati fattura aggiornati correttamente." });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = "Errore durante l'aggiornamento: " + ex.Message });
//            }
//        }


//        [HttpGet]
//        public ActionResult GestioneFattureEmesse()
//        {
//            var lista = db.GiornaleCreditoDebito
//                .Where(g => g.FatturaEmessa == true && g.Stato == "Attivo")
//                .OrderByDescending(g => g.DataOperazione)
//                .Select(g => new GiornaleCreditoDebitoViewModel
//                {
//                    ID_Operazione = g.ID_Operazione,
//                    TipoOperazione = g.TipoOperazione,
//                    Importo = g.Importo,
//                    Descrizione = g.Descrizione,
//                    DataOperazione = g.DataOperazione,
//                    NumeroFattura = g.NumeroFattura,
//                    DataFattura = g.DataFattura,
//                    NoteFattura = g.NoteFattura,
//                    TipoCliente = g.TipoCliente,
//                    Stato = g.Stato
//                })
//                .ToList();

//            return View("~/Views/Fatture/FattureEmesse.cshtml", lista);
//        }


//        [HttpGet]
//        public ActionResult GestioneFattureRicevute()
//        {
//            var lista = db.GiornaleCreditoDebito
//                .Where(g => g.FatturaRicevuta == true && g.Stato == "Attivo")
//                .OrderByDescending(g => g.DataOperazione)
//                .Select(g => new GiornaleCreditoDebitoViewModel
//                {
//                    ID_Operazione = g.ID_Operazione,
//                    TipoOperazione = g.TipoOperazione,
//                    Importo = g.Importo,
//                    Descrizione = g.Descrizione,
//                    DataOperazione = g.DataOperazione,
//                    NumeroFattura = g.NumeroFattura,
//                    DataFattura = g.DataFattura,
//                    NoteFattura = g.NoteFattura,
//                    TipoCliente = g.TipoCliente,
//                    Stato = g.Stato
//                })
//                .ToList();

//            return View("~/Views/Fatture/FattureRicevute.cshtml", lista);
//        }


//        // calcolo IVA 
//        [HttpPost]
//        public JsonResult CalcolaIVA(decimal importo, decimal aliquota, bool ivaInclusa)
//        {
//            try
//            {
//                CalcoloFatturaResult result;

//                if (ivaInclusa)
//                {
//                    // L'importo include già l'IVA → calcolo il netto
//                    decimal netto = importo / (1 + (aliquota / 100));
//                    decimal iva = importo - netto;
//                    result = new CalcoloFatturaResult
//                    {
//                        ImportoNetto = Math.Round(netto, 2),
//                        ImportoIVA = Math.Round(iva, 2),
//                        ImportoTotale = importo
//                    };
//                }
//                else
//                {
//                    // L'importo è netto → calcolo IVA e totale
//                    decimal iva = importo * (aliquota / 100);
//                    decimal totale = importo + iva;
//                    result = new CalcoloFatturaResult
//                    {
//                        ImportoNetto = importo,
//                        ImportoIVA = Math.Round(iva, 2),
//                        ImportoTotale = Math.Round(totale, 2)
//                    };
//                }

//                return Json(new { success = true, result });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = "Errore nel calcolo IVA: " + ex.Message });
//            }
//        }



//        // sezione per generazione fatture elettroniche e TeamSystem

//        public string GeneraFatturaPA_XML(GiornaleCreditoDebito voce)
//        {
//            var cliente = db.Clienti.Find(voce.ID_Cliente);
//            if (cliente == null) throw new Exception("Cliente non trovato.");

//            var citta = cliente.ID_Citta.HasValue ? db.Citta.Find(cliente.ID_Citta.Value) : null;
//            var nazione = cliente.ID_Nazione.HasValue ? db.Nazioni.Find(cliente.ID_Nazione.Value) : null;

//            string folderPath = Server.MapPath("~/FattureElettroniche/XML/");
//            if (!Directory.Exists(folderPath))
//                Directory.CreateDirectory(folderPath);

//            string fileName = $"IT{voce.ID_Operazione}_FatturaPA.xml";
//            string filePath = Path.Combine(folderPath, fileName);

//            // Denominazione: se azienda usa solo Nome, se professionista Nome + Cognome
//            string denominazione = cliente.TipoCliente == "Azienda"
//                ? cliente.Nome
//                : $"{cliente.Nome} {cliente.Cognome}"; // professionista

//            var xml = new XElement("FatturaElettronica",
//                new XElement("CedentePrestatore",
//                    new XElement("Denominazione", denominazione),
//                    new XElement("CodiceFiscale", cliente.CodiceFiscale),
//                    new XElement("IdFiscaleIVA",
//                        new XElement("IdPaese", nazione?.CODStato ?? "IT"),
//                        new XElement("IdCodice", cliente.PIVA ?? cliente.CodiceFiscale)
//                    ),
//                    new XElement("Sede",
//                        new XElement("Indirizzo", cliente.Indirizzo),
//                        new XElement("CAP", citta?.CAP ?? ""),
//                        new XElement("Comune", citta?.NameLocalita ?? ""),
//                        new XElement("Provincia", citta?.SGLPRV ?? ""),
//                        new XElement("Nazione", nazione?.CODStato ?? "IT")
//                    )
//                ),
//                new XElement("CessionarioCommittente",
//                    new XElement("CodiceDestinatario", cliente.CodiceUnivoco ?? "0000000"),
//                    new XElement("Denominazione", denominazione),
//                    new XElement("CodiceFiscale", cliente.CodiceFiscale),
//                    new XElement("Sede",
//                        new XElement("Indirizzo", cliente.Indirizzo),
//                        new XElement("CAP", citta?.CAP ?? ""),
//                        new XElement("Comune", citta?.NameLocalita ?? ""),
//                        new XElement("Provincia", citta?.SGLPRV ?? ""),
//                        new XElement("Nazione", nazione?.CODStato ?? "IT")
//                    )
//                ),
//                new XElement("DatiBeniServizi",
//                    new XElement("DettaglioLinee",
//                        new XElement("Descrizione", voce.Descrizione),
//                        new XElement("Quantita", 1),
//                        new XElement("PrezzoUnitario", voce.Importo),
//                        new XElement("AliquotaIVA", voce.AliquotaIva)
//                    ),
//                    new XElement("DatiRiepilogo",
//                        new XElement("AliquotaIVA", voce.AliquotaIva),
//                        new XElement("ImponibileImporto", voce.Importo),
//                        new XElement("Imposta", Math.Round((voce.Importo * voce.AliquotaIva.Value) / 100, 2)),
//                        new XElement("EsigibilitaIVA", "I")
//                    )
//                ),
//                new XElement("NumeroFattura", voce.NumeroFattura),
//                new XElement("DataFattura", voce.DataFattura?.ToString("yyyy-MM-dd"))
//            );

//            xml.Save(filePath);
//            return filePath;
//        }


//        [HttpPost]
//        public async Task<JsonResult> InviaFatturaTeamSystem(int idOperazione)
//        {
//            // ✅ Recupera la voce fattura dal database
//            var voce = db.GiornaleCreditoDebito.Find(idOperazione);
//            if (voce == null)
//                return Json(new { success = false, message = "Voce non trovata" });

//            // ✅ Genera il file XML conforme a FatturaPA
//            string xmlPath = GeneraFatturaPA_XML(voce);
//            var xmlContent = System.IO.File.ReadAllText(xmlPath);

//            // ✅ Prepara il payload in base64 da inviare via API
//            var payload = new
//            {
//                fileName = Path.GetFileName(xmlPath),
//                xmlContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlContent))
//            };

//            using (var client = new HttpClient())
//            {
//                // ✅ Base URL dell'API TeamSystem
//                client.BaseAddress = new Uri("https://api.teamsystem.com/");

//                // ✅ Inserisci qui il tuo token, idealmente da Web.config:
//                // <appSettings> <add key="TeamSystemToken" value="IL_TUO_TOKEN" /> </appSettings>
//                client.DefaultRequestHeaders.Authorization =
//                    new AuthenticationHeaderValue("Bearer", ConfigurationManager.AppSettings["TeamSystemToken"]);

//                // ✅ Serializza il payload in JSON
//                var jsonPayload = JsonConvert.SerializeObject(payload);
//                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

//                // ✅ Chiamata POST all'endpoint API di TeamSystem
//                var response = await client.PostAsync("api/fatture/invio", content);

//                if (response.IsSuccessStatusCode)
//                {
//                    // ✅ Se successo, aggiorna lo stato nel DB
//                    voce.StatoInvioFattura = "Inviata";
//                    voce.DataInvioFattura = DateTime.Now;
//                    db.SaveChanges();

//                    return Json(new { success = true, message = "Fattura inviata a TeamSystem correttamente." });
//                }
//                else
//                {
//                    return Json(new { success = false, message = $"Errore TeamSystem: {response.StatusCode}" });
//                }
//            }
//        }

//        [HttpGet]
//        public async Task<JsonResult> VerificaStatoFatturaTeamSystem(string numeroFattura)
//        {
//            try
//            {
//                using (var client = new HttpClient())
//                {
//                    // ✅ URL base dell’API TeamSystem
//                    client.BaseAddress = new Uri("https://api.teamsystem.com/");

//                    // ✅ Inserimento token da Web.config
//                    client.DefaultRequestHeaders.Authorization =
//                        new AuthenticationHeaderValue("Bearer", ConfigurationManager.AppSettings["TeamSystemToken"]);

//                    // ✅ Chiamata GET per ottenere lo stato della fattura te lo deve dare teamsystem sono degli endpoint che restituiscono i dati 
//                    var response = await client.GetAsync($"api/fatture/stato?numero={numeroFattura}");
//                    if (response.IsSuccessStatusCode)
//                    {
//                        // ❗ Usa Newtonsoft se ReadAsAsync non è disponibile
//                        var jsonString = await response.Content.ReadAsStringAsync();
//                        dynamic result = JsonConvert.DeserializeObject<dynamic>(jsonString);

//                        string stato = result.stato;
//                        string dettagli = result.descrizione;

//                        var voce = db.GiornaleCreditoDebito.FirstOrDefault(f => f.NumeroFattura == numeroFattura);
//                        if (voce != null)
//                        {
//                            voce.StatoFatturaSDI = stato;
//                            voce.NoteFattura += "\nEsito TeamSystem: " + dettagli;
//                            voce.UltimaModifica = DateTime.Now;
//                            db.SaveChanges();
//                        }

//                        return Json(new { success = true, stato, dettagli });
//                    }
//                    else
//                    {
//                        return Json(new { success = false, message = "Errore risposta stato da TeamSystem" });
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = "Eccezione TeamSystem: " + ex.Message });
//            }
//        }

//        [HttpGet]
//        public ActionResult GeneraAnteprimaFattura(int idPratica)
//        {
//            try
//            {
//                var anteprimaEsistente = db.GiornaleCreditoDebito
//                    .FirstOrDefault(f => f.ID_Pratiche == idPratica && f.NumeroFattura == null);

//                if (anteprimaEsistente != null)
//                {
//                    return Json(new { success = false, message = "L'anteprima è già presente." }, JsonRequestBehavior.AllowGet);
//                }

//                var service = new FatturaService(db);
//                var anteprima = service.GeneraAnteprimaFattura(idPratica);
//                db.GiornaleCreditoDebito.Add(anteprima);
//                db.SaveChanges();

//                return Json(new { success = true, message = "Anteprima generata con successo." }, JsonRequestBehavior.AllowGet);
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = $"Errore: {ex.Message}" }, JsonRequestBehavior.AllowGet);
//            }
//        }



//        //ESPORTAZIONE IN CSV E PDF 

//        [HttpGet]
//        public FileResult EsportaFattureCsv()
//        {
//            var fatture = db.GiornaleCreditoDebito
//                .Where(g => g.FatturaEmessa == true || g.FatturaRicevuta == true)
//                .OrderByDescending(g => g.DataOperazione)
//                .ToList();

//            var csv = new StringBuilder();
//            csv.AppendLine("Numero Fattura;Cliente;Tipo Cliente;Data Fattura;Importo;Aliquota IVA;Causale IVA");

//            foreach (var f in fatture)
//            {
//                var cliente = db.Clienti.Find(f.ID_Cliente);
//                string nomeCliente = cliente?.TipoCliente == "Professionista"
//                    ? $"{cliente?.Nome} {cliente?.Cognome}"
//                    : cliente?.Nome;

//                string causale = string.IsNullOrWhiteSpace(f.CausaleIva) ? "" : f.CausaleIva;

//                csv.AppendLine($"{f.NumeroFattura};{nomeCliente};{f.TipoCliente};{f.DataFattura:yyyy-MM-dd};{f.Importo};{f.AliquotaIva}%;{causale}");
//            }

//            byte[] buffer = Encoding.UTF8.GetBytes(csv.ToString());
//            return File(buffer, "text/csv", "Fatture.csv");
//        }

//        [HttpGet]
//        public ActionResult EsportaFatturePDF()
//        {
//            var lista = (from g in db.GiornaleCreditoDebito
//                         join p in db.Pratiche on g.ID_Pratiche equals p.ID_Pratiche
//                         join c in db.Clienti on g.ID_Cliente equals c.ID_Cliente
//                         where g.FatturaEmessa == true || g.FatturaRicevuta == true
//                         orderby g.DataFattura descending
//                         select new GiornaleCreditoDebitoViewModel
//                         {
//                             ID_Operazione = g.ID_Operazione,
//                             TipoOperazione = g.TipoOperazione,
//                             Importo = g.Importo,
//                             NumeroFattura = g.NumeroFattura,
//                             DataFattura = g.DataFattura,
//                             AliquotaIva = g.AliquotaIva,
//                             CausaleIva = g.CausaleIva,
//                             TipoCliente = c.TipoCliente,
//                             NomeCliente = c.TipoCliente == "Professionista"
//                                            ? c.Nome + " " + c.Cognome
//                                            : c.Nome
//                         }).ToList();

//            return new Rotativa.ViewAsPdf("~/Views/Fatture/PDF_Fatture.cshtml", lista)
//            {
//                FileName = $"Fatture_{DateTime.Now:yyyyMMdd}.pdf",
//                PageSize = Rotativa.Options.Size.A4,
//                PageOrientation = Rotativa.Options.Orientation.Landscape
//            };
//        }

//    }
//}