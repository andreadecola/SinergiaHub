namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("CategorieCosti")]
    public partial class CategorieCosti
    {
        [Key]
        public int ID_Categoria { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; }

        [StringLength(255)]
        public string Descrizione { get; set; }

        public bool Attivo { get; set; }

        public DateTime DataCreazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }
    }
}
