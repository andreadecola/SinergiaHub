namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("AnagraficaCostiPratica")]
    public partial class AnagraficaCostiPratica
    {
        [Key]
        public int ID_AnagraficaCosto { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; }

        public string Descrizione { get; set; }

        public bool Attivo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataCreazione { get; set; }

        public int ID_UtenteCreatore { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoCreatore { get; set; }

        [Required]
        [StringLength(20)]
        public string Stato { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_Categoria { get; set; }
    }
}
