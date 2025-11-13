namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class PlafondUtente_a
    {
        [Key]
        public int ID_PlannedPlafond_Archivio { get; set; }

        public int ID_PlannedPlafond_Originale { get; set; }

        public int ID_Utente { get; set; }

        public decimal ImportoTotale { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoPlafond { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInizio { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataFine { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataUltimaModifica { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        public int? ID_Incasso { get; set; }

        public decimal Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataVersamento { get; set; }

        public int ID_UtenteInserimento { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInserimento { get; set; }

        public string Note { get; set; }

        public int? ID_Pratiche { get; set; }

        public int? ID_CostoPersonale { get; set; }

        public DateTime? DataCompetenzaFinanziaria { get; set; }

        [StringLength(100)]
        public string Operazione { get; set; }
    }
}
