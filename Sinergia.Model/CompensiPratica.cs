namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("CompensiPratica")]
    public partial class CompensiPratica
    {
        [Key]
        public int ID_Compenso { get; set; }

        public int ID_Pratiche { get; set; }

        [Required]
        [StringLength(100)]
        public string Tipo { get; set; }

        [Required]
        [StringLength(200)]
        public string Descrizione { get; set; }

        public decimal Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataInserimento { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int ID_UtenteDestinatario { get; set; }
    }
}
