namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("BilancioProfessionista")]
    public partial class BilancioProfessionista
    {
        [Key]
        public int ID_Bilancio { get; set; }

        public int ID_Professionista { get; set; }

        public DateTime DataRegistrazione { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoVoce { get; set; }

        [Required]
        [StringLength(100)]
        public string Categoria { get; set; }

        [Required]
        [StringLength(255)]
        public string Descrizione { get; set; }

        public decimal Importo { get; set; }

        public int? ID_Pratiche { get; set; }

        [Required]
        [StringLength(20)]
        public string Stato { get; set; }

        [Required]
        [StringLength(30)]
        public string Origine { get; set; }

        public int ID_UtenteInserimento { get; set; }

        public DateTime DataInserimento { get; set; }

        public int? ID_Incasso { get; set; }

        public DateTime? DataCompetenzaEconomica { get; set; }

        public DateTime? DataCompetenzaFinanziaria { get; set; }
    }
}
