namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Previsione_a
    {
        [Key]
        public int ID_PrevisioneArchivio { get; set; }

        public int? ID_PrevisioneOriginale { get; set; }

        public int? ID_Pratiche { get; set; }

        public int? ID_Professionista { get; set; }

        public decimal? Percentuale { get; set; }

        [StringLength(50)]
        public string TipoOperazione { get; set; }

        [StringLength(500)]
        public string Descrizione { get; set; }

        public decimal? ImportoPrevisto { get; set; }

        public DateTime? DataPrevisione { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
