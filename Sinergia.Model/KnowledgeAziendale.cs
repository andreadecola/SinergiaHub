namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("KnowledgeAziendale")]
    public partial class KnowledgeAziendale
    {
        public int KnowledgeAziendaleID { get; set; }

        [Required]
        [StringLength(100)]
        public string Tipo { get; set; }

        [Required]
        [StringLength(200)]
        public string Titolo { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataDocumento { get; set; }

        public string Descrizione { get; set; }

        [Required]
        [StringLength(50)]
        public string Destinatario { get; set; }

        [StringLength(200)]
        public string AllegatoNome { get; set; }

        [StringLength(500)]
        public string AllegatoPercorso { get; set; }

        public int Versione { get; set; }

        public bool Vecchio { get; set; }

        [StringLength(150)]
        public string CreatoDa { get; set; }

        public DateTime DataCreazione { get; set; }

        [StringLength(150)]
        public string ModificatoDa { get; set; }

        public DateTime? DataModifica { get; set; }
    }
}
