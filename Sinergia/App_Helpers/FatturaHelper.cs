//using Sinergia.Model;
//using Sinergia.Models;
//using System;
//using System.Linq;

//namespace Sinergia.App_Helpers
//{
//    public static class FatturaHelper
//    {
//        public static string GeneraNumeroFattura()
//        {
//            using (var db = new SinergiaDB())
//            {
//                var anno = DateTime.Now.Year;

//                // ⚠️ Estrai prima la stringa fuori dalla query
//                var prefissoFattura = $"FATT-{anno}";

//                var count = db.GiornaleCreditoDebito
//                              .Where(f => f.DataFattura.HasValue && f.DataFattura.Value.Year == anno
//                                       && f.NumeroFattura.StartsWith(prefissoFattura))
//                              .Count() + 1;

//                return $"{prefissoFattura}-{count:D4}";
//            }
//        }
//    }
//}
