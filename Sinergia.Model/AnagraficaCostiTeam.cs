namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("AnagraficaCostiTeam")]
    public partial class AnagraficaCostiTeam
    {
        [Key]
        public int ID_AnagraficaCostoTeam { get; set; }

        public int? ID_Professione { get; set; }

        [Required]
        [StringLength(200)]
        public string Descrizione { get; set; }

        public decimal? Importo { get; set; }

        public bool Ricorrente { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        [Required]
        [StringLength(50)]
        public string Stato { get; set; }
    }
}
