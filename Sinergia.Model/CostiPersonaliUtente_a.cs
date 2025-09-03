namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class CostiPersonaliUtente_a
    {
        [Key]
        public int IDVersioneCostoPersonale { get; set; }

        public int ID_CostoPersonale { get; set; }

        public int ID_Utente { get; set; }

        [StringLength(200)]
        public string Descrizione { get; set; }

        public decimal? Importo { get; set; }

        [StringLength(20)]
        public string ModalitaRipartizione { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInserimento { get; set; }

        public bool? Approvato { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public string ModificheTestuali { get; set; }

        public int? ID_AnagraficaCostoProfessionista { get; set; }
    }
}
