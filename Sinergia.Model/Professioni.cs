namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Professioni")]
    public partial class Professioni
    {
        public int ProfessioniID { get; set; }

        [StringLength(50)]
        public string Codice { get; set; }

        [StringLength(200)]
        public string Descrizione { get; set; }

        public int? ID_ProfessionistaRiferimento { get; set; }

        public decimal? PercentualeContributoIntegrativo { get; set; }
    }
}
