namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Economico_a
    {
        [Key]
        public int ID_EconomicoArchivio { get; set; }

        public int? ID_EconomicoOriginale { get; set; }

        public int? ID_Pratiche { get; set; }

        public int? ID_Professionista { get; set; }

        public decimal? Percentuale { get; set; }

        [StringLength(50)]
        public string TipoOperazione { get; set; }

        [StringLength(500)]
        public string Descrizione { get; set; }

        public decimal? ImportoEconomico { get; set; }

        public DateTime? DataRegistrazione { get; set; }

        public int? ID_AvvisoParcella { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
