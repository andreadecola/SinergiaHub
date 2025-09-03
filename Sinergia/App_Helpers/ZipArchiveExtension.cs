using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Sinergia.App_Helpers
{
    /// <summary>
    /// Estensioni per creare archivi ZIP da file o directory.
    /// </summary>
    public static class ZipArchiveExtension
    {
        /// <summary>
        /// Aggiunge un file o una cartella all'interno di un archivio ZIP.
        /// </summary>
        /// <param name="archive">Archivio ZIP su cui lavorare</param>
        /// <param name="sourceName">Percorso assoluto del file o della cartella da inserire</param>
        /// <param name="entryName">Percorso relativo all’interno dello ZIP (opzionale)</param>
        public static void CreateEntryFromAny(this ZipArchive archive, string sourceName, string entryName = "")
        {
            var fileName = Path.GetFileName(sourceName);

            if (File.GetAttributes(sourceName).HasFlag(FileAttributes.Directory))
            {
                // Se è una cartella, ricorsivamente aggiungiamo tutti i contenuti
                archive.CreateEntryFromDirectory(sourceName, Path.Combine(entryName, fileName));
            }
            else
            {
                // Se è un file, lo aggiungiamo direttamente allo ZIP
                archive.CreateEntryFromFile(sourceName, Path.Combine(entryName, fileName), CompressionLevel.Optimal);
            }
        }

        /// <summary>
        /// Aggiunge ricorsivamente tutti i file e sotto-cartelle di una directory allo ZIP.
        /// </summary>
        /// <param name="archive">Archivio ZIP su cui lavorare</param>
        /// <param name="sourceDirName">Percorso della directory sorgente</param>
        /// <param name="entryName">Percorso relativo all’interno dello ZIP (opzionale)</param>
        public static void CreateEntryFromDirectory(this ZipArchive archive, string sourceDirName, string entryName = "")
        {
            // Ottiene tutti i file e le sottodirectory
            string[] files = Directory.GetFiles(sourceDirName)
                                      .Concat(Directory.GetDirectories(sourceDirName))
                                      .ToArray();

            // Li aggiunge ricorsivamente
            foreach (var file in files)
            {
                archive.CreateEntryFromAny(file, entryName);
            }
        }
    }
}
