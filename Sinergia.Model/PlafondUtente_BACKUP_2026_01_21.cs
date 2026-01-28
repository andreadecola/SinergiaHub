namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class PlafondUtente_BACKUP_2026_01_21
    {
        [Key]
        [Column(Order = 0)]
        public int ID_PlannedPlafond { get; set; }

        [Key]
        [Column(Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID_Utente { get; set; }

        [Key]
        [Column(Order = 2)]
        public decimal ImportoTotale { get; set; }

        [Key]
        [Column(Order = 3)]
        [StringLength(20)]
        public string TipoPlafond { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInizio { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataFine { get; set; }

        [Key]
        [Column(Order = 4)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_Incasso { get; set; }

        [Key]
        [Column(Order = 5)]
        public decimal Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataVersamento { get; set; }

        [Key]
        [Column(Order = 6)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
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
