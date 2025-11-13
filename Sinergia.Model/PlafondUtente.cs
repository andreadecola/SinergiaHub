namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("PlafondUtente")]
    public partial class PlafondUtente
    {
        [Key]
        public int ID_PlannedPlafond { get; set; }

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
