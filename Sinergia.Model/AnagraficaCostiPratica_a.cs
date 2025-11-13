namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class AnagraficaCostiPratica_a
    {
        [Key]
        public int ID_AnagraficaCosto_a { get; set; }

        public int ID_AnagraficaOriginale { get; set; }

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

        public int ID_AnagraficaCosto_Originale { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        [Required]
        public string ModificheTestuali { get; set; }

        [Required]
        [StringLength(20)]
        public string Stato { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_Categoria { get; set; }
    }
}
