namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("AnagraficaCostiProfessionista")]
    public partial class AnagraficaCostiProfessionista
    {
        [Key]
        public int ID_AnagraficaCostoProfessionista { get; set; }

        [Required]
        [StringLength(255)]
        public string Descrizione { get; set; }

        [StringLength(50)]
        public string ModalitaRipartizione { get; set; }

        [StringLength(50)]
        public string TipoPeriodicita { get; set; }

        public decimal? ImportoBase { get; set; }

        public bool? Attivo { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }
    }
}
