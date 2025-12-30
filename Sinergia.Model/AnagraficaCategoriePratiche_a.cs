namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class AnagraficaCategoriePratiche_a
    {
        [Key]
        public int ID_CategoriaPratica_Archivio { get; set; }

        public int ID_CategoriaPratica { get; set; }

        [StringLength(200)]
        public string Tipo { get; set; }

        [StringLength(200)]
        public string Materia { get; set; }

        [StringLength(200)]
        public string Note { get; set; }

        public bool Attivo { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public DateTime? DataArchiviazione { get; set; }
    }
}
