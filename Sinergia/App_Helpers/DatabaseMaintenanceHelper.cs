using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public static class DatabaseMaintenanceHelper
    {
        // 📍 Percorso del file persistente (in App_Data)
        private static readonly string logFilePath = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "UltimaPuliziaLog.txt");

        // 📅 Variabile interna con l’ultima data caricata
        private static DateTime ultimaPulizia = CaricaUltimaPulizia();

        // =====================================================
        // 🔹 Carica la data dell'ultima pulizia da file
        // =====================================================
        private static DateTime CaricaUltimaPulizia()
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    string text = File.ReadAllText(logFilePath);
                    if (DateTime.TryParse(text, out DateTime data))
                        return data;
                }
            }
            catch
            {
                // ignora eventuali errori di lettura
            }
            return DateTime.MinValue;
        }

        // =====================================================
        // 🔹 Salva la data corrente nel file di log
        // =====================================================
        private static void SalvaUltimaPulizia()
        {
            try
            {
                // assicura che la cartella App_Data esista
                string directory = Path.GetDirectoryName(logFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(logFilePath, DateTime.Now.ToString("O")); // formato ISO
            }
            catch
            {
                // ignora eventuali errori di scrittura
            }
        }

        // =====================================================
        // 🔹 Metodo principale: pulizia log Sinergia
        // =====================================================
        public static void PulisciLogSinergia()
        {
            try
            {
                // ✅ Esegui al massimo una volta ogni 12 ore
                if ((DateTime.Now - ultimaPulizia).TotalHours < 12)
                    return;

                using (var db = new SinergiaDB())
                {
                    string sqlPulizia = @"
                        USE Sinergia;
                        IF EXISTS (SELECT name FROM sys.databases WHERE name = 'Sinergia')
                        BEGIN
                            ALTER DATABASE Sinergia SET RECOVERY SIMPLE;
                            DBCC SHRINKFILE (Sinergia_log, 100);
                            ALTER DATABASE Sinergia SET RECOVERY FULL;
                        END";

                    db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, sqlPulizia);

                    ultimaPulizia = DateTime.Now;
                    SalvaUltimaPulizia(); // 🧾 Salva la data su file
                    System.Diagnostics.Debug.WriteLine($"🧹 Pulizia log Sinergia completata alle {ultimaPulizia}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Errore durante la pulizia log Sinergia: " + ex.Message);
            }
        }
    }
}