namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Professioni_a
    {
        [Key]
        public int ID_Archivio { get; set; }

        public int? ID_ProfessioniOriginali { get; set; }

        [StringLength(50)]
        public string Codice { get; set; }

        [StringLength(200)]
        public string Descrizione { get; set; }

        public int? ID_ProfessionistaRiferimento { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        public decimal? PercentualeContributoIntegrativo { get; set; }
    }
}
